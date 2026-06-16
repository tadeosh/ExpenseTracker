using ExpenseTracker.Resources.Strings;
//using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using System.Globalization;

namespace ExpenseTracker.Views;

public partial class SettingsPage : ContentPage
{
    private readonly List<CultureInfo> _supportedLanguages = new()
    {
        new CultureInfo("en"),
        new CultureInfo("pl"),
        new CultureInfo("de")
    };

    // NOWOŚĆ: Flaga informująca, czy strona jest w trakcie początkowego ładowania
    private bool _isInitializing = true;

    public SettingsPage()
    {
        InitializeComponent();
        LoadLanguages();
        LoadThemes();
        LoadCurrency();

        // Zakończyliśmy początkowe ładowanie, od teraz Picker może reagować na kliknięcia użytkownika
        _isInitializing = false;
    }

    // NOWOŚĆ: Metoda ładująca wybraną walutę z pamięci
    private void LoadCurrency()
    {
        string savedCurrency = Preferences.Default.Get("DefaultCurrency", "PLN");
        int currencyIndex = CurrencyPicker.Items.IndexOf(savedCurrency);

        // Jeśli znalazł zapisaną walutę na liście, ustawia ją. Jeśli nie (np. pierwsze uruchomienie), ustawia pierwszą z listy (PLN)
        CurrencyPicker.SelectedIndex = currencyIndex >= 0 ? currencyIndex : 0;
    }

    // NOWOŚĆ: Metoda reagująca na zmianę waluty w interfejsie
    private void OnCurrencyChanged(object? sender, EventArgs e)
    {
        if (_isInitializing) return;

        if (CurrencyPicker.SelectedIndex != -1)
        {
            Preferences.Default.Set("DefaultCurrency", CurrencyPicker.SelectedItem?.ToString());
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
        // NOWOŚĆ: Jeśli strona dopiero się ładuje, zignoruj to zdarzenie i wyjdź z metody
        if (_isInitializing) return;

        int selectedIndex = LanguagePicker.SelectedIndex;

        if (selectedIndex != -1)
        {
            var selectedCulture = _supportedLanguages[selectedIndex];

            // NOWOŚĆ: Zapisujemy język w pamięci telefonu przed przeładowaniem
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

        // NOWOŚĆ: Zabezpieczenie przed błędem NullReferenceException
        // Sprawdzamy, czy okno faktycznie istnieje, zanim spróbujemy w nim coś zmienić
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

        // Domyślnie ustawiamy Jasny (później nauczymy aplikację pamiętać ten wybór w bazie)
        ThemePicker.SelectedIndex = Preferences.Default.Get("AppTheme", 0);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_isInitializing) return;

        int selectedIndex = ThemePicker.SelectedIndex;
        if (selectedIndex == -1) return;

        // NOWOŚĆ: Zapisujemy wybór w pamięci telefonu
        Preferences.Default.Set("AppTheme", selectedIndex);

        // Wywołujemy naszą globalną metodę z App.xaml.cs
        App.ApplyTheme(selectedIndex);
    }

    
    private async void OnManageCategoriesClicked(object? sender, EventArgs e)
    {
        // Komenda Shell.Current.GoToAsync pozwala nam przeskoczyć do zarejestrowanej ścieżki
        await Shell.Current.GoToAsync("CategoriesPage");
    }

    private async void OnManageProjectsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("ProjectsPage");
    }
}

