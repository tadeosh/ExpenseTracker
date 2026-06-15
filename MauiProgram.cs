using ExpenseTracker.Data;
using ExpenseTracker.ViewModels;
using ExpenseTracker.Views;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker
{
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

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            // NOWOŚĆ: Rejestrujemy nasz serwis bazy danych.
            // AddSingleton oznacza, że aplikacja stworzy go raz i będzie używać tej samej kopii wszędzie.
            builder.Services.AddSingleton<DatabaseService>();

            builder.Services.AddTransient<AccountsViewModel>(); 
            builder.Services.AddTransient<AccountsPage>();

            return builder.Build();
        }
    }
}
