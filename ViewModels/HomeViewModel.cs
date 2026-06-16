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

                // Obliczanie całkowitego majątku
                if (acc.Currency == defaultCurrency)
                {
                    totalNetWorth += currentBalance;
                }
                else
                {
                    // Szukamy zapisanego kursu wymiany (np. Rate_EUR_PLN). Jeśli nie ma, liczymy awaryjnie 1:1
                    string rateKey = $"Rate_{acc.Currency}_{defaultCurrency}";
                    double exchangeRate = Preferences.Default.Get(rateKey, 1.0);
                    totalNetWorth += currentBalance * (decimal)exchangeRate;
                }
            }
            AccountsBalances = tempAccounts;

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