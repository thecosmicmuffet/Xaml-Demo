using Microsoft.Extensions.Logging;
#if WINDOWS
using Xaml_Demo.Controls;
#endif

namespace Xaml_Demo;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
builder
.UseMauiApp<App>()
.ConfigureFonts(fonts =>
{
fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
})
.ConfigureMauiHandlers(handlers =>
{
#if WINDOWS
handlers.AddHandler(typeof(WinUIListViewShim), typeof(WinUIListViewShimHandler));
#endif
});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
