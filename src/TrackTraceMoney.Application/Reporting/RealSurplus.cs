namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// README §35: "sobrante real" is balance minus known upcoming obligations — a distinct number
/// from raw account balance ("saldo disponible"). Never substitute one for the other in dashboards
/// or reports.
/// </summary>
public sealed record RealSurplus(decimal AvailableBalance, decimal UpcomingObligations, decimal Amount)
{
    public static RealSurplus Calculate(decimal availableBalance, decimal upcomingObligations) =>
        new(availableBalance, upcomingObligations, availableBalance - upcomingObligations);
}
