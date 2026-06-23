using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Helpers;
using ExpenseTracker.Models;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    public partial class AccountsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        public ObservableCollection<Account> Accounts { get; } = new();

        // NOWY STANDARD AOT: public partial Typ NazwaZDużejLitery { get; set; }
        [ObservableProperty]
        public partial string AccountName { get; set; } = string.Empty;

        // Ta zmienna przyjmie z Pickera pełny tekst: np. "🇵🇱 PLN - Polski Złoty"
        [ObservableProperty]
        public partial string AccountCurrency { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AccountBalance { get; set; } = string.Empty;

        public AccountsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
                        
            //  LoadAccountsAsync();
        }

        [RelayCommand]
        private async Task AddAccountAsync()
        {
            // 1. Wyciąganie kodu
            string? cleanCurrencyCode = Helpers.CurrencyHelper.ExtractCode(AccountCurrency);

            // 2. Walidacja tekstowa Z KOMUNIKATEM
            if (string.IsNullOrWhiteSpace(AccountName) || string.IsNullOrWhiteSpace(cleanCurrencyCode))
            {
                await Shell.Current.DisplayAlert("Błąd", "Wprowadź nazwę konta i wybierz walutę.", "OK");
                return;
            }

            // 3. Kuloodporne parsowanie kwoty (zamienia przecinki na kropki i radzi sobie z każdą kulturą)
            string normalizedBalance = AccountBalance.Replace(",", ".");
            if (!decimal.TryParse(normalizedBalance, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal initialBalance))
            {
                await Shell.Current.DisplayAlert("Błąd", "Wprowadź poprawną kwotę (np. 100.00).", "OK");
                return;
            }

            var newAccount = new Account
            {
                Name = AccountName,
                Currency = cleanCurrencyCode,
                InitialBalance = initialBalance
            };

            // 4. Zapis do bazy i na listę
            await _databaseService.SaveAccountAsync(newAccount);

            // 5. BEZPIECZNA aktualizacja interfejsu (Wymuszenie Głównego Wątku)
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // Dodajemy na listę
                Accounts.Add(newAccount);

                // 5. Czyszczenie formularza i informacja o sukcesie
                AccountName = string.Empty;
                AccountBalance = string.Empty;

                // NAPRAWA: Bezpośrednio ustawiamy domyślną walutę, zapobiegając nieskończonej pętli z Pickerem
                string defaultCode = Preferences.Default.Get("DefaultCurrency", "PLN");
                AccountCurrency = CurrencyHelper.FormatDisplay(defaultCode);

                // Odkomentowałem Ci powiadomienie o sukcesie!
                //await Shell.Current.DisplayAlert("Sukces", "Konto zostało dodane!", "OK");
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