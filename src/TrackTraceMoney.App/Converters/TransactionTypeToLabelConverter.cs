using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;

namespace TrackTraceMoney.App.Converters;

public sealed class TransactionTypeToLabelConverter : EnumToLabelConverter<TransactionType>
{
    protected override string GetLabel(TransactionType value) => GetDisplayName(value);

    /// <summary>
    /// Public static entry point for callers that need the label without going through
    /// <see cref="IValueConverter"/> (e.g. <c>HistoryViewModel</c> building its type-filter dropdown
    /// options) — mirrors the pattern already established by
    /// <see cref="SystemCategoryKeyToLabelConverter.GetDisplayName(Domain.Categories.Category)"/>.
    /// </summary>
    public static string GetDisplayName(TransactionType value) => value switch
    {
        TransactionType.Expense => AppResources.TransactionType_Expense,
        TransactionType.Income => AppResources.TransactionType_Income,
        TransactionType.Transfer => AppResources.TransactionType_Transfer,
        TransactionType.CreditCardPurchase => AppResources.TransactionType_CreditCardPurchase,
        _ => string.Empty
    };
}
