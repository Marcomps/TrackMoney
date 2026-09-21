using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.TransactionPresets;

/// <summary>
/// A user-defined, named, icon-bearing shortcut that pre-fills an ordinary Expense/Income/Transfer entry
/// (Transaction Type Customization slice spec, Half B, §B.3/§B.7). This is deliberately NOT a new
/// <c>TransactionType</c> and NOT a <c>Transaction</c> subtype -- it never gets saved as a transaction row
/// itself, only used by the App layer to pre-fill <c>AddTransactionViewModel</c>'s fields before the user
/// hits Save. <c>TransactionEntryService</c> needs zero changes for this entity to exist (spec's core
/// design constraint).
/// </summary>
public sealed class TransactionPreset : Entity
{
    public string Name { get; private set; } = null!;

    public string? Icon { get; private set; }

    public TransactionPresetBaseType BaseType { get; private set; }

    /// <summary>Null for a <see cref="TransactionPresetBaseType.Transfer"/> preset -- Transfer has no
    /// category concept anywhere in this app (confirmed: <c>Transfer.cs</c> has no <c>CategoryId</c>
    /// property at all, unlike <c>Expense</c>/<c>Income</c>/<c>CreditCardPurchase</c>).</summary>
    public Guid? DefaultCategoryId { get; private set; }

    /// <summary>A <c>FinancialAccount</c> id. Mutually exclusive with <see cref="DefaultCreditAccountId"/>
    /// -- at most one of the two is ever set (never both), and, unlike
    /// <c>RecurringExpense.AccountId</c>/<c>CreditAccountId</c>'s mandatory XOR, both may also be null
    /// simultaneously: a preset is an entry-time shortcut, not a standing obligation, so a preset that
    /// only pre-fills name+icon+category (no default account at all) is a legal, useful preset.</summary>
    public Guid? DefaultAccountId { get; private set; }

    /// <summary>A <c>CreditCard</c> id (§B.7.2's card-backed-preset follow-up). Only ever non-null when
    /// <see cref="BaseType"/> is <see cref="TransactionPresetBaseType.Expense"/> -- mirrors the identical
    /// restriction <c>CreditCardPurchase</c> itself is already subject to everywhere else in this app.
    /// Mutually exclusive with <see cref="DefaultAccountId"/>, never a single polymorphic field: a bare
    /// <see cref="Guid"/> can't self-describe which repository to resolve it against, and
    /// <c>RecurringExpense</c> itself doesn't do this either.</summary>
    public Guid? DefaultCreditAccountId { get; private set; }

    public bool IsActive { get; private set; } = true;

    private TransactionPreset()
    {
    }

    public TransactionPreset(
        string name,
        string? icon,
        TransactionPresetBaseType baseType,
        Guid? defaultCategoryId,
        Guid? defaultAccountId,
        Guid? defaultCreditAccountId)
    {
        ValidateName(name);
        ValidateCategory(baseType, defaultCategoryId);
        ValidateDefaults(baseType, defaultAccountId, defaultCreditAccountId);

        Name = name.Trim();
        Icon = icon;
        BaseType = baseType;
        DefaultCategoryId = defaultCategoryId;
        DefaultAccountId = defaultAccountId;
        DefaultCreditAccountId = defaultCreditAccountId;
    }

    public void Rename(string name)
    {
        ValidateName(name);
        Name = name.Trim();
    }

    public void SetIcon(string? icon) => Icon = icon;

    /// <summary>Re-validates the identical invariants <see cref="ValidateCategory"/>/
    /// <see cref="ValidateDefaults"/> enforce at construction -- mirrors <c>RecurringExpense</c>'s own
    /// "every mutation entry point re-checks the rule" discipline.</summary>
    public void UpdateDefaults(Guid? categoryId, Guid? accountId, Guid? creditAccountId)
    {
        ValidateCategory(BaseType, categoryId);
        ValidateDefaults(BaseType, accountId, creditAccountId);

        DefaultCategoryId = categoryId;
        DefaultAccountId = accountId;
        DefaultCreditAccountId = creditAccountId;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Transaction preset name cannot be empty.", nameof(name));
    }

    private static void ValidateCategory(TransactionPresetBaseType baseType, Guid? categoryId)
    {
        if (categoryId is not null && baseType == TransactionPresetBaseType.Transfer)
            throw new ArgumentException("A Transfer-based preset cannot default to a category -- Transfer has no category concept.", nameof(categoryId));
    }

    /// <summary>The XOR-shaped guard from §B.7.2: throw if both <paramref name="accountId"/> and
    /// <paramref name="creditAccountId"/> are non-null; throw if <paramref name="creditAccountId"/> is
    /// non-null and <paramref name="baseType"/> isn't <see cref="TransactionPresetBaseType.Expense"/>.
    /// Both being null is explicitly allowed -- this is "at most one," not "exactly one."</summary>
    private static void ValidateDefaults(TransactionPresetBaseType baseType, Guid? accountId, Guid? creditAccountId)
    {
        if (accountId is not null && creditAccountId is not null)
            throw new ArgumentException("A preset cannot default to both a FinancialAccount and a credit card at once.");

        if (creditAccountId is not null && baseType != TransactionPresetBaseType.Expense)
            throw new ArgumentException("Only an Expense-based preset can default to a credit card.", nameof(creditAccountId));
    }
}
