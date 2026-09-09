using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// A picker-friendly (id, label) pair. <see cref="Currency"/> is populated only for account options
/// so callers can validate cross-currency operations (README §6/§10) client-side before calling the
/// Application layer — it is left null for category/person options where it doesn't apply.
/// <see cref="IsCreditAccount"/> is used only by <c>AddTransactionViewModel.PaymentAccounts</c> (the
/// Expense flow's Account picker) to route to <c>RecordCreditCardPurchaseAsync</c> instead of
/// <c>RecordExpenseAsync</c> when the selected option is a credit card.
/// </summary>
public sealed record NamedOption(Guid Id, string Name, CurrencyCode? Currency = null, bool IsCreditAccount = false)
{
    public override string ToString() => Name;
}
