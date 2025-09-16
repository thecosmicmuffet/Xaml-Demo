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

var app = builder.Build();

// Wire up Core logging sink (only if not already set by another host)
Xaml_Demo.Logging.LogRouter.Sink ??= new Xaml_Demo.Services.LogSinkAdapter();

return app;
	}
}
