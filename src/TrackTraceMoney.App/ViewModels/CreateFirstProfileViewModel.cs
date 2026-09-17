using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Backs <see cref="Views.CreateFirstProfilePage"/> — the app's root page (constructed directly by
/// <c>App.CreateWindow</c>, not reached via a Shell route) when no local profile is active yet at
/// cold start.
/// </summary>
public sealed partial class CreateFirstProfileViewModel : ObservableObject
{
    private readonly IProfileManagementService _profileManagementService;
    private readonly IActiveProfileStore _activeProfileStore;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public CreateFirstProfileViewModel(
        IProfileManagementService profileManagementService,
        IActiveProfileStore activeProfileStore,
        IServiceProvider serviceProvider)
    {
        _profileManagementService = profileManagementService;
        _activeProfileStore = activeProfileStore;
        _serviceProvider = serviceProvider;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.CreateFirstProfile_ValidationNameRequired;
            return;
        }

        IsBusy = true;
        try
        {
            var profile = await _profileManagementService.CreateProfileAsync(Name);
            await _activeProfileStore.SetActiveProfileIdAsync(profile.Id);

            // Nothing has touched the finance database yet at this point in a brand-new install --
            // MauiProgram.CreateMauiApp deliberately skips migrating/seeding it while no profile is
            // active (see that file). Do it now, in a throwaway child scope, mirroring that same
            // startup block's own CreateScope() usage.
            //
            // Deliberately NOT a normal constructor-injected IFinanceDatabaseInitializer: this
            // ViewModel is constructed eagerly, at app cold-start (see App.xaml.cs's CreateWindow),
            // before any profile exists yet. An eagerly injected dependency that itself depends on
            // TrackTraceMoneyDbContext would force that DbContext to be constructed right then --
            // and, because it would be resolved from the app's single root container, permanently
            // cached for the rest of the session -- against the still-no-active-profile fallback
            // connection string. Resolving it lazily, from a scope created here after
            // SetActiveProfileIdAsync has already completed, avoids that trap.
            using (var scope = _serviceProvider.CreateScope())
            {
                var financeDatabaseInitializer = scope.ServiceProvider.GetRequiredService<IFinanceDatabaseInitializer>();
                await financeDatabaseInitializer.EnsureReadyAsync(AppResources.PersonRelationshipType_Me);
            }

            // Live root-page swap -- supported without a restart specifically because nothing has
            // constructed TrackTraceMoneyDbContext from the app's root container before this point
            // (see the scoped resolution above). AppShell's own tabs resolve their pages lazily,
            // against the app's ambient DI container, the first time Shell actually navigates to them.
            //
            // Fully qualified (not bare "Application") because this file's enclosing namespace chain
            // (TrackTraceMoney.App.ViewModels -> TrackTraceMoney.App -> TrackTraceMoney) also contains
            // the sibling TrackTraceMoney.Application project's namespace, which shadows the bare
            // "Application" MAUI type from Microsoft.Maui.Controls -- App.xaml.cs's own base-class
            // declaration already works around the same collision the same way.
            Microsoft.Maui.Controls.Application.Current!.Windows[0].Page = new AppShell();
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.CreateFirstProfile_Error;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
