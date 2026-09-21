using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Categories;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class CategoriesListViewModel : ObservableObject
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICategoryLifecycleService _categoryLifecycleService;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    /// <summary>
    /// Non-optional per the Category lifecycle slice §A.4: without this toggle, Deactivate is a
    /// one-way trap -- this ViewModel would otherwise only ever call <c>GetActiveAsync</c>, so a
    /// deactivated category would vanish from the user's own view with no UI path back. Mirrors
    /// <c>AccountsListViewModel.ShowInactive</c>'s exact shape.
    /// </summary>
    [ObservableProperty]
    private bool showInactive;

    [ObservableProperty]
    private string? errorMessage;

    public ObservableCollection<CategoryListItem> Categories { get; } = [];

    public bool IsEmpty => HasLoaded && Categories.Count == 0;

    public CategoriesListViewModel(ICategoryRepository categoryRepository, ICategoryLifecycleService categoryLifecycleService)
    {
        _categoryRepository = categoryRepository;
        _categoryLifecycleService = categoryLifecycleService;
    }

    partial void OnShowInactiveChanged(bool value) => LoadCategoriesCommand.Execute(null);

    [RelayCommand]
    private async Task LoadCategoriesAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var categories = ShowInactive
                ? await _categoryRepository.GetAllAsync()
                : await _categoryRepository.GetActiveAsync();

            Categories.Clear();
            foreach (var category in categories.OrderByDescending(c => c.IsActive).ThenBy(c => SystemCategoryKeyToLabelConverter.GetDisplayName(c)))
                Categories.Add(CategoryListItem.FromDomain(category, SystemCategoryKeyToLabelConverter.GetDisplayName(category)));

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddCategoryAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddCategoryPage));
    }

    [RelayCommand]
    private static async Task EditCategoryAsync(Guid categoryId) =>
        await Shell.Current.GoToAsync($"{nameof(EditCategoryPage)}?categoryId={categoryId}");

    /// <summary>
    /// Checks <c>CanHardDeleteAsync</c> BEFORE showing the destructive confirm dialog -- if it's false,
    /// shows an explanatory message instead of a confirm dialog the user would only have rejected
    /// after the fact (mirrors <c>AccountsListViewModel.DeleteAccountAsync</c>'s exact shape). The
    /// confirm-then-delete call re-checks the guard itself (never trusting this cached "yes") -- see
    /// <see cref="CategoryLifecycleService.DeleteAsync"/>.
    /// </summary>
    [RelayCommand]
    private async Task DeleteCategoryAsync(Guid categoryId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        IsBusy = true;
        bool canDelete;
        try
        {
            canDelete = await _categoryLifecycleService.CanHardDeleteAsync(categoryId);
        }
        finally
        {
            IsBusy = false;
        }

        if (!canDelete)
        {
            ErrorMessage = AppResources.CategoriesList_CannotDeleteMessage;
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.CategoriesList_DeleteConfirmTitle,
            AppResources.CategoriesList_DeleteConfirmMessage,
            AppResources.CategoriesList_DeleteConfirmAccept,
            AppResources.CategoriesList_DeleteConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            await _categoryLifecycleService.DeleteAsync(categoryId);
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = AppResources.CategoriesList_CannotDeleteMessage;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadCategoriesAsync();
    }

    [RelayCommand]
    private async Task DeactivateCategoryAsync(Guid categoryId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.CategoriesList_DeactivateConfirmTitle,
            AppResources.CategoriesList_DeactivateConfirmMessage,
            AppResources.CategoriesList_DeactivateConfirmAccept,
            AppResources.CategoriesList_DeactivateConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            await _categoryLifecycleService.DeactivateAsync(categoryId);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.CategoriesList_Error;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadCategoriesAsync();
    }

    [RelayCommand]
    private async Task ReactivateCategoryAsync(Guid categoryId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await _categoryLifecycleService.ReactivateAsync(categoryId);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.CategoriesList_Error;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadCategoriesAsync();
    }
}
