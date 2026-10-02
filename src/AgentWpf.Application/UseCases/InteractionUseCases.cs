using System.Globalization;
using AgentWpf.Application.Ports;
using AgentWpf.Application.Sessions;
using AgentWpf.Domain;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// Options of the <c>click</c> and <c>dblclick</c> commands.
/// </summary>
public sealed record ClickOptions
{
    /// <summary>Gets a value indicating whether the real mouse is used instead of UIA patterns (<c>--input</c>).</summary>
    public bool Input { get; init; }

    /// <summary>Gets the mouse button for <c>--input</c> clicks.</summary>
    public MouseButton Button { get; init; } = MouseButton.Left;

    /// <summary>Gets the number of clicks for <c>--input</c> clicks.</summary>
    public int ClickCount { get; init; } = 1;
}

/// <summary>
/// Acting on elements: <c>click</c>, <c>fill</c>, <c>select</c>, <c>check</c>, <c>expand</c>,
/// <c>focus</c>, <c>scroll</c>, <c>press</c> and <c>type</c>. Pattern-based by default; the real
/// mouse and keyboard are only used when the agent asks for it.
/// </summary>
public sealed class InteractionUseCases : SessionUseCase
{
    // Tri-state check boxes need at most two toggles to reach any state; one more for safety.
    private const int MaxToggles = 3;

    private const string InputHint = "use --input to act with the real mouse and keyboard";

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionUseCases"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="driver">The automation driver.</param>
    public InteractionUseCases(AgentSession session, IAutomationDriver driver)
        : base(session, driver)
    {
    }

    /// <summary>
    /// Clicks an element with the best matching pattern: Invoke, Toggle, SelectionItem or ExpandCollapse.
    /// </summary>
    /// <param name="refId">The element.</param>
    /// <param name="options">The click options.</param>
    /// <returns>The result naming the performed action.</returns>
    public CommandResult Click(RefId refId, ClickOptions options)
    {
        System.Diagnostics.Debug.Assert(options != null, "Precondition: options must not be null.");
        System.Diagnostics.Debug.Assert(options.ClickCount is 1 or 2, "Precondition: click count must be 1 or 2.");

        if (options.Input)
        {
            this.OnElement(refId, id => this.Driver.Click(id, options.Button, options.ClickCount));
            return Done("clicked", refId, " (mouse)");
        }

        if (options.ClickCount != 1 || options.Button != MouseButton.Left)
        {
            throw new AgentWpfException(ErrorCode.Usage, "Double and right clicks need the real mouse.", "add --input");
        }

        var verb = this.OnElement(refId, id => this.ClickByPattern(refId, id));

        System.Diagnostics.Debug.Assert(verb.Length > 0, "Postcondition: an action was performed.");
        return Done(verb, refId);
    }

    /// <summary>
    /// Replaces the value of an element (ValuePattern), or types it with <c>--input</c>.
    /// </summary>
    /// <param name="refId">The element.</param>
    /// <param name="text">The new value.</param>
    /// <param name="input">Whether to type with the real keyboard.</param>
    /// <returns>The result.</returns>
    public CommandResult Fill(RefId refId, string text, bool input)
    {
        System.Diagnostics.Debug.Assert(text != null, "Precondition: text must not be null.");

        this.OnElement(refId, id =>
        {
            if (input)
            {
                this.Driver.Focus(id);
                this.Driver.PressKeys(id, "Control+A");
                this.Driver.TypeText(id, text);
            }
            else if (this.Driver.GetPatterns(id).Contains(PatternKind.Value))
            {
                this.Driver.SetValue(id, text);
            }
            else
            {
                throw new AgentWpfException(ErrorCode.ActionFailed, this.Describe(refId, id) + " has no editable value.", InputHint);
            }
        });

        return Done("filled", refId);
    }

    /// <summary>
    /// Checks or unchecks a check box or toggle button, or selects a radio button.
    /// </summary>
    /// <param name="refId">The element.</param>
    /// <param name="on">The desired state.</param>
    /// <returns>The result.</returns>
    public CommandResult SetChecked(RefId refId, bool on)
    {
        this.OnElement(refId, id =>
        {
            var patterns = this.Driver.GetPatterns(id);
            if (patterns.Contains(PatternKind.Toggle))
            {
                this.ToggleTo(refId, id, on ? ToggleState.On : ToggleState.Off);
            }
            else if (on && patterns.Contains(PatternKind.SelectionItem))
            {
                this.Driver.Select(id);
            }
            else
            {
                throw new AgentWpfException(ErrorCode.ActionFailed, this.Describe(refId, id) + " cannot be " + (on ? "checked." : "unchecked."), "use click on another option, or click --input");
            }
        });

        return Done(on ? "checked" : "unchecked", refId);
    }

    /// <summary>
    /// Selects an option of a combo box or list by its visible text.
    /// </summary>
    /// <param name="refId">The combo box or list.</param>
    /// <param name="option">The option text.</param>
    /// <returns>The result.</returns>
    public CommandResult Select(RefId refId, string option)
    {
        System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(option), "Precondition: option must not be empty.");

