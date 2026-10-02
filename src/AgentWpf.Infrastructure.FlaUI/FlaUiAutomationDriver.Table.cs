using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using AgentWpf.Application.Ports;
using AgentWpf.Domain;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Exceptions;
using FlaUI.Core.Patterns;

namespace AgentWpf.Infrastructure.FlaUI;

/// <content>
/// Reading grids (GridPattern/TablePattern), realizing virtualized rows on demand.
/// </content>
public sealed partial class FlaUiAutomationDriver
{
    // Wider tables are cut; agents can scope with a smaller grid.
    private const int MaxColumns = 100;

    /// <inheritdoc />
    public TableData ReadTable(string runtimeId, int firstRow, int rowCount)
    {
        Debug.Assert(firstRow >= 0, "Precondition: firstRow must not be negative.");
        Debug.Assert(rowCount is > 0 and <= 500, "Precondition: rowCount must be between 1 and 500.");

        var table = this.On(runtimeId, e =>
        {
            var grid = e.Patterns.Grid.PatternOrDefault
                ?? throw new AgentWpfException(ErrorCode.ActionFailed, "The element is not a grid.", "use table on a datagrid or table element from the snapshot");
            var total = grid.RowCount.ValueOrDefault;
            var columns = Math.Min(grid.ColumnCount.ValueOrDefault, MaxColumns);
            var rows = new List<TableRow>();
            for (var row = firstRow; row < Math.Min(total, firstRow + rowCount); row++)
            {
                rows.Add(new TableRow(row, Enumerable.Range(0, columns).Select(column => CellText(e, grid, row, column, total)).ToList()));
            }

            return new TableData(Headers(e, columns), rows, total);
        });

        Debug.Assert(table.Rows.Count <= rowCount, "Postcondition: never more rows than requested.");
        return table;
    }

    private static List<string> Headers(AutomationElement grid, int columns)
    {
        var headers = grid.Patterns.Table.PatternOrDefault?.ColumnHeaders.ValueOrDefault ?? [];
        return Enumerable.Range(0, columns)
            .Select(i => i < headers.Length && !string.IsNullOrEmpty(headers[i].Name) ? headers[i].Name : "c" + i.ToString(CultureInfo.InvariantCulture))
            .ToList();
    }

    private static string CellText(AutomationElement gridElement, IGridPattern grid, int row, int column, int total)
    {
        var cell = TryGetItem(grid, row, column);
        if (cell is null && gridElement.Patterns.Scroll.PatternOrDefault is { } scroll && total > 1)
        {
            // Bring the row into the realized range, then retry once.
            scroll.SetScrollPercent(-1, Math.Clamp(row * 100.0 / (total - 1), 0, 100));
            cell = TryGetItem(grid, row, column);
        }

        if (cell is null)
        {
            return string.Empty;
        }

        var value = cell.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        return !string.IsNullOrEmpty(cell.Name) ? cell.Name : cell.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text))?.Name ?? string.Empty;
    }

    private static AutomationElement? TryGetItem(IGridPattern grid, int row, int column)
    {
        try
        {
            return grid.GetItem(row, column);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or ElementNotAvailableException or InvalidOperationException)
        {
            return null;
        }
    }
}
