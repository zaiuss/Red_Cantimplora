using Cantimplora.Pages;
using Cantimplora.Services;
using Cantimplora.ViewModels;
using Microsoft.Extensions.Logging;

namespace Cantimplora;

// Bootstrap de MAUI. Configura el contenedor de DI con servicios y páginas.
public static class MauiProgram
{
	// Registra IBluetoothService, IMessageStore, Esp32ViewModel y las páginas.
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<IBluetoothService, BluetoothService>();
		builder.Services.AddSingleton<IMessageStore, MessageStore>();
		builder.Services.AddSingleton<SettingsService>();
		builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
		builder.Services.AddSingleton<IPermissionService, BluetoothPermissionService>();
		builder.Services.AddSingleton<IToastService, ToastService>();
		builder.Services.AddSingleton<Esp32ViewModel>();
		builder.Services.AddSingleton<IChatCoordinator>(sp => sp.GetRequiredService<Esp32ViewModel>());
		builder.Services.AddSingleton<IEsp32ConfigCoordinator>(sp => sp.GetRequiredService<Esp32ViewModel>());

		builder.Services.AddTransient<Esp32Page>();
		builder.Services.AddTransient<Esp32ConfigPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
