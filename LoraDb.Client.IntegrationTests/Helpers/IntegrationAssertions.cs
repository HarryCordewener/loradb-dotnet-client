using System.Text.Json;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.IntegrationTests.Helpers;

internal static class IntegrationAssertions
{
    public static async Task AssertSingleIntegerResult(LoraDbQueryResult result, string column, int expected)
    {
        await AssertRowCount(result, 1);
        await Assert.That(GetRowColumn(result, 0, column).GetInt32()).IsEqualTo(expected);
    }

    public static async Task AssertRowCount(LoraDbQueryResult result, int expected)
    {
        var rows = result.Root.GetProperty("rows");
        await Assert.That(rows.GetArrayLength()).IsEqualTo(expected);
    }

    public static JsonElement GetRowColumn(LoraDbQueryResult result, int rowIndex, string column)
    {
        var rows = result.Root.GetProperty("rows");

        if (rowIndex < 0 || rowIndex >= rows.GetArrayLength())
            throw new ArgumentOutOfRangeException(nameof(rowIndex), rowIndex, $"Row index {rowIndex} is outside the result set.");

        var row = rows[rowIndex];
        if (!row.TryGetProperty(column, out var value))
            throw new KeyNotFoundException($"Column '{column}' was not found in row {rowIndex}.");

        return value;
    }
}
