using System.Diagnostics;
using System.Globalization;

namespace AgentWpf.Domain;

/// <summary>
/// The semantic role of an element, mirroring the UI Automation control types.
/// </summary>
public enum Role
{
    /// <summary>The control type is not known.</summary>
    Unknown,

    /// <summary>An application bar.</summary>
    AppBar,

    /// <summary>A push button.</summary>
    Button,

    /// <summary>A calendar.</summary>
    Calendar,

    /// <summary>A check box.</summary>
    CheckBox,

    /// <summary>A combo box.</summary>
    ComboBox,

    /// <summary>A custom control.</summary>
    Custom,

    /// <summary>A data grid.</summary>
    DataGrid,

    /// <summary>A row or item inside a data grid.</summary>
    DataItem,

    /// <summary>A document.</summary>
    Document,

    /// <summary>An editable text field.</summary>
    Edit,

    /// <summary>A grouping container.</summary>
    Group,

    /// <summary>A header.</summary>
    Header,

    /// <summary>A header item, e.g. a column header.</summary>
    HeaderItem,

    /// <summary>A hyperlink.</summary>
    Hyperlink,

    /// <summary>An image.</summary>
    Image,

    /// <summary>A list.</summary>
    List,

    /// <summary>An item of a list.</summary>
    ListItem,

    /// <summary>A menu.</summary>
    Menu,

    /// <summary>A menu bar.</summary>
    MenuBar,

    /// <summary>A menu item.</summary>
    MenuItem,

    /// <summary>A pane.</summary>
    Pane,

    /// <summary>A progress bar.</summary>
    ProgressBar,

    /// <summary>A radio button.</summary>
    RadioButton,

    /// <summary>A scroll bar.</summary>
    ScrollBar,

    /// <summary>A semantic zoom control.</summary>
    SemanticZoom,

    /// <summary>A separator.</summary>
    Separator,

    /// <summary>A slider.</summary>
    Slider,

    /// <summary>A spinner.</summary>
    Spinner,

    /// <summary>A split button.</summary>
    SplitButton,

    /// <summary>A status bar.</summary>
    StatusBar,

    /// <summary>A tab control.</summary>
    Tab,

    /// <summary>A tab item.</summary>
    TabItem,

    /// <summary>A table.</summary>
    Table,

    /// <summary>Static text.</summary>
    Text,

    /// <summary>A thumb, e.g. of a scroll bar.</summary>
    Thumb,

    /// <summary>A title bar.</summary>
    TitleBar,

    /// <summary>A tool bar.</summary>
    ToolBar,

    /// <summary>A tool tip.</summary>
    ToolTip,

    /// <summary>A tree.</summary>
    Tree,

    /// <summary>A tree item.</summary>
    TreeItem,

    /// <summary>A top-level or child window.</summary>
    Window,
}

/// <summary>
/// Classification helpers for <see cref="Role"/>.
/// </summary>
public static class RoleExtensions
{
    /// <summary>
    /// Returns the lower-case role name used in snapshots, e.g. <c>checkbox</c>.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The lower-case role name.</returns>
    public static string ToRoleName(this Role role)
    {
        Debug.Assert(System.Enum.IsDefined(role), "Precondition: role must be a defined Role.");

        var name = role.ToString().ToLower(CultureInfo.InvariantCulture);

        Debug.Assert(name.Length > 0, "Postcondition: role name must not be empty.");
        return name;
    }

    /// <summary>
    /// Determines whether agents typically act on elements of this role (used by <c>snapshot -i</c>).
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns><see langword="true"/> for actionable roles such as buttons and edits.</returns>
    public static bool IsInteractive(this Role role) => role switch
    {
        Role.Button or Role.CheckBox or Role.ComboBox or Role.Edit or Role.Hyperlink
            or Role.ListItem or Role.MenuItem or Role.RadioButton or Role.Slider or Role.Spinner
            or Role.SplitButton or Role.TabItem or Role.TreeItem or Role.DataItem or Role.HeaderItem
            or Role.Document or Role.Calendar => true,
        _ => false,
    };

    /// <summary>
    /// Determines whether the role is a pure layout container that compact snapshots may collapse.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns><see langword="true"/> for windows, panes, groups and custom containers.</returns>
    public static bool IsStructural(this Role role) => role is Role.Window or Role.Pane or Role.Group or Role.Custom;
}
