using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// One currency group's README §20 snowball plan — recomputed entirely in-memory (no repository
/// calls) whenever the user edits <see cref="ExtraAvailableText"/>, mirroring DashboardViewModel's
/// "per currency, never blended" principle but with each group owning its own bindable "extra
/// available" input rather than sharing one global figure across currencies. An <see cref="ObservableObject"/>
/// (not a plain record) because it needs to raise change notifications after recomputing.
/// </summary>
public sealed partial class SnowballCurrencyGroupItem : ObservableObject
{
    private readonly ISnowballPlanner _planner;
    private readonly IReadOnlyList<SnowballDebtInput> _debts;

    private SnowballPlan _currentPlan;

    public CurrencyCode Currency { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalMinimums))]
    [NotifyPropertyChangedFor(nameof(ExtraAppliedToTarget))]
    [NotifyPropertyChangedFor(nameof(UnallocatedExtra))]
    [NotifyPropertyChangedFor(nameof(NextTargetName))]
    [NotifyPropertyChangedFor(nameof(ShowNextTargetAdvisory))]
    [NotifyPropertyChangedFor(nameof(NextTargetAdvisoryMessage))]
    [NotifyPropertyChangedFor(nameof(PayThisMonthText))]
    [NotifyPropertyChangedFor(nameof(PayThisMonthBreakdownText))]
    [NotifyPropertyChangedFor(nameof(DebtFreeText))]
    private string extraAvailableText = "0";

    private int? _debtFreeInMonths;

    public ObservableCollection<SnowballDebtLineItem> Lines { get; } = [];

    public decimal TotalMinimums => _currentPlan.TotalMinimums;

    public decimal ExtraAppliedToTarget => _currentPlan.ExtraAppliedToTarget;

    public decimal UnallocatedExtra => _currentPlan.UnallocatedExtra;

    /// <summary>
    /// The current target's name. The snowball order depends only on AmountOwed/CreatedAtUtc/Id (never
    /// on the declared extra), so this is stable across recomputes for the lifetime of this group.
    /// </summary>
    public string? CurrentTargetName => _debts.Count > 0 ? _currentPlan.Lines[0].Name : null;

    /// <summary>
    /// The name of the debt named in <see cref="SnowballPlan.NextTargetAfterCurrentId"/>, looked up
    /// against this group's own debt list. Null whenever the plan has no "what's next" advisory to
    /// show (no overflow, or no next-smallest debt in this currency group).
    /// </summary>
    public string? NextTargetName => _currentPlan.NextTargetAfterCurrentId is { } nextTargetId
        ? _debts.FirstOrDefault(d => d.CreditAccountId == nextTargetId)?.Name
        : null;

    /// <summary>What to pay this month across every debt in this currency: all minimums plus the extra applied.</summary>
    public string PayThisMonthText => string.Format(
        CultureInfo.CurrentCulture, AppResources.SnowballPlan_PayThisMonthFormat, Lines.Sum(l => l.SuggestedTotalPayment));

    public string PayThisMonthBreakdownText => string.Format(
        CultureInfo.CurrentCulture, AppResources.SnowballPlan_PayThisMonthBreakdownFormat, TotalMinimums, ExtraAppliedToTarget);

    public string DebtFreeText => _debtFreeInMonths is { } months
        ? string.Format(CultureInfo.CurrentCulture, AppResources.SnowballPlan_DebtFreeFormat, months, MonthLabel(months))
        : AppResources.SnowballPlan_DebtFreeUnknown;

    public bool ShowNextTargetAdvisory => UnallocatedExtra > 0m && NextTargetName is not null;

    public string NextTargetAdvisoryMessage => ShowNextTargetAdvisory
        ? string.Format(CultureInfo.CurrentCulture, AppResources.SnowballPlan_UnallocatedExtraMessage, UnallocatedExtra, CurrentTargetName, NextTargetName)
        : string.Empty;

    public SnowballCurrencyGroupItem(ISnowballPlanner planner, CurrencyCode currency, IReadOnlyList<SnowballDebtInput> debts)
    {
        _planner = planner;
        Currency = currency;
        _debts = debts;

        _currentPlan = _planner.Plan(_debts, 0m);
        RebuildLines(0m);
    }

    partial void OnExtraAvailableTextChanged(string value) => Recompute();

    private void Recompute()
    {
        var extra = decimal.TryParse(ExtraAvailableText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed) && parsed >= 0
            ? parsed
            : 0m;

        _currentPlan = _planner.Plan(_debts, extra);
        RebuildLines(extra);
    }

    private void RebuildLines(decimal extra)
    {
        var payoffMonths = SnowballPayoffEstimator.EstimatePayoffMonths(_debts, extra);

        Lines.Clear();
        var position = 1;
        foreach (var line in _currentPlan.Lines)
        {
            var month = payoffMonths.GetValueOrDefault(line.CreditAccountId);
            Lines.Add(SnowballDebtLineItem.FromDomain(line, position++, month is { } m ? MonthLabel(m) : null));
        }

        _debtFreeInMonths = payoffMonths.Count > 0 && payoffMonths.Values.All(m => m is not null)
            ? payoffMonths.Values.Max()
            : null;
    }

    /// <summary>Month 1 is the current month (its payment is the one suggested now).</summary>
    private static DateOnly MonthLabel(int payoffMonth)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new DateOnly(today.Year, today.Month, 1).AddMonths(payoffMonth - 1);
    }
}
