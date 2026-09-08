using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using ExpenseTracker.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace ExpenseTracker
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // 1. WCZYTANIE JĘZYKA
            // Jeśli użytkownik wcześniej zapisał język, pobieramy go. 
            // Jeśli nie, pobieramy język systemu (CurrentUICulture).
            string defaultLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            string savedLang = Preferences.Default.Get("AppLanguage", defaultLang);

            var culture = new CultureInfo(savedLang);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            AppResources.Culture = culture;

            // 2. WCZYTANIE MOTYWU
            // Domyślnie ładujemy motyw jasny (indeks 0), jeśli nic nie zapisano.
            int savedThemeIndex = Preferences.Default.Get("AppTheme", 0);
            ApplyTheme(savedThemeIndex);

            
        }

        protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
        {
            // Jawne wskazanie przestrzeni nazw rozwiązuje konflikt kompilatora
            var window = new Microsoft.Maui.Controls.Window(new SetupPage());

            window.Created += (s, e) =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        if (activationState?.Context?.Services != null)
                        {
                            var engine = activationState.Context.Services.GetRequiredService<IRecurringTransactionEngine>();
                            await engine.ProcessPendingTransactionsAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RecurringEngine Error]: {ex.Message}");
                        // Wypchnięcie błędu na główny wątek, aby Developer go zobaczył na urządzeniu!
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            Shell.Current?.DisplayAlertAsync("Błąd Silnika", ex.Message, "OK");
                        });
                    }
                });
            };

            return window;
        }

        public static void ApplyTheme(int themeIndex)
        {
            var mergedDictionaries = Current!.Resources.MergedDictionaries;
            var existingTheme = mergedDictionaries.FirstOrDefault(d => d.GetType().Namespace == "ExpenseTracker.Resources.Themes");

            if (existingTheme != null)
            {
                mergedDictionaries.Remove(existingTheme);
            }

            switch (themeIndex)
            {
                case 0:
                    mergedDictionaries.Add(new Resources.Themes.LightTheme());
                    Application.Current.UserAppTheme = AppTheme.Light;
                    break;
                case 1:
                    mergedDictionaries.Add(new Resources.Themes.DarkTheme());
                    Application.Current.UserAppTheme = AppTheme.Dark;
                    break;
                case 2:
                    mergedDictionaries.Add(new Resources.Themes.HighContrastTheme());
                    Application.Current.UserAppTheme = AppTheme.Dark;
                    break;
            }

           
        }
    }
}