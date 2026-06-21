using Microsoft.Extensions.Logging;
using UltraViagem.Android.Pages;
using UltraViagem.Android.Services;
using UltraViagem.Android.ViewModels;

namespace UltraViagem.Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<TripFileService>();
        builder.Services.AddSingleton<TripsViewModel>();
        builder.Services.AddTransient<TripViewModel>();
        builder.Services.AddTransient<TripsPage>();
        builder.Services.AddTransient<OverviewPage>();
        builder.Services.AddTransient<ItineraryPage>();
        builder.Services.AddTransient<TasksPage>();
        builder.Services.AddTransient<ExpensesPage>();
        builder.Services.AddTransient<LinksPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
