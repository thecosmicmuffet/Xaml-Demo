using Microsoft.Extensions.Logging;
#if WINDOWS
using Xaml_Demo.Controls;
#endif
using Xaml_Demo.Surfaces;

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

// Dispatcher + lifecycle infrastructure
builder.Services.AddSingleton<Xaml_Demo.Dispatching.IUiDispatcher, Xaml_Demo.Dispatching.MauiDispatcherAdapter>();
builder.Services.AddSingleton<ISurfaceCatalog>(_ => DefaultSurfaceCatalog.Instance);

#if DEBUG
		builder.Logging.AddDebug();
#endif

var app = builder.Build();

#nullable enable
// Wire up Core logging sink (only if not already set by another host)
Xaml_Demo.Logging.LogRouter.Sink ??= new Xaml_Demo.Services.LogSinkAdapter();

// Initialize ambient dispatcher (idempotent)
Xaml_Demo.Dispatching.MauiDispatcherRegistration.EnsureAmbientInitialized(app.Services);
#nullable disable

return app;
	}
}
