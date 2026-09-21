using TrackTraceMoney.Domain.TransactionPresets;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// Backs the Add-Transaction screen's "Quick presets" chip row (Transaction Type Customization slice
/// spec §B.4/§B.7.3) -- one active <see cref="TransactionPreset"/> per chip. Carries the preset's raw
/// default-field ids (not resolved <see cref="NamedOption"/>s) so <c>AddTransactionViewModel</c>'s
/// preset-tap handler can resolve each against the correct already-loaded collection itself (see
/// <c>ApplyPresetCommand</c>'s doc comment for the exact per-<see cref="TransactionPresetBaseType"/>
/// resolution rules), the same "resolve against a live picker collection, not a stale snapshot" pattern
/// <c>AddTransactionViewModel.LoadEditingTransactionAsync</c> already relies on.
/// </summary>
public sealed record TransactionPresetOption(
    Guid Id,
    string Name,
    string? Icon,
    TransactionPresetBaseType BaseType,
    Guid? DefaultCategoryId,
    Guid? DefaultAccountId,
    Guid? DefaultCreditAccountId)
{
    /// <summary>The chip's rendered label -- "{icon} {name}" when an icon is set, otherwise just the
    /// name, mirroring the emoji-as-icon convention used throughout this app (no image-asset system).</summary>
    public string DisplayLabel => string.IsNullOrEmpty(Icon) ? Name : $"{Icon} {Name}";

    public static TransactionPresetOption FromDomain(TransactionPreset preset) =>
        new(preset.Id, preset.Name, preset.Icon, preset.BaseType, preset.DefaultCategoryId, preset.DefaultAccountId, preset.DefaultCreditAccountId);
}