        this.OnElement(refId, id => this.Driver.SelectOption(id, option));
        return Done("selected " + TextFormat.Quote(option) + " in", refId);
    }

    /// <summary>
    /// Expands or collapses an element (ExpandCollapsePattern).
    /// </summary>
    /// <param name="refId">The element.</param>
    /// <param name="expand"><see langword="true"/> to expand, <see langword="false"/> to collapse.</param>
    /// <returns>The result.</returns>
    public CommandResult Expand(RefId refId, bool expand)
    {
        this.OnElement(refId, id => { if (expand) { this.Driver.Expand(id); } else { this.Driver.Collapse(id); } });
        return Done(expand ? "expanded" : "collapsed", refId);
    }

    /// <summary>
    /// Invokes an element (InvokePattern) without falling back to other patterns.
    /// </summary>
    /// <param name="refId">The element.</param>
    /// <returns>The result.</returns>
    public CommandResult Invoke(RefId refId)
    {
        this.OnElement(refId, this.Driver.Invoke);
        return Done("invoked", refId);
    }

    /// <summary>
    /// Sets keyboard focus to an element.
    /// </summary>
    /// <param name="refId">The element.</param>
    /// <returns>The result.</returns>
    public CommandResult Focus(RefId refId)
    {
        this.OnElement(refId, this.Driver.Focus);
        return Done("focused", refId);
    }

    /// <summary>
    /// Scrolls an element into view, or scrolls a container by pages.
    /// </summary>
    /// <param name="refId">The element or container.</param>
    /// <param name="direction">The direction, or <see langword="null"/> to scroll the element into view.</param>
    /// <param name="pages">The number of pages.</param>
    /// <returns>The result.</returns>
    public CommandResult Scroll(RefId refId, ScrollDirection? direction, int pages)
    {
        System.Diagnostics.Debug.Assert(pages is > 0 and <= 1000, "Precondition: pages must be between 1 and 1000.");

        this.OnElement(refId, id => { if (direction is ScrollDirection d) { this.Driver.Scroll(id, d, pages); } else { this.Driver.ScrollIntoView(id); } });
        return Done("scrolled", refId);
    }

    /// <summary>
    /// Presses a key chord with the real keyboard on an element or the focused element.
    /// </summary>
    /// <param name="refId">The element, or <see langword="null"/> for the focused element.</param>
    /// <param name="keys">The key chord, e.g. <c>Enter</c> or <c>Control+S</c>.</param>
    /// <returns>The result.</returns>
    public CommandResult Press(RefId? refId, string keys)
    {
        System.Diagnostics.Debug.Assert(!string.IsNullOrWhiteSpace(keys), "Precondition: keys must not be empty.");

        this.RequireTarget();
        this.OnOptionalElement(refId, id => this.Driver.PressKeys(id, keys));
        return new CommandResult("pressed " + keys + "\n");
    }

    /// <summary>
    /// Types text with the real keyboard on an element or the focused element.
    /// </summary>
    /// <param name="refId">The element, or <see langword="null"/> for the focused element.</param>
    /// <param name="text">The text.</param>
    /// <returns>The result.</returns>
    public CommandResult Type(RefId? refId, string text)
    {
        System.Diagnostics.Debug.Assert(text != null, "Precondition: text must not be null.");

        this.RequireTarget();
        this.OnOptionalElement(refId, id => this.Driver.TypeText(id, text));
        return new CommandResult(string.Format(CultureInfo.InvariantCulture, "typed {0} characters\n", text.Length));
    }

    private static CommandResult Done(string verb, RefId refId, string suffix = "")
        => new(string.Format(CultureInfo.InvariantCulture, "{0} @{1}{2}\n", verb, refId, suffix));

    private void OnOptionalElement(RefId? refId, System.Action<string?> operation)
    {
        if (refId is RefId r)
        {
            this.OnElement(r, id => operation(id));
        }
        else
        {
            operation(null);
        }
    }

    private string ClickByPattern(RefId refId, string id)
    {
        var patterns = this.Driver.GetPatterns(id);
        if (patterns.Contains(PatternKind.Invoke))
        {
            this.Driver.Invoke(id);
            return "invoked";
        }

        if (patterns.Contains(PatternKind.Toggle))
        {
            this.Driver.Toggle(id);
            return "toggled";
        }

        if (patterns.Contains(PatternKind.SelectionItem))
        {
            this.Driver.Select(id);
            return "selected";
        }

        if (patterns.Contains(PatternKind.ExpandCollapse))
        {
            var expanded = this.Driver.Capture(id, 0).States.HasFlag(ElementState.Expanded);
            if (expanded) { this.Driver.Collapse(id); } else { this.Driver.Expand(id); }
            return expanded ? "collapsed" : "expanded";
        }

        throw new AgentWpfException(ErrorCode.ActionFailed, this.Describe(refId, id) + " supports no click pattern.", InputHint);
    }

    private void ToggleTo(RefId refId, string id, ToggleState desired)
    {
        for (var i = 0; i < MaxToggles; i++)
        {
            if (this.Driver.GetToggleState(id) == desired)
            {
                return;
            }

            this.Driver.Toggle(id);
        }

        if (this.Driver.GetToggleState(id) != desired)
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, this.Describe(refId, id) + " did not reach the requested state.", "the app may reject the change; take a snapshot");
        }
    }
}
