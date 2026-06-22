using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using WalkBase.Rendering;
using WalkBase.Services;
using WalkBase.ViewModels;
using WalkBase.Views;

namespace WalkBase;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseSkiaSharp()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Services (singletons — single source of truth for game state).
		builder.Services.AddSingleton<ISettingsService, SettingsService>();
		builder.Services.AddSingleton<ICurrencyService, CurrencyService>();
		builder.Services.AddSingleton<IGameDataService, GameDataService>();
		builder.Services.AddSingleton<IStepSensor, StepSensor>();
		builder.Services.AddSingleton<IStepService, StepService>();
		builder.Services.AddSingleton<IBackgroundTracker, BackgroundTracker>();
		builder.Services.AddSingleton<IFeedbackService, FeedbackService>();
		builder.Services.AddSingleton<IAmbientService, AmbientService>();
		builder.Services.AddSingleton<IWidgetService, WidgetService>();
		builder.Services.AddSingleton<IShareService, ShareService>();
		builder.Services.AddSingleton<SelectionState>();
		builder.Services.AddSingleton<SpriteCache>();
		builder.Services.AddSingleton<IsometricRenderer>();

		// ViewModels.
		builder.Services.AddSingleton<BaseViewModel>();
		builder.Services.AddSingleton<BuildViewModel>();
		builder.Services.AddSingleton<StatsViewModel>();
		builder.Services.AddSingleton<HistoryViewModel>();
		builder.Services.AddSingleton<InsightsViewModel>();
		builder.Services.AddSingleton<SettingsViewModel>();
		builder.Services.AddSingleton<QuestsViewModel>();
		builder.Services.AddSingleton<ProfileViewModel>();

		// Pages.
		builder.Services.AddSingleton<BasePage>();
		builder.Services.AddSingleton<BuildPage>();
		builder.Services.AddSingleton<StatsPage>();
		builder.Services.AddSingleton<HistoryPage>();
		builder.Services.AddSingleton<InsightsPage>();
		builder.Services.AddSingleton<SettingsPage>();
		builder.Services.AddSingleton<QuestsPage>();
		builder.Services.AddSingleton<PrivacyPage>();
		builder.Services.AddSingleton<ProfilePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
