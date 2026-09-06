using TrackTraceMoney.App.Views;

namespace TrackTraceMoney.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(AddAccountPage), typeof(AddAccountPage));
	}
}
