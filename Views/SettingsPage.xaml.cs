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

        // Zakończyliśmy początkowe ładowanie, od teraz Picker może reagować na kliknięcia użytkownika
        _isInitializing = false;
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
        ThemePicker.SelectedIndex = 0;
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
}