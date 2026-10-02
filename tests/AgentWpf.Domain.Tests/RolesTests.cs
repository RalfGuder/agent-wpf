using AgentWpf.Domain;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Domain.Tests;

[TestFixture]
public sealed class RolesTests
{
    [TestCase(Role.Button, "button")]
    [TestCase(Role.CheckBox, "checkbox")]
    [TestCase(Role.DataItem, "dataitem")]
    [TestCase(Role.Edit, "edit")]
    public void Role_names_are_lower_case_control_type_names(Role role, string name)
    {
        role.ToRoleName().ShouldBe(name);
    }

    [TestCase(Role.Button, true)]
    [TestCase(Role.Edit, true)]
    [TestCase(Role.ComboBox, true)]
    [TestCase(Role.CheckBox, true)]
    [TestCase(Role.MenuItem, true)]
    [TestCase(Role.TreeItem, true)]
    [TestCase(Role.Text, false)]
    [TestCase(Role.Pane, false)]
    [TestCase(Role.Group, false)]
    public void Interactive_roles_are_classified(Role role, bool interactive)
    {
        role.IsInteractive().ShouldBe(interactive);
    }

    [TestCase(Role.Window, true)]
    [TestCase(Role.Pane, true)]
    [TestCase(Role.Group, true)]
    [TestCase(Role.Custom, true)]
    [TestCase(Role.Button, false)]
    public void Structural_roles_are_classified(Role role, bool structural)
    {
        role.IsStructural().ShouldBe(structural);
    }
}
