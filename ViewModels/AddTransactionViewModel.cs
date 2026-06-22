using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    public partial class AddTransactionViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        // Listy wyboru dla Pickerów na ekranie
        public ObservableCollection<Account> Accounts { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<Project> Projects { get; } = new();

        // Ukryta zmienna do zapamiętania kursu z bazy (żeby wiedzieć, czy użytkownik go zmienił)
        private decimal? _lastFetchedRate = null;

        // Pola powiązane z formularzem wprowadzania
        [ObservableProperty]
        public partial string AmountText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial DateTime SelectedDate { get; set; } = DateTime.Today;

        [ObservableProperty]
        public partial string DescriptionText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial Account? SelectedAccount { get; set; }

        [ObservableProperty]
        public partial Category? SelectedCategory { get; set; }

        [ObservableProperty]
        public partial Project? SelectedProject { get; set; }

        [ObservableProperty]
        public partial int SelectedTypeIndex { get; set; } = 0; // Domyślnie wydatek

        [ObservableProperty]
        public partial Account? DestinationAccount { get; set; }

        [ObservableProperty]
        public partial string ExchangeRateText { get; set; } = string.Empty;

        // Flagi decydujące o tym, czy pola są widoczne na ekranie
        [ObservableProperty]
        public partial bool IsTransfer { get; set; }

        [ObservableProperty]
        public partial bool IsCurrencyConversion { get; set; }

        [ObservableProperty]
        public partial string CurrencyConversionLabel { get; set; } = string.Empty;

        public AddTransactionViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        // ==========================================
        // NASŁUCHIWACZE (CZYSTE, BEZ DUPLIKATÓWI)
        // ==========================================

        partial void OnSelectedTypeIndexChanged(int value)
        {
            IsTransfer = value == 2;
            CheckCurrencyConversion();
        }

        partial void OnSelectedAccountChanged(Account? value) => CheckCurrencyConversion();
        partial void OnDestinationAccountChanged(Account? value) => CheckCurrencyConversion();
        partial void OnSelectedDateChanged(DateTime value) => CheckCurrencyConversion();

        // Inteligentna logika sprawdzająca i pobierająca kurs
        private async void CheckCurrencyConversion()
        {
            if (IsTransfer && SelectedAccount != null && DestinationAccount != null && SelectedAccount.Currency != DestinationAccount.Currency)
            {
                IsCurrencyConversion = true;
                CurrencyConversionLabel = $"{SelectedAccount.Currency} -> {DestinationAccount.Currency}";

                // Szukamy w bazie
                var rate = await _databaseService.GetApplicableExchangeRateAsync(
                    SelectedAccount.Currency,
                    DestinationAccount.Currency,
                    SelectedDate);

                if (rate.HasValue)
                {
                    ExchangeRateText = rate.Value.ToString("0.####");
                    _lastFetchedRate = rate.Value;
                }
                else
                {
                    ExchangeRateText = string.Empty;
                    _lastFetchedRate = null;
                }
            }
            else
            {
                IsCurrencyConversion = false;
                CurrencyConversionLabel = string.Empty;
                ExchangeRateText = string.Empty;
                _lastFetchedRate = null;
            }
        }

        // ==========================================
        // ŁADOWANIE DANYCH
        // ==========================================

        public async Task LoadDataAsync()
        {
            var accountsFromDb = await _databaseService.GetAccountsAsync();
            Accounts.Clear();
            foreach (var acc in accountsFromDb) Accounts.Add(acc);

            var categoriesFromDb = await _databaseService.GetCategoriesAsync();
            Categories.Clear();
            foreach (var cat in categoriesFromDb) Categories.Add(cat);

            var projectsFromDb = await _databaseService.GetProjectsAsync();
            Projects.Clear();
            foreach (var proj in projectsFromDb) Projects.Add(proj);
        }

        // ==========================================
        // ZAPIS TRANSAKCJI + UCZENIE KURSÓW
        // ==========================================

        [RelayCommand]
        private async Task SaveTransactionAsync()
        {
            if (!decimal.TryParse(AmountText, out decimal amount) || amount <= 0) return;
            if (string.IsNullOrWhiteSpace(DescriptionText) || SelectedAccount == null || SelectedCategory == null) return;

            TransactionType type = SelectedTypeIndex switch
            {
                1 => TransactionType.Income,
                2 => TransactionType.Transfer,
                _ => TransactionType.Expense
            };

            if (type == TransactionType.Transfer)
            {
                if (DestinationAccount == null || SelectedAccount.Id == DestinationAccount.Id) return;
            }

            decimal? exchangeRate = null;
            if (IsCurrencyConversion)
            {
                if (!decimal.TryParse(ExchangeRateText, out decimal parsedRate) || parsedRate <= 0) return;
                exchangeRate = parsedRate;

                // --- AUTOMATYCZNE UCZENIE SIĘ KURSÓW WALUT ---
                if (_lastFetchedRate == null || _lastFetchedRate.Value != parsedRate)
                {
                    var newLearnedRate = new ExchangeRate
                    {
                        SourceCurrency = SelectedAccount.Currency,
                        TargetCurrency = DestinationAccount.Currency,
                        Rate = parsedRate,
                        Date = SelectedDate
                    };
                    await _databaseService.SaveExchangeRateAsync(newLearnedRate);
                }
            }

            var transaction = new Transaction
            {
                Amount = amount,
                Date = SelectedDate,
                Description = DescriptionText,
                Type = type,
                AccountId = SelectedAccount.Id,
                CategoryId = SelectedCategory.Id,
                ProjectId = SelectedProject?.Id,
                DestinationAccountId = type == TransactionType.Transfer ? DestinationAccount?.Id : null,
                ExchangeRate = exchangeRate
            };

            await _databaseService.SaveTransactionAsync(transaction);

            // Czyszczenie formularza
            AmountText = string.Empty;
            DescriptionText = string.Empty;
            SelectedAccount = null;
            DestinationAccount = null;
            SelectedCategory = null;
            SelectedProject = null;
            ExchangeRateText = string.Empty;

            await Shell.Current.GoToAsync("//HomePage");
        }

        [RelayCommand]
        private void SetTransactionType(string typeIndex)
        {
            if (int.TryParse(typeIndex, out int index))
            {
                SelectedTypeIndex = index;
            }
        }
    }
}