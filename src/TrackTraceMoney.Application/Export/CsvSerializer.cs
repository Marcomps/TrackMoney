namespace TrackTraceMoney.Application.Export;

/// <summary>
/// Pure RFC 4180 CSV mechanics only — field/record escaping and joining, zero domain knowledge.
/// Callers (e.g. the App layer's <c>TransactionCsvRowBuilder</c>) are responsible for deciding what
/// the header/row values actually are; this type only knows how to make them safe CSV text.
/// A static pure function, not an injected service — mirrors this codebase's existing precedent for
/// pure static Application-layer helpers (<see cref="TrackTraceMoney.Application.Reporting.AccountCurrencyMapBuilder"/>).
/// </summary>
public static class CsvSerializer
{
    /// <summary>
    /// Builds a full CSV document (header row + one row per entry in <paramref name="rows"/>),
    /// using CRLF line endings per RFC 4180, including after the final row.
    /// </summary>
    public static string Write(IReadOnlyList<string> headers, IEnumerable<string?[]> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new System.Text.StringBuilder();

        WriteRecord(builder, headers);
        foreach (var row in rows)
            WriteRecord(builder, row);

        return builder.ToString();
    }

    private static void WriteRecord(System.Text.StringBuilder builder, IReadOnlyList<string?> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
                builder.Append(',');

            builder.Append(EscapeField(fields[i]));
        }

        builder.Append("\r\n");
    }

    /// <summary>
    /// RFC 4180 §2.6/§2.7: a field is quote-wrapped only when it contains a comma, a double quote, or
    /// a line break (CR or LF); embedded double quotes are doubled. Fields that need none of this are
    /// left bare, matching the RFC's "quotes optional" rule and keeping the common case readable.
    /// </summary>
    private static string EscapeField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        var needsQuoting = field.Contains(',') || field.Contains('"') || field.Contains('\r') || field.Contains('\n');
        if (!needsQuoting)
            return field;

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
