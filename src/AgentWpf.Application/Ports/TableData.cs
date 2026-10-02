using System.Collections.Generic;

namespace AgentWpf.Application.Ports;

/// <summary>
/// A slice of rows read from a grid or table.
/// </summary>
/// <param name="Columns">The column headers.</param>
/// <param name="Rows">The requested rows.</param>
/// <param name="TotalRows">The total number of rows, including unrealized ones.</param>
public sealed record TableData(IReadOnlyList<string> Columns, IReadOnlyList<TableRow> Rows, int TotalRows);

/// <summary>
/// One row of a <see cref="TableData"/>.
/// </summary>
/// <param name="Index">The zero-based row index.</param>
/// <param name="Cells">The cell texts in column order.</param>
public sealed record TableRow(int Index, IReadOnlyList<string> Cells);
