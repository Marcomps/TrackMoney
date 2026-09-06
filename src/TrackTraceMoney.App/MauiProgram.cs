using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrackTraceMoney.Infrastructure;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "tracktracemoney.db3");
		builder.Services.AddTrackTraceMoneyInfrastructure($"Data Source={dbPath}");

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>().Database.Migrate();
		}

		return app;
	}
}
