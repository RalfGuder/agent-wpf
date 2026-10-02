using System;
using System.CommandLine;
using AgentWpf.Application.Ports;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;

namespace AgentWpf.Cli;

/// <content>
/// The commands that act on elements.
/// </content>
internal sealed partial class CommandLineApp
{
    private void AddInteractionCommands(RootCommand root, Invocation inv)
    {
        var input = new Option<bool>("--input") { Description = "Use the real mouse and keyboard instead of UI Automation patterns." };
        var right = new Option<bool>("--right") { Description = "Right click (needs --input)." };

        root.Subcommands.Add(RefCommand("click", "Click an element (Invoke, Toggle, Select or Expand pattern).", inv, [input, right], (pr, r) =>
            this.Services.Interaction.Click(r, new ClickOptions { Input = pr.GetValue(input), Button = pr.GetValue(right) ? MouseButton.Right : MouseButton.Left })));
        root.Subcommands.Add(RefCommand("dblclick", "Double-click an element with the real mouse.", inv, [], (_, r) =>
            this.Services.Interaction.Click(r, new ClickOptions { Input = true, ClickCount = 2 })));
        root.Subcommands.Add(RefCommand("invoke", "Invoke an element (InvokePattern only).", inv, [], (_, r) => this.Services.Interaction.Invoke(r)));
        root.Subcommands.Add(RefCommand("check", "Check a check box or select a radio button.", inv, [], (_, r) => this.Services.Interaction.SetChecked(r, true)));
        root.Subcommands.Add(RefCommand("uncheck", "Uncheck a check box.", inv, [], (_, r) => this.Services.Interaction.SetChecked(r, false)));
        root.Subcommands.Add(RefCommand("expand", "Expand a tree item, combo box or menu.", inv, [], (_, r) => this.Services.Interaction.Expand(r, true)));
        root.Subcommands.Add(RefCommand("collapse", "Collapse a tree item, combo box or menu.", inv, [], (_, r) => this.Services.Interaction.Expand(r, false)));
        root.Subcommands.Add(RefCommand("focus", "Set keyboard focus to an element.", inv, [], (_, r) => this.Services.Interaction.Focus(r)));
        root.Subcommands.Add(RefCommand("scrollintoview", "Scroll an element into view.", inv, [], (_, r) => this.Services.Interaction.Scroll(r, null, 1)));

        var fillText = new Argument<string>("text") { Description = "The new value." };
        root.Subcommands.Add(RefCommand("fill", "Replace the value of a text field.", inv, [fillText, input], (pr, r) =>
            this.Services.Interaction.Fill(r, pr.GetValue(fillText)!, pr.GetValue(input))));

        var typeText = new Argument<string>("text") { Description = "The text to type." };
        root.Subcommands.Add(RefCommand("type", "Type text with the real keyboard into an element.", inv, [typeText], (pr, r) =>
            this.Services.Interaction.Type(r, pr.GetValue(typeText)!)));

        var option = new Argument<string>("option") { Description = "The visible option text." };
        root.Subcommands.Add(RefCommand("select", "Select an option in a combo box or list.", inv, [option], (pr, r) =>
            this.Services.Interaction.Select(r, pr.GetValue(option)!)));

        var direction = new Argument<string?>("direction") { Description = "up, down, left or right; omit to scroll into view.", Arity = ArgumentArity.ZeroOrOne };
        var pages = new Option<int>("--pages") { Description = "Number of pages.", DefaultValueFactory = _ => 1 };
        root.Subcommands.Add(RefCommand("scroll", "Scroll a container, or scroll an element into view.", inv, [direction, pages], (pr, r) =>
            this.Services.Interaction.Scroll(r, ParseDirection(pr.GetValue(direction)), pr.GetValue(pages))));

        root.Subcommands.Add(this.PressCommand(inv));
    }

    private Command PressCommand(Invocation inv)
    {
        var keys = new Argument<string>("keys") { Description = "Key chord, e.g. Enter, Escape, Control+S, Alt+F4." };
        var target = new Option<string?>("--target") { Description = "Ref of the element that receives the keys; default is the focused element." };
        var press = new Command("press", "Press a key chord with the real keyboard.") { keys, target };
        press.SetAction(pr => inv.Run(() => this.Services.Interaction.Press(ParseOptionalRef(pr.GetValue(target)), pr.GetValue(keys)!)));
        return press;
    }

    private static Command RefCommand(string name, string description, Invocation inv, Symbol[] extras, Func<ParseResult, RefId, CommandResult> action)
    {
        var refArgument = new Argument<string>("ref") { Description = "Element ref from the latest snapshot, e.g. @e3." };
        var command = new Command(name, description) { refArgument };
        foreach (var extra in extras)
        {
            if (extra is Argument argument)
            {
                command.Arguments.Add(argument);
            }
            else
            {
                command.Options.Add((Option)extra);
            }
        }

        command.SetAction(pr => inv.Run(() => action(pr, ParseRef(pr.GetValue(refArgument)))));
        return command;
    }

    private static ScrollDirection? ParseDirection(string? text) => text switch
    {
        null => null,
        _ when Enum.TryParse<ScrollDirection>(text, ignoreCase: true, out var d) => d,
        _ => throw new AgentWpfException(ErrorCode.Usage, "'" + text + "' is not a scroll direction.", "use up, down, left or right"),
    };
}
