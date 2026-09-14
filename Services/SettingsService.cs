using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.Services
{
    public enum AppThemeOption
    {
        Light = 0,
        Dark = 1,
        HighContrast = 2
    }

    public class SettingsService : ISettingsService
    {
        // Używamy nameof(), aby uniknąć literówek w kluczach (tzw. "Magic Strings")
        public string DefaultCurrency
        {
            get => Preferences.Default.Get(nameof(DefaultCurrency), "PLN");
            set => Preferences.Default.Set(nameof(DefaultCurrency), value);
        }

        public string AppLanguage
        {
            get => Preferences.Default.Get(nameof(AppLanguage), "en");
            set => Preferences.Default.Set(nameof(AppLanguage), value);
        }

        public int AppTheme
        {
            get => Preferences.Default.Get(nameof(AppTheme), (int)AppThemeOption.Light);
            set => Preferences.Default.Set(nameof(AppTheme), value);
        }

        public string RecentCategoryColors
        {
            get => Preferences.Default.Get(nameof(RecentCategoryColors), string.Empty);
            set => Preferences.Default.Set(nameof(RecentCategoryColors), value);
        }

        public void ClearBusinessSettings()
        {
            // Czyścimy tylko ustawienia biznesowe, zostawiając motyw i język, 
            // żeby po zresetowaniu aplikacji UI nie oszalało.
            Preferences.Default.Remove(nameof(DefaultCurrency));
            // Preferences.Default.Remove("FavoriteCurrencies"); // Jeśli dodasz w przyszłości
        }
    }
}
