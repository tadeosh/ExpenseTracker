using ExpenseTracker.Resources.Strings;
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

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
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
                    break;
                case 1:
                    mergedDictionaries.Add(new Resources.Themes.DarkTheme());
                    break;
                case 2:
                    mergedDictionaries.Add(new Resources.Themes.HighContrastTheme());
                    break;
            }
        }
    }
}