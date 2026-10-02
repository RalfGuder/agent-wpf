using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using AgentWpf.Application.Ports;
using AgentWpf.Domain;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Exceptions;
using FlaUI.UIA3;

namespace AgentWpf.Infrastructure.FlaUI;

/// <summary>
/// The UI Automation driver based on FlaUI/UIA3. Elements seen in captures or window lists are kept
/// by runtime id so that later commands can act on them.
/// </summary>
public sealed partial class FlaUiAutomationDriver : IAutomationDriver, IDisposable
{
    /// <summary>
    /// The maximum number of remembered elements; beyond that the oldest generation is dropped.
    /// </summary>
    public const int MaxRememberedElements = 200_000;

    // UIA_E_ELEMENTNOTAVAILABLE: the element was destroyed.
    private const int ElementNotAvailable = unchecked((int)0x80040201);

    private readonly UIA3Automation automation = new();

    private readonly ConcurrentDictionary<string, AutomationElement> elements = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<int, ProcessWatch> watches = new();

    /// <inheritdoc />
    public ProcessInfo GetProcess(int processId)
    {
        Debug.Assert(processId > 0, "Precondition: processId must be positive.");

        try
        {
            using var process = Process.GetProcessById(processId);
            var info = new ProcessInfo(processId, process.ProcessName, IntegrityLevel.IsHigherThanCurrent(processId));

            Debug.Assert(info.Name.Length > 0, "Postcondition: a running process has a name.");
            return info;
        }
        catch (ArgumentException)
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, string.Format(CultureInfo.InvariantCulture, "No process with id {0} is running.", processId), "open the app again");
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DesktopWindow> GetDesktopWindows()
    {
        var desktop = this.automation.GetDesktop();
        var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window))
            .Select(w => new DesktopWindow(w.Properties.ProcessId.ValueOrDefault, TitleOf(w)))
            .Where(w => w.ProcessId > 0 && w.Title.Length > 0)
            .ToList();

        Debug.Assert(windows.All(w => w.ProcessId > 0), "Postcondition: every window has an owner.");
        return windows;
    }

    /// <inheritdoc />
    public IReadOnlyList<WindowInfo> GetWindows(int processId)
    {
        Debug.Assert(processId > 0, "Precondition: processId must be positive.");

        var foreground = NativeMethods.GetForegroundWindow();
        var result = new List<WindowInfo>();
        var topLevel = this.automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(processId));
        foreach (var window in topLevel)
        {
            // Owned dialogs appear as child windows of their owner; list them first (they are on top).
            foreach (var child in SafeChildWindows(window))
            {
                this.AddWindow(result, child, foreground, isPopup: false);
            }

            var type = window.Properties.ControlType.ValueOrDefault;
            if (type is ControlType.Window or ControlType.Menu or ControlType.Pane or ControlType.ToolTip)
            {
                this.AddWindow(result, window, foreground, isPopup: type != ControlType.Window || IsPopupWindow(window));
            }
        }

        Debug.Assert(result.Count <= 1000, "Postcondition: a process has a sane number of windows.");
        return result;
    }

    /// <inheritdoc />
    public nint GetWindowHandle(string runtimeId) => this.On(runtimeId, e => e.Properties.NativeWindowHandle.ValueOrDefault);

    /// <inheritdoc />
    public long GetStructureChangeCount(int processId)
    {
        var watch = this.watches.GetOrAdd(processId, _ => new ProcessWatch());
        var windows = this.automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(processId));
        foreach (var window in windows)
        {
            var id = RuntimeIdOf(window);
            if (id.Length > 0 && watch.Registered.TryAdd(id, true))
            {
                window.RegisterStructureChangedEvent(TreeScope.Subtree, (_, _, _) => Interlocked.Increment(ref watch.Count));
                Interlocked.Increment(ref watch.Count);
            }
        }

        return Interlocked.Read(ref watch.Count);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.automation.UnregisterAllEvents();
        this.automation.Dispose();
    }

    /// <summary>Returns the textual runtime id, e.g. <c>42.1234.4.17</c>.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The runtime id, or an empty string when the element has none.</returns>
    internal static string RuntimeIdOf(AutomationElement element)
    {
        var parts = element.Properties.RuntimeId.ValueOrDefault;
        return parts is { Length: > 0 } ? string.Join('.', parts) : string.Empty;
    }

    /// <summary>Remembers an element so that later commands can address it by runtime id.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The runtime id.</returns>
    internal string Remember(AutomationElement element)
    {
        var id = RuntimeIdOf(element);
        if (id.Length > 0)
        {
            if (this.elements.Count >= MaxRememberedElements)
            {
                this.elements.Clear();
            }

            this.elements[id] = element;
        }

        return id;
    }

    // Runs an operation on a remembered element, translating "element gone" failures.
    private T On<T>(string runtimeId, Func<AutomationElement, T> operation)
    {
        if (!this.elements.TryGetValue(runtimeId, out var element))
        {
            throw new ElementGoneException(runtimeId);
        }

        try
        {
            if (!IsAttachedToLiveWindow(element))
            {
                this.elements.TryRemove(runtimeId, out _);
                throw new ElementGoneException(runtimeId);
            }

            return operation(element);
        }
        catch (ElementNotAvailableException ex)
        {
            this.elements.TryRemove(runtimeId, out _);
            throw new ElementGoneException(runtimeId, ex);
        }
        catch (COMException ex) when (ex.HResult == ElementNotAvailable)
        {
            this.elements.TryRemove(runtimeId, out _);
            throw new ElementGoneException(runtimeId, ex);
        }
        catch (PatternNotSupportedException ex)
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, "The element does not support this action: " + ex.Message, "take a snapshot and pick another element, or use --input");
        }
    }

    private void On(string runtimeId, Action<AutomationElement> operation) => this.On(runtimeId, e => { operation(e); return true; });

    private void AddWindow(List<WindowInfo> result, AutomationElement window, nint foreground, bool isPopup)
    {
        var id = this.Remember(window);
        if (id.Length == 0 || result.Any(w => w.RuntimeId == id))
        {
            return;
        }

        var isModal = window.Patterns.Window.PatternOrDefault?.IsModal.ValueOrDefault ?? false;
        var handle = window.Properties.NativeWindowHandle.ValueOrDefault;
        result.Add(new WindowInfo(id, TitleOf(window), isModal, handle != 0 && handle == foreground, isPopup));
    }

    // WPF keeps automation peers of closed windows alive, and acting on them silently succeeds (or
    // crashes the app). An element is only alive when every window on its ancestor chain still exists
    // and the chain reaches the desktop.
    private bool IsAttachedToLiveWindow(AutomationElement element)
    {
        var desktop = this.automation.GetDesktop();
        var current = element;
        for (var depth = 0; depth < MaxAncestorDepth; depth++)
        {
            if (current.Equals(desktop))
            {
                return true;
            }

            var handle = current.Properties.NativeWindowHandle.ValueOrDefault;
            if (handle != 0 && !NativeMethods.IsWindow(handle))
            {
                return false;
            }

            // Only the desktop has no parent; any other element without one is detached.
            current = current.Parent;
            if (current is null)
            {
                return false;
            }
        }

        return true;
    }

    // The Win32 window text is the real title; WPF derives the UIA name of a window from its content.
    private static unsafe string TitleOf(AutomationElement window)
    {
        var handle = window.Properties.NativeWindowHandle.ValueOrDefault;
        var length = handle == 0 ? 0 : Math.Min(NativeMethods.GetWindowTextLength(handle), 4096);
        if (length > 0)
        {
            var buffer = new char[length + 1];
            fixed (char* text = buffer)
            {
                var copied = NativeMethods.GetWindowText(handle, text, buffer.Length);
                if (copied > 0)
                {
                    return new string(buffer, 0, copied);
                }
            }
        }

        return window.Properties.Name.ValueOrDefault ?? string.Empty;
    }

    private static AutomationElement[] SafeChildWindows(AutomationElement window)
    {
        try
        {
            return window.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
        }
        catch (COMException)
        {
            return [];
        }
    }

    // WPF renders popups (menus, drop-downs, tool tips) as unnamed top-level windows of class "Popup".
    private static bool IsPopupWindow(AutomationElement window)
        => string.Equals(window.Properties.ClassName.ValueOrDefault, "Popup", StringComparison.Ordinal);

    // Structure-change bookkeeping of one process for wait --idle.
    private sealed class ProcessWatch
    {
        public ConcurrentDictionary<string, bool> Registered { get; } = new(StringComparer.Ordinal);

        // Incremented from UIA event threads.
        public long Count;
    }
}
