using TrackTraceMoney.Application.Export;

namespace TrackTraceMoney.Application.Tests.Export;

/// <summary>
/// README §41's CSV export slice (slice 1: CSV only). Covers RFC 4180 field/record mechanics only —
/// <see cref="CsvSerializer"/> has zero domain knowledge, so these tests feed literal strings and
/// assert exact CSV text, no <c>Transaction</c>/repository involved. Plain xUnit, no App/MAUI project
/// reference required to run.
/// </summary>
public sealed class CsvSerializerTests
{
    [Fact]
    public void Write_HeaderOnly_NoRows_ProducesHeaderRowWithCrlf()
    {
        var csv = CsvSerializer.Write(["Date", "Amount"], []);

        Assert.Equal("Date,Amount\r\n", csv);
    }

    [Fact]
    public void Write_SimpleFields_NoQuotingNeeded()
    {
        var csv = CsvSerializer.Write(
            ["Date", "Type", "Amount"],
            [["2026-01-01", "Expense", "12.50"]]);

        Assert.Equal("Date,Type,Amount\r\n2026-01-01,Expense,12.50\r\n", csv);
    }

    [Fact]
    public void Write_FieldContainingComma_IsQuoted()
    {
        var csv = CsvSerializer.Write(
            ["Description"],
            [["Groceries, snacks, and drinks"]]);

        Assert.Equal("Description\r\n\"Groceries, snacks, and drinks\"\r\n", csv);
    }

    [Fact]
    public void Write_FieldContainingDoubleQuote_IsQuotedAndQuoteIsDoubled()
    {
        var csv = CsvSerializer.Write(
            ["Description"],
            [["She said \"hello\" to me"]]);

        Assert.Equal("Description\r\n\"She said \"\"hello\"\" to me\"\r\n", csv);
    }

    [Fact]
    public void Write_FieldContainingEmbeddedNewline_IsQuotedAndNewlinePreservedInside()
    {
        var csv = CsvSerializer.Write(
            ["Description"],
            [["Line one\nLine two"]]);

        Assert.Equal("Description\r\n\"Line one\nLine two\"\r\n", csv);
    }

    [Fact]
    public void Write_FieldContainingEmbeddedCarriageReturn_IsQuoted()
    {
        var csv = CsvSerializer.Write(
            ["Description"],
            [["Line one\rLine two"]]);

        Assert.Equal("Description\r\n\"Line one\rLine two\"\r\n", csv);
    }

    [Fact]
    public void Write_FieldContainingCommaAndQuoteAndNewline_QuotesAndDoublesQuotesTogether()
    {
        var csv = CsvSerializer.Write(
            ["Description"],
            [["A, \"quoted\" phrase\nwith a newline"]]);

        Assert.Equal("Description\r\n\"A, \"\"quoted\"\" phrase\nwith a newline\"\r\n", csv);
    }

    [Fact]
    public void Write_NullField_RendersAsEmptyCell()
    {
        var csv = CsvSerializer.Write(
            ["Category"],
            [[null]]);

        Assert.Equal("Category\r\n\r\n", csv);
    }

    [Fact]
    public void Write_EmptyStringField_RendersAsEmptyCellUnquoted()
    {
        var csv = CsvSerializer.Write(
            ["Category"],
            [[""]]);

        Assert.Equal("Category\r\n\r\n", csv);
    }

    [Fact]
    public void Write_MultipleRows_AreJoinedInOrder()
    {
        var csv = CsvSerializer.Write(
            ["Date", "Amount"],
            [
                ["2026-01-01", "10.00"],
                ["2026-01-02", "20.00"]
            ]);

        Assert.Equal("Date,Amount\r\n2026-01-01,10.00\r\n2026-01-02,20.00\r\n", csv);
    }

    [Fact]
    public void Write_RowWithFewerFieldsThanHeaders_DoesNotThrow_ProducesShorterRecord()
    {
        // CsvSerializer has no domain knowledge of "expected column count" — callers are responsible
        // for always passing arrays the same length as the header. This documents that the serializer
        // itself does not enforce that, rather than leaving it unspecified.
        var csv = CsvSerializer.Write(
            ["Date", "Amount", "Currency"],
            [["2026-01-01", "10.00"]]);

        Assert.Equal("Date,Amount,Currency\r\n2026-01-01,10.00\r\n", csv);
    }

    [Fact]
    public void Write_FieldWithOnlyWhitespace_IsNotQuoted()
    {
        var csv = CsvSerializer.Write(
            ["Description"],
            [["   "]]);

        Assert.Equal("Description\r\n   \r\n", csv);
    }
}
