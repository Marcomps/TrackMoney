using System.Globalization;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Export;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// Builds the History export CSV (README §41, slice 1: CSV only) from the screen's already-filtered
/// <see cref="HistoryEntryItem"/> list — i.e. exactly what's currently on screen, not a fresh
/// unfiltered pass. Deliberately App-layer, not Application-layer: <see cref="HistoryEntryItem"/> is
/// an App-layer model with fields the Application layer must never reference (one-way reference
/// direction — see CLAUDE.md), and this type re-derives no domain switch of its own, it only reads
/// fields <see cref="HistoryEntryItem.FromDomain"/> already resolved. RFC 4180 mechanics are delegated
/// entirely to <see cref="CsvSerializer"/>; this type only decides which strings go in which column.
/// </summary>
public static class TransactionCsvRowBuilder
{
    /// <summary>
    /// Full CSV document text (localized header row + one row per entry), ready to write to a file.
    /// </summary>
    public static string Build(
        IReadOnlyList<HistoryEntryItem> entries,
        IReadOnlyDictionary<Guid, string> categoryNames,
        IReadOnlyDictionary<Guid, string> personNames,
        IReadOnlyDictionary<Guid, CurrencyCode> accountCurrencies)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(categoryNames);
        ArgumentNullException.ThrowIfNull(personNames);
        ArgumentNullException.ThrowIfNull(accountCurrencies);

        string[] headers =
        [
            AppResources.Export_Header_Date,
            AppResources.Export_Header_Type,
            AppResources.Export_Header_Amount,
            AppResources.Export_Header_Currency,
            AppResources.Export_Header_Account,
            AppResources.Export_Header_Category,
            AppResources.Export_Header_Person,
            AppResources.Export_Header_Description
        ];

        var rows = entries.Select(entry => BuildRow(entry, categoryNames, personNames, accountCurrencies));

        return CsvSerializer.Write(headers, rows);
    }

    /// <summary>
    /// One row per <see cref="HistoryEntryItem"/>, same column shape regardless of
    /// <see cref="HistoryEntryItem.Type"/> — inapplicable columns are blank, never omitted (Decision 3
    /// of the slice spec). Date/Amount are culture-invariant regardless of device language; only the
    /// header row (built in <see cref="Build"/>) is localized.
    /// </summary>
    private static string?[] BuildRow(
        HistoryEntryItem entry,
        IReadOnlyDictionary<Guid, string> categoryNames,
        IReadOnlyDictionary<Guid, string> personNames,
        IReadOnlyDictionary<Guid, CurrencyCode> accountCurrencies)
    {
        var currency = entry.AccountIds.Count > 0 && accountCurrencies.TryGetValue(entry.AccountIds[0], out var currencyCode)
            ? currencyCode.ToString()
            : string.Empty;

        var category = entry.CategoryId is { } categoryId && categoryNames.TryGetValue(categoryId, out var categoryName)
            ? categoryName
            : string.Empty;

        var person = string.Join(
            "; ",
            entry.PersonIds
                .Select(id => personNames.TryGetValue(id, out var name) ? name : null)
                .Where(name => name is not null));

        return
        [
            entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TransactionTypeToLabelConverter.GetDisplayName(entry.Type),
            entry.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            currency,
            entry.AccountLabel,
            category,
            person,
            entry.Description ?? string.Empty
        ];
    }
}
