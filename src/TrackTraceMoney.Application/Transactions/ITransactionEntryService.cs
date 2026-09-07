namespace TrackTraceMoney.Application.Transactions;

/// <summary>
/// Coordinates recording a transaction with the balance mutation it implies on the affected
/// account(s) — these are two aggregates that must change together (README §9), which belongs
/// in the Application layer rather than Domain.
/// </summary>
public interface ITransactionEntryService
{
    Task RecordExpenseAsync(
        DateOnly date,
        decimal amount,
        Guid accountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        CancellationToken ct = default);

    Task RecordIncomeAsync(
        DateOnly date,
        decimal amount,
        Guid destinationAccountId,
        Guid categoryId,
        Guid? personId,
        string? description,
        string? notes,
        CancellationToken ct = default);

    Task RecordTransferAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid destinationAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default);
}
