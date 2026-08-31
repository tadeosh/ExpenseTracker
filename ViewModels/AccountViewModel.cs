using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Helpers;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings; // NOWOŚĆ: Referencja do tłumaczeń
using ExpenseTracker.Services.Interfaces;
using FluentValidation;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ExpenseTracker.ViewModels
{
    public partial class AccountsViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly IValidator<AccountsViewModel> _validator;
        private readonly ISettingsService _settingsService; // ZMIANA: Wstrzykujemy serwis ustawień

        public ObservableCollection<Account> Accounts { get; } = new();

        // NOWY STANDARD AOT: public partial Typ NazwaZDużejLitery { get; set; }
        [ObservableProperty]
        public partial string AccountName { get; set; } = string.Empty;

        // Ta zmienna przyjmie z Pickera pełny tekst: np. "🇵🇱 PLN - Polski Złoty"
        [ObservableProperty]
        public partial string AccountCurrency { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AccountBalance { get; set; } = string.Empty;

        // NOWOŚĆ: Pola do wyświetlania błędów w UI
        [ObservableProperty] public partial string? AccountNameError { get; set; }
        [ObservableProperty] public partial string? AccountCurrencyError { get; set; }
        [ObservableProperty] public partial string? AccountBalanceError { get; set; }

        public AccountsViewModel(IDatabaseService databaseService,IValidator<AccountsViewModel> validator,ISettingsService settingsService)
        {
            _databaseService = databaseService;
            _validator = validator;
            _settingsService = settingsService;
        }

        private void ClearErrors()
        {
            AccountNameError = AccountCurrencyError = AccountBalanceError = null;
        }



        [RelayCommand]
        private async Task AddAccountAsync()
        {
            ClearErrors();

            // 1. Walidacja FluentValidation
            var validationResult = await _validator.ValidateAsync(this);

            if (!validationResult.IsValid)
            {
                // 2. Mapowanie błędów do widoku (UI)
                foreach (var error in validationResult.Errors)
                {
                    switch (error.PropertyName)
                    {
                        case nameof(AccountName): AccountNameError = error.ErrorMessage; break;
                        case nameof(AccountCurrency): AccountCurrencyError = error.ErrorMessage; break;
                        case nameof(AccountBalance): AccountBalanceError = error.ErrorMessage; break;
                    }
                }
                return; // Przerywamy zapis, interfejs pokaże czerwone etykiety
            }

            // 3. Ekstrakcja i parsowanie z gwarancją sukcesu
            string? cleanCurrencyCode = CurrencyHelper.ExtractCode(AccountCurrency);
            decimal initialBalance = decimal.Parse(AccountBalance.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture);

            var newAccount = new Account
            {
                Name = AccountName,
                Currency = cleanCurrencyCode,
                InitialBalance = initialBalance
            };

            // 4. Zapis do bazy
            await _databaseService.SaveAccountAsync(newAccount);

            // 5. Aktualizacja UI w głównym wątku
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Accounts.Add(newAccount);

                AccountName = string.Empty;
                AccountBalance = string.Empty;

                // ZMIANA: Używamy wstrzykniętego serwisu, omijając Preferences.Default
                string defaultCode = _settingsService.DefaultCurrency;
                AccountCurrency = CurrencyHelper.FormatDisplay(defaultCode);

                ClearErrors();
            });
        }

        public async Task LoadAccountsAsync()
        {
            var accountsFromDb = await _databaseService.GetAccountsAsync();
            Accounts.Clear();
            foreach (var acc in accountsFromDb)
            {
                Accounts.Add(acc);
            }
        }
    }
}