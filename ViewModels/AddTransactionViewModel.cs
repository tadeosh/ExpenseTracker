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
        //public ObservableCollection<string> TransactionTypes { get; } = new();

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

        // Odpala się, gdy zmienimy typ transakcji
        partial void OnSelectedTypeIndexChanged(int value)
        {
            // Jeśli wybrano indeks 2 (Transfer), pokaż pole konta docelowego
            IsTransfer = value == 2;
            UpdateCurrencyConversionState();
        }

        // Odpala się, gdy zmienimy konto źródłowe
        partial void OnSelectedAccountChanged(Account? value)
        {
            UpdateCurrencyConversionState();
        }

        // Odpala się, gdy zmienimy konto docelowe
        partial void OnDestinationAccountChanged(Account? value)
        {
            UpdateCurrencyConversionState();
        }

        // Główna logika sprawdzająca, czy potrzebujemy przewalutowania
        private void UpdateCurrencyConversionState()
        {
            if (IsTransfer && SelectedAccount != null && DestinationAccount != null &&
                SelectedAccount.Currency != DestinationAccount.Currency)
            {
                IsCurrencyConversion = true;
                CurrencyConversionLabel = $"{SelectedAccount.Currency} -> {DestinationAccount.Currency}";
            }
            else
            {
                IsCurrencyConversion = false;
                CurrencyConversionLabel = string.Empty;
                ExchangeRateText = string.Empty; // Czyścimy pole kursu, gdy znika z ekranu
            }
        }

        public AddTransactionViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        // Metoda do załadowania list wyboru (wywoływana przez widok)
        public async Task LoadDataAsync()
        {
            // Ładowanie Kont
            var accountsFromDb = await _databaseService.GetAccountsAsync();
            Accounts.Clear();
            foreach (var acc in accountsFromDb) Accounts.Add(acc);

            // Ładowanie Kategorii
            var categoriesFromDb = await _databaseService.GetCategoriesAsync();
            Categories.Clear();
            foreach (var cat in categoriesFromDb) Categories.Add(cat);

            // Ładowanie Projektów
            var projectsFromDb = await _databaseService.GetProjectsAsync();
            Projects.Clear();
            foreach (var proj in projectsFromDb) Projects.Add(proj);
        }

        [RelayCommand]
        private async Task SaveTransactionAsync()
        {
            // Podstawowa walidacja
            if (!decimal.TryParse(AmountText, out decimal amount) || amount <= 0)
                return;

            if (string.IsNullOrWhiteSpace(DescriptionText) || SelectedAccount == null || SelectedCategory == null)
                return;

            // Mapowanie wybranego indeksu na Enum
            TransactionType type = SelectedTypeIndex switch
            {
                1 => TransactionType.Income,
                2 => TransactionType.Transfer,
                _ => TransactionType.Expense
            };

            // NOWOŚĆ: Walidacja dla transferu
            if (type == TransactionType.Transfer)
            {
                if (DestinationAccount == null) return;
                if (SelectedAccount.Id == DestinationAccount.Id) return; // Zabezpieczenie przed transferem na to samo konto
            }

            // NOWOŚĆ: Pobranie kursu wymiany, jeśli jest wymagany
            decimal? exchangeRate = null;
            if (IsCurrencyConversion)
            {
                if (!decimal.TryParse(ExchangeRateText, out decimal parsedRate) || parsedRate <= 0) return;
                exchangeRate = parsedRate;
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

            // Powiadomienie użytkownika i powrót do głównego ekranu (opcjonalne)
            //await Shell.Current.DisplayAlertAsync("Sukces", "Zapisano transakcję!", "OK");
            // DODAJ TO (Automatyczny skok do strony głównej):
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