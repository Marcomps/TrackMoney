using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class CategoriesListViewModel : ObservableObject
{
    private readonly ICategoryRepository _categoryRepository;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<CategoryListItem> Categories { get; } = [];

    public bool IsEmpty => HasLoaded && Categories.Count == 0;

    public CategoriesListViewModel(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    [RelayCommand]
    private async Task LoadCategoriesAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var categories = await _categoryRepository.GetAllAsync();

            Categories.Clear();
            foreach (var category in categories)
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
}
