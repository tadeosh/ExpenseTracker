using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using ExpenseTracker.Data;

#if ANDROID
using ExpenseTracker.Platforms.Android.Handlers;
#endif

using ExpenseTracker.Services;
using ExpenseTracker.Services.Interfaces;
using ExpenseTracker.Validators;
using ExpenseTracker.ViewModels;
using ExpenseTracker.Views;
using ExpenseTracker.Views.Controls;
using FluentValidation;
using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace ExpenseTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseLiveCharts()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler(typeof(AppPlatformDatePicker), typeof(AppDatePickerHandler));
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        RegisterServices(builder.Services);
        RegisterViewModels(builder.Services);
        RegisterViews(builder.Services);
        RegisterValidators(builder.Services);

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<DatabaseService>();

        services.AddSingleton<IDatabaseService>(serviceProvider =>
            serviceProvider.GetRequiredService<DatabaseService>());

        services.AddSingleton<IDatabaseMaintenanceService>(serviceProvider =>
            serviceProvider.GetRequiredService<DatabaseService>());

        services.AddSingleton<ITemporaryDirectoryProvider, MauiTemporaryDirectoryProvider>();
        services.AddSingleton<IPortableBackupService, PortableBackupService>();

        services.AddSingleton<IFileSaver>(FileSaver.Default);
        services.AddSingleton<IBackupFileService, BackupFileService>();

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IRecurringTransactionEngine, RecurringTransactionEngine>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<ITransactionChangeNotifier, TransactionChangeNotifier>();
        services.AddSingleton<IDialogService, DialogService>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<AccountsViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<AddTransactionViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ExchangeRatesViewModel>();
        services.AddTransient<FavoriteCurrenciesViewModel>();
        services.AddTransient<TransactionsViewModel>();
        services.AddTransient<RecurringTransactionsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddTransient<SetupPage>();
        services.AddTransient<AccountsPage>();
        services.AddTransient<CategoriesPage>();
        services.AddTransient<ProjectsPage>();
        services.AddTransient<AddTransactionPage>();
        services.AddTransient<HomePage>();
        services.AddTransient<ExchangeRatesPage>();
        services.AddTransient<FavoriteCurrenciesPage>();
        services.AddTransient<TransactionsPage>();
        services.AddTransient<RecurringTransactionsPage>();
        services.AddTransient<ReportsPage>();
        services.AddTransient<SettingsPage>();
    }

    private static void RegisterValidators(IServiceCollection services)
    {
        services.AddTransient<IValidator<AddTransactionViewModel>, AddTransactionValidator>();
        services.AddTransient<IValidator<AccountsViewModel>, AccountsValidator>();
        services.AddTransient<IValidator<CategoriesViewModel>, CategoriesValidator>();
        services.AddTransient<IValidator<ProjectsViewModel>, ProjectsValidator>();
        services.AddTransient<IValidator<ExchangeRatesViewModel>, ExchangeRatesValidator>();
    }
}
