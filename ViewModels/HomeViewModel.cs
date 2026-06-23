using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        // NOWOŚĆ: Dodajemy [ObservableProperty], żeby móc w kodzie hurtowo podmieniać całe listy (zamiast Clear i Add)
        [ObservableProperty]
        public partial ObservableCollection<AccountBalanceItem> AccountsBalances { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<TransactionGroup> GroupedTransactions { get; set; } = new();

        [ObservableProperty]
        public partial bool HasNoTransactions { get; set; } = true;

        // NOWOŚĆ: Flaga dla bezpiecznego ukrywania CollectionView na Windowsie
        [ObservableProperty]
        public partial bool HasTransactions { get; set; } = false;

        [ObservableProperty]
        public partial string TotalNetWorthDisplay { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasMissingRates { get; set; } = false;

        // Tutaj będziemy trzymać treść błędu do wyświetlenia
        private string _missingRatesMessage = string.Empty;

        public HomeViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task LoadDataAsync()
        {
            // --- 1. BEZPIECZNE ŁADOWANIE KONT ---
            // --- 1. BEZPIECZNE ŁADOWANIE KONT ---
            var accounts = await _databaseService.GetAccountsAsync();
            var accountDictionary = accounts.ToDictionary(a => a.Id, a => a.Name);

            // Pobieramy domyślną walutę z ustawień
            string defaultCurrency = Preferences.Default.Get("DefaultCurrency", "PLN");
            decimal totalNetWorth = 0;

            // NOWOŚĆ: Lista do łapania brakujących kursów
            List<string> missingRatesList = new();

            var tempAccounts = new ObservableCollection<AccountBalanceItem>();
            foreach (var acc in accounts)
            {
                decimal currentBalance = await _databaseService.GetAccountBalanceAsync(acc.Id);
                tempAccounts.Add(new AccountBalanceItem
                {
                    Name = acc.Name,
                    Balance = currentBalance,
                    Currency = acc.Currency
                });

                if (acc.Currency == defaultCurrency)
                {
                    totalNetWorth += currentBalance;
                }
                else
                {
                    var rateFromDb = await _databaseService.GetApplicableExchangeRateAsync(acc.Currency, defaultCurrency, DateTime.Today);

                    decimal exchangeRate = 1m; // Awaryjnie 1:1
                    if (rateFromDb.HasValue && rateFromDb.Value > 0)
                    {
                        exchangeRate = rateFromDb.Value;
                    }
                    else
                    {
                        // NOWOŚĆ: Nie znaleźliśmy kursu! Zapisujemy to na czarną listę.
                        missingRatesList.Add($"{acc.Currency} ➔ {defaultCurrency}");
                    }

                    totalNetWorth += currentBalance * (1m / exchangeRate); // Nasza perfekcyjna matematyka
                }
            }
            AccountsBalances = tempAccounts;

            // NOWOŚĆ: Podsumowanie brakujących kursów
            if (missingRatesList.Any())
            {
                HasMissingRates = true;

                // Usuwamy duplikaty (np. gdyby użytkownik miał 2 konta w USD, a brakuje kursu USD->PLN)
                var uniqueRates = missingRatesList.Distinct();

                _missingRatesMessage = "Aplikacja użyła awaryjnego przelicznika 1:1 dla następujących walut, ponieważ brakuje ich kursów w bazie:\n\n"
                                       + string.Join("\n", uniqueRates)
                                       + "\n\nDodaj brakujące kursy w ustawieniach, aby majątek liczył się w 100% poprawnie.";
            }
            else
            {
                HasMissingRates = false;
                _missingRatesMessage = string.Empty;
            }

            // Formatowanie wyświetlania Całkowitego Majątku
            TotalNetWorthDisplay = $"{totalNetWorth:N2} {defaultCurrency}";


            // --- 2. BEZPIECZNE ŁADOWANIE TRANSAKCJI ---
            var categories = await _databaseService.GetCategoriesAsync();
            var categoryDictionary = categories.ToDictionary(c => c.Id, c => c.Name);

            var projects = await _databaseService.GetProjectsAsync();
            var projectDictionary = projects.ToDictionary(p => p.Id, p => p.Name);

            var transactions = await _databaseService.GetRecentTransactionsAsync(30);

            var groupedData = transactions
                .GroupBy(t => t.Date.Date)
                .Select(g =>
                {
                    decimal dailyIncome = g.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
                    decimal dailyExpense = g.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);

                    var items = g.Select(t =>
                    {
                        Color amountColor = t.Type switch
                        {
                            TransactionType.Income => Color.FromArgb("#4CAF50"),
                            TransactionType.Expense => Color.FromArgb("#E53935"),
                            _ => Color.FromArgb("#1E88E5")
                        };
                        string amountPrefix = t.Type == TransactionType.Income ? "+ " : (t.Type == TransactionType.Expense ? "- " : "");

                        string accName = accountDictionary.ContainsKey(t.AccountId) ? accountDictionary[t.AccountId] : "?";
                        if (t.Type == TransactionType.Transfer && t.DestinationAccountId.HasValue)
                        {
                            string destName = accountDictionary.ContainsKey(t.DestinationAccountId.Value) ? accountDictionary[t.DestinationAccountId.Value] : "?";
                            accName = $"{accName} ➔ {destName}";
                        }

                        return new RecentTransactionItem
                        {
                            CategoryName = t.CategoryId.HasValue && categoryDictionary.ContainsKey(t.CategoryId.Value) ? categoryDictionary[t.CategoryId.Value] : "Brak",
                            SubcategoryName = "",
                            Description = t.Description,
                            AccountDisplay = accName,
                            ProjectName = t.ProjectId.HasValue && projectDictionary.ContainsKey(t.ProjectId.Value) ? projectDictionary[t.ProjectId.Value] : "",
                            SubprojectName = "",
                            AmountDisplay = $"{amountPrefix}{t.Amount:N2}",
                            AmountColor = amountColor
                        };
                    });

                    return new TransactionGroup(g.Key, items)
                    {
                        DateDisplay = g.Key.ToString("dd.MM.yyyy"),
                        DayOfWeekDisplay = g.Key.ToString("ddd"),
                        TotalIncomeDisplay = dailyIncome > 0 ? $"+ {dailyIncome:N2}" : "",
                        TotalExpenseDisplay = dailyExpense > 0 ? $"- {dailyExpense:N2}" : ""
                    };
                })
                .OrderByDescending(g => g.Date)
                .ToList();

            // Tworzymy tymczasową listę grup
            var tempGroups = new ObservableCollection<TransactionGroup>();
            foreach (var group in groupedData)
            {
                tempGroups.Add(group);
            }

            // Zastępujemy starą listę nową za jednym zamachem
            GroupedTransactions = tempGroups;

            // Ustawiamy flagi widoczności
            HasNoTransactions = GroupedTransactions.Count == 0;
            HasTransactions = GroupedTransactions.Count > 0;
        }

        [RelayCommand]
        private async Task NavigateToAddTransactionAsync()
        {
            await Shell.Current.GoToAsync("//AddTransactionPage");
        }

        [RelayCommand]
        private async Task ShowMissingRatesInfoAsync()
        {
            await Shell.Current.DisplayAlert("Brakujące kursy walut", _missingRatesMessage, "Zrozumiałem");
        }
    }

    public class AccountBalanceItem
    {
        public string Name { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    // DTO dla pojedynczej transakcji na liście
    public class RecentTransactionItem
    {
        public string CategoryName { get; set; } = string.Empty;
        public string SubcategoryName { get; set; } = string.Empty; // Na przyszłość

        public string Description { get; set; } = string.Empty;
        public string AccountDisplay { get; set; } = string.Empty; // Obsłuży format "A -> B"

        public string ProjectName { get; set; } = string.Empty;
        public string SubprojectName { get; set; } = string.Empty;  // Na przyszłość

        public string AmountDisplay { get; set; } = string.Empty;
        public Color AmountColor { get; set; } = Colors.Gray;
    }

    // NOWOŚĆ: Klasa reprezentująca Grupę (jeden dzień)
    public class TransactionGroup : ObservableCollection<RecentTransactionItem>
    {
        public DateTime Date { get; private set; }
        public string DateDisplay { get; set; } = string.Empty;
        public string DayOfWeekDisplay { get; set; } = string.Empty;

        // Sumy dzienne
        public string TotalIncomeDisplay { get; set; } = string.Empty;
        public string TotalExpenseDisplay { get; set; } = string.Empty;

        // Konstruktor przyjmujący listę elementów do grupy
        public TransactionGroup(DateTime date, IEnumerable<RecentTransactionItem> items) : base(items)
        {
            Date = date;
        }
    }


}