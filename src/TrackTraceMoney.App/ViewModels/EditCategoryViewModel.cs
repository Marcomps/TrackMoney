using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Rename + icon editing for a <see cref="Domain.Categories.Category"/> (Category lifecycle slice
/// §A.4), mirroring <c>AddAccountViewModel</c>'s edit-mode precedent but as its own dedicated page
/// rather than a shared Add/Edit page -- <c>AddCategoryPage</c>'s insert flow has no "existing entity"
/// concept to graft an edit branch onto cleanly, and a small mutator-driven page is the same choice
/// <c>EditRecurringIncomeAmountViewModel</c> already made for the identical shape of problem.
///
/// For a system-defined category (<c>IsSystemDefined == true</c>): only the icon is editable.
/// <see cref="Name"/> is populated from the same localized display-name resolution
/// (<see cref="SystemCategoryKeyToLabelConverter.GetDisplayName"/>) the rest of the app already uses
/// to render a system category's name, not from <c>Category.Name</c> directly -- confirmed by reading
/// that converter before writing this class (Category lifecycle slice §A-1's own resolution). Renaming
/// "Food" to something else would silently do nothing useful, since every other screen still resolves
/// the display name via <c>SystemKey</c>, not the (unused, for system categories) <c>Name</c> field --
/// so <see cref="IsNameEditable"/> gates the Name <c>Entry</c> to read-only for these.
/// </summary>
[QueryProperty(nameof(CategoryIdText), "categoryId")]
public sealed partial class EditCategoryViewModel : ObservableObject
{
    private readonly ICategoryRepository _categoryRepository;

    [ObservableProperty]
    private Guid categoryId;

    /// <summary>
    /// The actual <c>[QueryProperty]</c> target. MAUI Shell's query-string navigation always hands a
    /// <c>string</c> to the receiving property and internally does a plain <c>Convert.ChangeType</c> --
    /// which throws <see cref="InvalidCastException"/> for a non-nullable <see cref="Guid"/> target.
    /// Bound here as a string and parsed defensively instead, same idiom as
    /// <c>EditRecurringIncomeAmountViewModel.RecurringIncomeIdText</c>.
    /// </summary>
    [ObservableProperty]
    private string? categoryIdText;

    partial void OnCategoryIdTextChanged(string? value)
    {
        if (Guid.TryParse(value, out var parsed))
            CategoryId = parsed;
    }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string selectedIcon = CategoryIconPalette.NoIconValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSystemNameLockedMessageVisible))]
    private bool isNameEditable = true;

    /// <summary>
    /// Shown instead of silently disabling the Name <c>Entry</c> (mirrors
    /// <c>AddAccountViewModel.IsCurrencyLockedMessageVisible</c>'s own "show why it's disabled, don't
    /// just silently disable it" precedent).
    /// </summary>
    public bool IsSystemNameLockedMessageVisible => !IsNameEditable;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<string> AvailableIcons { get; } = CategoryIconPalette.Icons;

    public EditCategoryViewModel(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (CategoryId == Guid.Empty)
            return;

        var category = await _categoryRepository.GetByIdAsync(CategoryId);
        if (category is null)
        {
            ErrorMessage = AppResources.EditCategory_NotFound;
            return;
        }

        Name = SystemCategoryKeyToLabelConverter.GetDisplayName(category);
        SelectedIcon = category.Icon ?? CategoryIconPalette.NoIconValue;
        IsNameEditable = !category.IsSystemDefined;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (IsNameEditable && string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddCategory_ValidationNameRequired;
            return;
        }

        IsBusy = true;
        try
        {
            var category = await _categoryRepository.GetByIdAsync(CategoryId);
            if (category is null)
            {
                ErrorMessage = AppResources.EditCategory_NotFound;
                return;
            }

            if (IsNameEditable)
                category.Rename(Name);

            category.SetIcon(AsNullableIcon(SelectedIcon));

            await _categoryRepository.SaveChangesAsync();
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private static string? AsNullableIcon(string icon) =>
        icon == CategoryIconPalette.NoIconValue ? null : icon;
}
