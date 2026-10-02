using System.Collections.Generic;
using AgentWpf.Domain;

namespace AgentWpf.Application.Ports;

/// <summary>
/// The UI Automation boundary. Elements are addressed by their textual runtime id; operations on an
/// element that no longer exists throw <see cref="ElementGoneException"/>, missing patterns or other
/// expected failures throw <see cref="AgentWpfException"/>.
/// </summary>
public interface IAutomationDriver
{
    /// <summary>Gets facts about a running process.</summary>
    /// <param name="processId">The process id.</param>
    /// <returns>The process facts.</returns>
    ProcessInfo GetProcess(int processId);

    /// <summary>Lists the visible top-level windows on the desktop.</summary>
    /// <returns>The windows with their owning process ids.</returns>
    IReadOnlyList<DesktopWindow> GetDesktopWindows();

    /// <summary>Lists the top-level windows of a process, including dialogs.</summary>
    /// <param name="processId">The process id.</param>
    /// <returns>The windows in z-order, topmost first.</returns>
    IReadOnlyList<WindowInfo> GetWindows(int processId);

    /// <summary>Captures an element and its realized descendants.</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    /// <param name="maxDepth">The maximum depth to traverse; 0 captures the element only.</param>
    /// <returns>The captured tree.</returns>
    ElementSnapshot Capture(string runtimeId, int maxDepth);

    /// <summary>Returns the control patterns an element supports.</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    /// <returns>The supported patterns.</returns>
    IReadOnlySet<PatternKind> GetPatterns(string runtimeId);

    /// <summary>Invokes the element (InvokePattern).</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    void Invoke(string runtimeId);

    /// <summary>Sets the element's value (ValuePattern).</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    /// <param name="value">The new value.</param>
    void SetValue(string runtimeId, string value);

    /// <summary>Reads the toggle state (TogglePattern).</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    /// <returns>The current state.</returns>
    ToggleState GetToggleState(string runtimeId);

    /// <summary>Advances the toggle state (TogglePattern).</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    void Toggle(string runtimeId);

    /// <summary>Selects the element (SelectionItemPattern).</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    void Select(string runtimeId);

    /// <summary>Selects the option with the given text in a combo box or list.</summary>
    /// <param name="runtimeId">The container's runtime id.</param>
    /// <param name="option">The visible option text.</param>
    void SelectOption(string runtimeId, string option);

    /// <summary>Expands the element (ExpandCollapsePattern).</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    void Expand(string runtimeId);

    /// <summary>Collapses the element (ExpandCollapsePattern).</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    void Collapse(string runtimeId);

    /// <summary>Sets keyboard focus to the element.</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    void Focus(string runtimeId);

    /// <summary>Scrolls the element into view (ScrollItemPattern), realizing virtualized items.</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    void ScrollIntoView(string runtimeId);

    /// <summary>Scrolls a container (ScrollPattern).</summary>
    /// <param name="runtimeId">The container's runtime id.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="pages">The number of pages to scroll.</param>
    void Scroll(string runtimeId, ScrollDirection direction, int pages);

    /// <summary>Clicks the element's center with the real mouse (SendInput).</summary>
    /// <param name="runtimeId">The element's runtime id.</param>
    /// <param name="button">The mouse button.</param>
    /// <param name="clickCount">1 for a click, 2 for a double click.</param>
    void Click(string runtimeId, MouseButton button, int clickCount);

    /// <summary>Types text with the real keyboard (SendInput) into the element or the focused element.</summary>
    /// <param name="runtimeId">The target element's runtime id, or <see langword="null"/> for the focused element.</param>
    /// <param name="text">The text to type.</param>
    void TypeText(string? runtimeId, string text);

    /// <summary>Presses a key chord such as <c>Enter</c> or <c>Control+S</c> with the real keyboard.</summary>
    /// <param name="runtimeId">The target element's runtime id, or <see langword="null"/> for the focused element.</param>
    /// <param name="keys">The key chord.</param>
    void PressKeys(string? runtimeId, string keys);

    /// <summary>Reads rows of a grid or table, realizing virtualized rows as needed.</summary>
    /// <param name="runtimeId">The grid's runtime id.</param>
    /// <param name="firstRow">The zero-based first row.</param>
    /// <param name="rowCount">The number of rows to read.</param>
    /// <returns>The rows that exist in the requested range.</returns>
    TableData ReadTable(string runtimeId, int firstRow, int rowCount);

    /// <summary>Closes a window (WindowPattern).</summary>
    /// <param name="runtimeId">The window's runtime id.</param>
    void CloseWindow(string runtimeId);

    /// <summary>Returns the native handle of a window.</summary>
    /// <param name="runtimeId">The window's runtime id.</param>
    /// <returns>The window handle.</returns>
    nint GetWindowHandle(string runtimeId);

    /// <summary>
    /// Returns a counter that increases with every structure change event of the process, used to
    /// detect when the UI has settled (<c>wait --idle</c>).
    /// </summary>
    /// <param name="processId">The process id.</param>
    /// <returns>The current counter value.</returns>
    long GetStructureChangeCount(int processId);
}
