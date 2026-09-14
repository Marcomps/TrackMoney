using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.TermDeposits;

/// <summary>
/// Applies <see cref="Domain.Accounts.TermDeposit.RenewAtMaturity"/> across every matured,
/// auto-renewal-enabled term deposit — the Application-layer wiring for README §22's AutoRenewal flag,
/// invoked on every Dashboard load (see <c>DashboardViewModel.LoadDashboardAsync</c>) the same way
/// <c>INetWorthSnapshotService</c> is.
/// </summary>
public interface ITermDepositRenewalService
{
    Task<IReadOnlyList<TermDepositRenewalResult>> ProcessMaturedRenewalsAsync(
        DateOnly asOfDate, CancellationToken ct = default);
}

/// <summary>One successfully-renewed term deposit, enough for the caller to raise a notification.</summary>
public sealed record TermDepositRenewalResult(
    Guid OldTermDepositId,
    Guid NewTermDepositId,
    string Institution,
    CurrencyCode Currency,
    decimal RenewedAmount,
    DateOnly NewMaturityDate);
