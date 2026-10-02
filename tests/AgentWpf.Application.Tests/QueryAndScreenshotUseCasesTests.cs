using AgentWpf.Application.Ports;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Application.Tests;

[TestFixture]
public sealed class QueryAndScreenshotUseCasesTests
{
    [Test]
    public void Get_text_returns_name()
    {
        var h = new Harness();
        var r = h.RefFor("s");
        h.Driver.Setup(d => d.Capture("s", 0)).Returns(Harness.Node("s", Role.Text, "Fertig"));

        new QueryUseCases(h.Session, h.Driver.Object).GetText(r).Text.ShouldBe("Fertig\n");
    }

    [Test]
    public void Get_value_returns_value_or_empty()
    {
        var h = new Harness();
        var r = h.RefFor("e");
        h.Driver.Setup(d => d.Capture("e", 0)).Returns(Harness.Node("e", Role.Edit, "Pfad", value: "C:\\x"));

        new QueryUseCases(h.Session, h.Driver.Object).GetValue(r).Text.ShouldBe("C:\\x\n");
    }

    [Test]
    public void Table_renders_header_and_indexed_rows()
    {
        var h = new Harness();
        var r = h.RefFor("g");
        h.Driver.Setup(d => d.ReadTable("g", 10, 2)).Returns(new TableData(
            ["Nr", "Name"],
            [new TableRow(10, ["11", "Anna"]), new TableRow(11, ["12", "Bernd"])],
            TotalRows: 1000));

        var result = new QueryUseCases(h.Session, h.Driver.Object).Table(r, 10, 11);

        result.Text.ShouldBe(
            "# rows 10-11 of 1000; columns: Nr | Name\n" +
            "[10] 11 | Anna\n" +
            "[11] 12 | Bernd\n");
        result.Data.ShouldBeOfType<TableData>();
    }

    [Test]
    public void Table_rejects_inverted_range()
    {
        var h = new Harness();
        var r = h.RefFor("g");

        Should.Throw<AgentWpfException>(() => new QueryUseCases(h.Session, h.Driver.Object).Table(r, 5, 4)).Code.ShouldBe(ErrorCode.Usage);
    }

    [Test]
    public void Screenshot_captures_active_window()
    {
        var h = new Harness();
        h.Driver.Setup(d => d.GetWindowHandle("w1")).Returns(1234);
        h.Capture.Setup(c => c.SavePng(1234, null, "C:\\out\\a.png")).Returns((800, 600));

        var result = new ScreenshotUseCase(h.Session, h.Driver.Object, h.Capture.Object).Execute("C:\\out\\a.png", null);

        result.Text.ShouldBe("saved C:\\out\\a.png (800x600)\n");
    }

    [Test]
    public void Screenshot_of_element_crops_to_its_bounds()
    {
        var h = new Harness();
        var r = h.RefFor("b");
        var bounds = new Bounds(10, 20, 30, 40);
        h.Driver.Setup(d => d.GetWindowHandle("w1")).Returns(1234);
        h.Driver.Setup(d => d.Capture("b", 0)).Returns(new ElementSnapshot { RuntimeId = "b", Role = Role.Button, Bounds = bounds });
        h.Capture.Setup(c => c.SavePng(1234, bounds, "C:\\b.png")).Returns((30, 40));

        new ScreenshotUseCase(h.Session, h.Driver.Object, h.Capture.Object).Execute("C:\\b.png", r).Text.ShouldContain("30x40");
    }
}
