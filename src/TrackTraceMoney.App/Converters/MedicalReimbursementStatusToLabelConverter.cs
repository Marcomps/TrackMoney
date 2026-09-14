using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.MedicalExpenses;

namespace TrackTraceMoney.App.Converters;

/// <summary>
/// Shared status-label mapping for <see cref="MedicalReimbursementStatus"/> (README §25/§26/§27/§29) —
/// previously duplicated verbatim in both <c>MedicalExpenseDetailViewModel.GetStatusLabel</c> and
/// <c>HistoryEntryItem.GetMedicalStatusBadge</c>. Mirrors <see cref="SystemCategoryKeyToLabelConverter"/>'s
/// pattern: a <see cref="EnumToLabelConverter{TEnum}"/> for XAML binding use, plus a public static
/// <see cref="GetDisplayName"/> for the direct C# calls both callers actually need today.
/// </summary>
public sealed class MedicalReimbursementStatusToLabelConverter : EnumToLabelConverter<MedicalReimbursementStatus>
{
    protected override string GetLabel(MedicalReimbursementStatus value) => GetDisplayName(value);

    /// <summary>
    /// Only <see cref="MedicalReimbursementStatus.Pending"/>/<see cref="MedicalReimbursementStatus.Reimbursed"/>/
    /// <see cref="MedicalReimbursementStatus.Rejected"/> have a label — <see cref="MedicalReimbursementStatus.None"/>/
    /// <see cref="MedicalReimbursementStatus.PaidDirectly"/> resolve to empty, matching both original
    /// call sites' behavior (neither ever showed a badge/label for those two statuses).
    /// </summary>
    public static string GetDisplayName(MedicalReimbursementStatus status) => status switch
    {
        MedicalReimbursementStatus.Pending => AppResources.History_MedicalStatusPending,
        MedicalReimbursementStatus.Reimbursed => AppResources.History_MedicalStatusReimbursed,
        MedicalReimbursementStatus.Rejected => AppResources.History_MedicalStatusRejected,
        _ => string.Empty
    };
}
