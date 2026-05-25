using Microsoft.Extensions.Logging;
using UltraViagem.Android.Services;
using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android;

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
            });

        // Serviços
        builder.Services.AddSingleton<TripFileService>();

        // ViewModels
        builder.Services.AddSingleton<TripsViewModel>();
        builder.Services.AddTransient<TripViewModel>();

        // Pages
        builder.Services.AddSingleton<Pages.TripsPage>();
        builder.Services.AddTransient<Pages.TripPage>();
        builder.Services.AddTransient<Pages.OverviewPage>();
        builder.Services.AddTransient<Pages.ItineraryPage>();
        builder.Services.AddTransient<Pages.ExpensesPage>();
        builder.Services.AddTransient<Pages.LinksPage>();
        builder.Services.AddTransient<Pages.TasksPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
