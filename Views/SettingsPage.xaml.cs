using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Data;
using System.Globalization;

namespace ExpenseTracker.Views;

public partial class SettingsPage : ContentPage
{
    private readonly DatabaseService _databaseService;

    private readonly List<CultureInfo> _supportedLanguages = new()
    {
        new CultureInfo("en"),
        new CultureInfo("pl"),
        new CultureInfo("de")
    };

    private bool _isInitializing = true;

    // JEDYNY, PRAWIDŁOWY KONSTRUKTOR (Obsługuje bazę i konfiguruje stronę)
    public SettingsPage(DatabaseService databaseService)
    {
        InitializeComponent();

        // Zapisujemy wstrzyknięty serwis bazy danych do lokalnego pola
        _databaseService = databaseService;

        // Odpalamy Twoje oryginalne metody ładujące dane do pickerów
        LoadLanguages();
        LoadThemes();
        LoadCurrency();

        // Zakończyliśmy początkowe ładowanie, od teraz Pickery mogą bezpiecznie reagować na zmiany
        _isInitializing = false;
    }

    private void LoadCurrency()
    {
        CurrencyPicker.Items.Clear();
        foreach (var c in Helpers.CurrencyHelper.GetSortedCurrencyDisplayList())
        {
            CurrencyPicker.Items.Add(c);
        }

        string savedCurrencyCode = Preferences.Default.Get("DefaultCurrency", "PLN");
        string displayToFind = Helpers.CurrencyHelper.FormatDisplay(savedCurrencyCode);

        int currencyIndex = CurrencyPicker.Items.IndexOf(displayToFind);
        CurrencyPicker.SelectedIndex = currencyIndex >= 0 ? currencyIndex : 0;
    }

    private void OnCurrencyChanged(object? sender, EventArgs e)
    {
        if (_isInitializing) return;

        if (CurrencyPicker.SelectedIndex != -1)
        {
            string? selectedDisplay = CurrencyPicker.SelectedItem?.ToString();

            if (selectedDisplay != null && selectedDisplay.Contains("──"))
            {
                string savedCurrencyCode = Preferences.Default.Get("DefaultCurrency", "PLN");
                string displayToFind = Helpers.CurrencyHelper.FormatDisplay(savedCurrencyCode);
                CurrencyPicker.SelectedIndex = CurrencyPicker.Items.IndexOf(displayToFind);
                return;
            }

            string? cleanCode = Helpers.CurrencyHelper.ExtractCode(selectedDisplay);

            if (!string.IsNullOrEmpty(cleanCode))
            {
                Preferences.Default.Set("DefaultCurrency", cleanCode);
            }
        }
    }

    private void LoadLanguages()
    {
        foreach (var culture in _supportedLanguages)
        {
            LanguagePicker.Items.Add(culture.NativeName);
        }

        var currentCulture = AppResources.Culture ?? CultureInfo.CurrentUICulture;
        var currentIndex = _supportedLanguages.FindIndex(c => c.TwoLetterISOLanguageName == currentCulture.TwoLetterISOLanguageName);

        if (currentIndex >= 0)
        {
            LanguagePicker.SelectedIndex = currentIndex;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_isInitializing) return;

        int selectedIndex = LanguagePicker.SelectedIndex;

        if (selectedIndex != -1)
        {
            var selectedCulture = _supportedLanguages[selectedIndex];
            Preferences.Default.Set("AppLanguage", selectedCulture.TwoLetterISOLanguageName);
            SetLanguage(selectedCulture);
        }
    }

    private void SetLanguage(CultureInfo culture)
    {
        if (AppResources.Culture?.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName)
            return;

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        AppResources.Culture = culture;

        if (this.Window != null)
        {
            this.Window.Page = new AppShell();
        }
        else if (Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0)
        {
            Microsoft.Maui.Controls.Application.Current.Windows[0].Page = new AppShell();
        }
    }

    private void LoadThemes()
    {
        ThemePicker.Items.Clear();
        ThemePicker.Items.Add(AppResources.ThemeLight);
        ThemePicker.Items.Add(AppResources.ThemeDark);
        ThemePicker.Items.Add(AppResources.ThemeHighContrast);

        ThemePicker.SelectedIndex = Preferences.Default.Get("AppTheme", 0);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_isInitializing) return;

        int selectedIndex = ThemePicker.SelectedIndex;
        if (selectedIndex == -1) return;

        Preferences.Default.Set("AppTheme", selectedIndex);
        App.ApplyTheme(selectedIndex);
    }

    private async void OnManageCategoriesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("CategoriesPage");
    }

    private async void OnManageProjectsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("ProjectsPage");
    }

    private async void OnManageExchangeRatesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("ExchangeRatesPage");
    }

    private async void OnManageFavoriteCurrenciesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("FavoriteCurrenciesPage");
    }

    private async void OnWipeDataClicked(object sender, EventArgs e)
    {
        bool firstWarning = await DisplayAlert(
            "Ostrzeżenie",
            "Czy na pewno chcesz usunąć wszystkie dane? Ta operacja jest nieodwracalna.",
            "Tak, usuń",
            "Anuluj");

        if (!firstWarning) return;

        bool finalWarning = await DisplayAlert(
            "OSTATNIE OSTRZEŻENIE",
            "Wszystkie konta, transakcje, kategorie i projekty zostaną trwale zniszczone. Kontynuować?",
            "ZNISZCZ DANE",
            "Anuluj");

        if (!finalWarning) return;

        // Teraz _databaseService na 100% nie jest nullem, operacja wykona się bezpiecznie!
        await _databaseService.WipeAllDataAsync();

        await DisplayAlert("Sukces", "Aplikacja została przywrócona do stanu fabrycznego.", "OK");

        // Zamiast resetować rdzeń aplikacji, po prostu płynnie wracamy na pusty ekran główny
        await Shell.Current.GoToAsync("//HomePage");
    }
}