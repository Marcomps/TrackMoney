namespace TrackTraceMoney.App.Models;

/// <summary>
/// Dashboard health indicator v0 (README §30/§35 — final formula, do not add 🟡 Attention or
/// 🔵 Opportunity, those are explicitly deferred past v0).
/// </summary>
public enum FinancialHealthStatus
{
    /// <summary>🟢 No negative balance and no over-budget categories.</summary>
    Healthy,

    /// <summary>🟠 No negative balance, but at least one budget category is over budget this month.</summary>
    AtRisk,

    /// <summary>🔴 At least one active account has a negative balance.</summary>
    Critical
}

public static class FinancialHealthEvaluator
{
    public static FinancialHealthStatus Evaluate(bool hasNegativeBalance, bool hasOverBudgetCategory)
    {
        if (hasNegativeBalance)
            return FinancialHealthStatus.Critical;

        return hasOverBudgetCategory ? FinancialHealthStatus.AtRisk : FinancialHealthStatus.Healthy;
    }

    public static string GetEmoji(FinancialHealthStatus status) => status switch
    {
        FinancialHealthStatus.Critical => "\U0001F534", // 🔴
        FinancialHealthStatus.AtRisk => "\U0001F7E0", // 🟠
        FinancialHealthStatus.Healthy => "\U0001F7E2", // 🟢
        _ => string.Empty
    };
}
