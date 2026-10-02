using System;
using System.Globalization;
using System.Linq;
using System.Text;
using AgentWpf.Application.Ports;
using AgentWpf.Application.Sessions;
using AgentWpf.Domain;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// Reading element data: <c>get text</c>, <c>get value</c> and <c>table</c>.
/// </summary>
public sealed class QueryUseCases : SessionUseCase
{
    /// <summary>
    /// The maximum number of rows returned by one <c>table</c> call.
    /// </summary>
    public const int MaxTableRows = 500;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUseCases"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="driver">The automation driver.</param>
    public QueryUseCases(AgentSession session, IAutomationDriver driver)
        : base(session, driver)
    {
    }

    /// <summary>
    /// Returns the accessible name of an element.
    /// </summary>
    /// <param name="refId">The element.</param>
    /// <returns>The name, unescaped, followed by a newline.</returns>
    public CommandResult GetText(RefId refId)
    {
        var name = this.OnElement(refId, id => this.Driver.Capture(id, 0).Name) ?? string.Empty;
        return new CommandResult(name + "\n", name);
    }

    /// <summary>
    /// Returns the value of an element.
    /// </summary>
    /// <param name="refId">The element.</param>
    /// <returns>The value, unescaped, followed by a newline.</returns>
    public CommandResult GetValue(RefId refId)
    {
        var value = this.OnElement(refId, id => this.Driver.Capture(id, 0).Value) ?? string.Empty;
        return new CommandResult(value + "\n", value);
    }

    /// <summary>
    /// Reads rows of a grid or table, realizing virtualized rows.
    /// </summary>
    /// <param name="refId">The grid.</param>
    /// <param name="firstRow">The zero-based first row.</param>
    /// <param name="lastRow">The zero-based last row, inclusive.</param>
    /// <returns>The rows as text (<c>[index] cell | cell</c>) and as <see cref="TableData"/>.</returns>
    public CommandResult Table(RefId refId, int firstRow, int lastRow)
    {
        if (firstRow < 0 || lastRow < firstRow || lastRow - firstRow + 1 > MaxTableRows)
        {
            throw new AgentWpfException(
                ErrorCode.Usage,
                string.Format(CultureInfo.InvariantCulture, "Invalid row range {0}-{1}.", firstRow, lastRow),
                string.Format(CultureInfo.InvariantCulture, "use --rows <first>-<last> with 0 <= first <= last and at most {0} rows", MaxTableRows));
        }

        var table = this.OnElement(refId, id => this.Driver.ReadTable(id, firstRow, lastRow - firstRow + 1));

        System.Diagnostics.Debug.Assert(table.Rows.Count <= MaxTableRows, "Postcondition: driver must respect the row count.");
        return new CommandResult(RenderTable(table, firstRow, lastRow), table);
    }

    private static string RenderTable(TableData table, int firstRow, int lastRow)
    {
        var text = new StringBuilder();
        if (table.Rows.Count == 0)
        {
            return string.Format(CultureInfo.InvariantCulture, "# no rows in range {0}-{1} of {2}\n", firstRow, lastRow, table.TotalRows);
        }

        text.Append(CultureInfo.InvariantCulture, $"# rows {table.Rows[0].Index}-{table.Rows[^1].Index} of {table.TotalRows}; columns: {string.Join(" | ", table.Columns.Select(Cell))}\n");
        foreach (var row in table.Rows)
        {
            text.Append(CultureInfo.InvariantCulture, $"[{row.Index}] {string.Join(" | ", row.Cells.Select(Cell))}\n");
        }

        return text.ToString();
    }

    private static string Cell(string text) => TextFormat.Escape(text).Replace("|", "\\|", StringComparison.Ordinal);
}
