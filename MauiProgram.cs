using CommunityToolkit.Maui;
using ExpenseTracker.Data;
using ExpenseTracker.Services;
using ExpenseTracker.Services.Interfaces;
using ExpenseTracker.Validators;
using ExpenseTracker.ViewModels;
using ExpenseTracker.Views;
using FluentValidation;
using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;


namespace ExpenseTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseLiveCharts() // NOWOŚĆ: Inicjalizacja silnika graficznego
                .UseMauiCommunityToolkit()
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
            builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
            builder.Services.AddSingleton<ISettingsService, SettingsService>();

            builder.Services.AddTransient<Views.SetupPage>();

            builder.Services.AddTransient<AccountsViewModel>(); 
            builder.Services.AddTransient<AccountsPage>();

            builder.Services.AddTransient<CategoriesViewModel>();
            builder.Services.AddTransient<CategoriesPage>();
            builder.Services.AddTransient<ProjectsViewModel>();
            builder.Services.AddTransient<ProjectsPage>();

            builder.Services.AddTransient<AddTransactionViewModel>();
            builder.Services.AddTransient<AddTransactionPage>();

            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddTransient<HomePage>();

            builder.Services.AddTransient<ExchangeRatesViewModel>();
            builder.Services.AddTransient<ExchangeRatesPage>();

            builder.Services.AddTransient<FavoriteCurrenciesViewModel>();
            builder.Services.AddTransient<FavoriteCurrenciesPage>();

            builder.Services.AddTransient<Views.TransactionsPage>();
            builder.Services.AddTransient<ViewModels.TransactionsViewModel>();

            builder.Services.AddTransient<RecurringTransactionsViewModel>();
            builder.Services.AddTransient<RecurringTransactionsPage>();

            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<SettingsPage>();

            builder.Services.AddTransient<IValidator<AddTransactionViewModel>, AddTransactionValidator>();
            builder.Services.AddTransient<IValidator<AccountsViewModel>, AccountsValidator>();
            builder.Services.AddTransient<IValidator<CategoriesViewModel>, CategoriesValidator>();
            builder.Services.AddTransient<IValidator<ProjectsViewModel>, ProjectsValidator>();
            builder.Services.AddTransient<IValidator<ExchangeRatesViewModel>, ExchangeRatesValidator>();

            return builder.Build();
        }
    }
}
