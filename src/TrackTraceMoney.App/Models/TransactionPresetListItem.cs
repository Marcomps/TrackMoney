using TrackTraceMoney.Domain.TransactionPresets;

namespace TrackTraceMoney.App.Models;

public sealed record TransactionPresetListItem(Guid Id, string Name, string? Icon, TransactionPresetBaseType BaseType, bool IsActive)
{
    public bool IsInactive => !IsActive;

    /// <summary>Mirrors <c>CategoryListItem.HasIcon</c>'s exact shape.</summary>
    public bool HasIcon => !string.IsNullOrEmpty(Icon);

    public static TransactionPresetListItem FromDomain(TransactionPreset preset) =>
        new(preset.Id, preset.Name, preset.Icon, preset.BaseType, preset.IsActive);
}
