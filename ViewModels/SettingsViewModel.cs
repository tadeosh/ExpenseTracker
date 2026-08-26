using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Helpers;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ExpenseTracker.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly ISettingsService _settingsService;

        // Listy dla Pickerów
        public ObservableCollection<string> Currencies { get; } = new();
        public ObservableCollection<string> Languages { get; } = new();
        public ObservableCollection<string> Themes { get; } = new();

        private readonly List<CultureInfo> _supportedCultures = new()
        {
            new CultureInfo("en"),
            new CultureInfo("pl"),
            new CultureInfo("de")
        };

        // Właściwości trzymające wybrany element
        [ObservableProperty]
        private string? _selectedCurrency = null!;

        [ObservableProperty]
        private string? _selectedLanguage = null!;

        [ObservableProperty]
        private string? _selectedTheme = null!;

        private bool _isInitializing = true;

        public SettingsViewModel(IDatabaseService databaseService, ISettingsService settingsService)
        {
            _databaseService = databaseService;
            _settingsService = settingsService;

            InitializeData();
        }

        private void InitializeData()
        {
            _isInitializing = true;

            // 1. Waluty
            foreach (var c in CurrencyHelper.GetSortedCurrencyDisplayList())
                Currencies.Add(c);

            string savedCurrencyCode = _settingsService.DefaultCurrency;
            SelectedCurrency = CurrencyHelper.FormatDisplay(savedCurrencyCode);

            // 2. Języki
            foreach (var culture in _supportedCultures)
                Languages.Add(culture.NativeName);

            var currentCulture = AppResources.Culture ?? CultureInfo.CurrentUICulture;
            var cultureMatch = _supportedCultures.FirstOrDefault(c => c.TwoLetterISOLanguageName == currentCulture.TwoLetterISOLanguageName);
            SelectedLanguage = cultureMatch != null ? cultureMatch.NativeName : Languages.First();

            // 3. Motywy
            Themes.Add(AppResources.ThemeLight);
            Themes.Add(AppResources.ThemeDark);
            Themes.Add(AppResources.ThemeHighContrast);

            int themeIndex = _settingsService.AppTheme;
            SelectedTheme = themeIndex >= 0 && themeIndex < Themes.Count ? Themes[themeIndex] : Themes[0];

            _isInitializing = false;
        }

        // Reagowanie na zmiany w Pickerach
        partial void OnSelectedCurrencyChanged(string? value)
        {
            if (_isInitializing || string.IsNullOrEmpty(value) || value.Contains("──")) return;

            string? cleanCode = CurrencyHelper.ExtractCode(value);
            if (!string.IsNullOrEmpty(cleanCode))
                _settingsService.DefaultCurrency = cleanCode;
        }

        partial void OnSelectedLanguageChanged(string? value)
        {
            if (_isInitializing || string.IsNullOrEmpty(value)) return;

            var selectedCulture = _supportedCultures.FirstOrDefault(c => c.NativeName == value);
            if (selectedCulture != null)
            {
                _settingsService.AppLanguage = selectedCulture.TwoLetterISOLanguageName;
                SetLanguage(selectedCulture);
            }
        }

        partial void OnSelectedThemeChanged(string? value)
        {
            if (_isInitializing || string.IsNullOrEmpty(value)) return;

            int index = Themes.IndexOf(value);
            if (index != -1)
            {
                _settingsService.AppTheme = index;
                App.ApplyTheme(index);
            }
        }

        private void SetLanguage(CultureInfo culture)
        {
            if (AppResources.Culture?.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName) return;

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            AppResources.Culture = culture;

            // W MAUI odświeżenie UI po zmianie języka wymaga zresetowania MainPage
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Application.Current?.Windows.Count > 0)
                    Application.Current.Windows[0].Page = new AppShell();
            });
        }

        // --- KOMENDY NAWIGACYJNE ---

        [RelayCommand]
        private async Task ManageCategoriesAsync() => await Shell.Current.GoToAsync("CategoriesPage");

        [RelayCommand]
        private async Task ManageProjectsAsync() => await Shell.Current.GoToAsync("ProjectsPage");

        [RelayCommand]
        private async Task ManageExchangeRatesAsync() => await Shell.Current.GoToAsync("ExchangeRatesPage");

        [RelayCommand]
        private async Task ManageFavoriteCurrenciesAsync() => await Shell.Current.GoToAsync("FavoriteCurrenciesPage");

        // --- KOMENDA ZAAWANSOWANA: WIPE DATA ---

        [RelayCommand]
        private async Task WipeDataAsync()
        {
            bool firstWarning = await Shell.Current.DisplayAlert(
                AppResources.WarningTitle,
                AppResources.WipeDataWarningMessage,
                AppResources.YesBtn,
                AppResources.CancelBtn);

            if (!firstWarning) return;

            bool finalWarning = await Shell.Current.DisplayAlert(
                AppResources.WipeDataFinalWarningTitle,
                AppResources.WipeDataFinalWarningMessage,
                AppResources.DestroyDataBtn,
                AppResources.CancelBtn);

            if (!finalWarning) return;

            try
            {
                await _databaseService.WipeAllDataAsync();
                await Shell.Current.DisplayAlert(AppResources.SuccessTitle, AppResources.WipeDataSuccessMessage, AppResources.OkBtn);
                await Shell.Current.GoToAsync("//HomePage");
            }
            catch (Exception ex)
            {
                // Dzięki temu, że zwracamy Task, błąd nie ubije aplikacji!
                await Shell.Current.DisplayAlert("Error", $"Data wipe failed: {ex.Message}", "OK");
            }
        }
    }
}