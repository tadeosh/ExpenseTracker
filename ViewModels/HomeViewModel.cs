using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using System.Collections.ObjectModel;
using ExpenseTracker.Helpers;
using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        public partial ObservableCollection<AccountBalanceItem> AccountsBalances { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<TransactionGroup> GroupedTransactions { get; set; } = new();

        [ObservableProperty]
        public partial bool HasNoTransactions { get; set; } = true;

        [ObservableProperty]
        public partial bool HasTransactions { get; set; } = false;

        [ObservableProperty]
        public partial string TotalNetWorthDisplay { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasMissingRates { get; set; } = false;

        private string _missingRatesMessage = string.Empty;

        public HomeViewModel(IDatabaseService databaseService, ISettingsService settingsService)
        {
            _databaseService = databaseService;
            _settingsService = settingsService;
        }

       
        // NOWY KOD:
        [RelayCommand]
        private async Task AccountTappedAsync(AccountBalanceItem? account)
        {
            if (account == null) return;

            // Bezpieczne przekazanie parametru przez słownik
            var navParams = new Dictionary<string, object>
            {
                // Klucz musi odpowiadać temu, co odbiera TransactionsViewModel
                { "AccountId", account.Id.ToString() }
            };

            // Bezpośredni, silnie typowany dostęp do "Id" - kompilator wie, czym jest account!
            //await Shell.Current.GoToAsync($"{RoutesHelper.AccountTransactionsRoute}?AccountId={account.Id}");
            // ZMIANA ARCHITEKTONICZNA: Dodajemy "///" aby przeskoczyć do innej gałęzi Shell
            await Shell.Current.GoToAsync(RoutesHelper.AccountTransactionsRoute, navParams);
        }

        public async Task LoadDataAsync()
        {
            // --- 1. BEZPIECZNE ŁADOWANIE KONT ---
            var accounts = await _databaseService.GetAccountsAsync();
            var accountDictionary = accounts.ToDictionary(a => a.Id, a => a.Name);

            //string defaultCurrency = Preferences.Default.Get("DefaultCurrency", "PLN");
            string defaultCurrency = _settingsService.DefaultCurrency;
            decimal totalNetWorth = 0;

            List<string> missingRatesList = new();
            var tempAccounts = new ObservableCollection<AccountBalanceItem>();

            var balances = await _databaseService.GetAllAccountBalancesAsync();

            foreach (var acc in accounts)
            {
                // decimal currentBalance = await _databaseService.GetAccountBalanceAsync(acc.Id);
                var currentBalance = balances.GetValueOrDefault(acc.Id);

                tempAccounts.Add(new AccountBalanceItem
                {
                    Id =acc.Id,
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

                    decimal exchangeRate = 1m;
                    if (rateFromDb.HasValue && rateFromDb.Value > 0)
                    {
                        exchangeRate = rateFromDb.Value;
                    }
                    else
                    {
                        missingRatesList.Add($"{acc.Currency} ➔ {defaultCurrency}");
                    }

                    totalNetWorth += currentBalance * (1m / exchangeRate);
                }
            }
            AccountsBalances = tempAccounts;

            // --- ZMIANA: Tłumaczenia z AppResources ---
            if (missingRatesList.Any())
            {
                HasMissingRates = true;
                var uniqueRates = missingRatesList.Distinct();

                // Zlepiamy dynamicznie przetłumaczony komunikat
                _missingRatesMessage = $"{AppResources.MissingRatesMsgPart1}\n\n"
                                       + string.Join("\n", uniqueRates)
                                       + $"\n\n{AppResources.MissingRatesMsgPart2}";
            }
            else
            {
                HasMissingRates = false;
                _missingRatesMessage = string.Empty;
            }

            TotalNetWorthDisplay = $"{totalNetWorth:N2} {defaultCurrency}";

            // --- 2. BEZPIECZNE ŁADOWANIE TRANSAKCJI ---
            var categories = await _databaseService.GetCategoriesAsync(includeArchived: true);
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
                            // --- ZMIANA: Tłumaczenie dla braku kategorii ---
                            CategoryName = t.CategoryId.HasValue && categoryDictionary.ContainsKey(t.CategoryId.Value) ? categoryDictionary[t.CategoryId.Value] : AppResources.CategoryNone,
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

            var tempGroups = new ObservableCollection<TransactionGroup>();
            foreach (var group in groupedData)
            {
                tempGroups.Add(group);
            }

            GroupedTransactions = tempGroups;
            HasNoTransactions = GroupedTransactions.Count == 0;
            HasTransactions = GroupedTransactions.Count > 0;
        }

        [RelayCommand]
        private async Task NavigateToAddTransactionAsync()
        {
            //await Shell.Current.GoToAsync("//AddTransactionPage");
            await Shell.Current.GoToAsync(RoutesHelper.AddTransactionPage);
        }

        [RelayCommand]
        private async Task ShowMissingRatesInfoAsync()
        {
            // --- ZMIANA: Tłumaczenia okienka z błędem ---
            await Shell.Current.DisplayAlertAsync(AppResources.MissingRatesTitle, _missingRatesMessage, AppResources.UnderstoodBtn);
        }
              
    }

    public class AccountBalanceItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    public class RecentTransactionItem
    {
        public string CategoryName { get; set; } = string.Empty;
        public string SubcategoryName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public string AccountDisplay { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;
        public string SubprojectName { get; set; } = string.Empty;

        public string AmountDisplay { get; set; } = string.Empty;
        public Color AmountColor { get; set; } = Colors.Gray;
    }

    public class TransactionGroup : ObservableCollection<RecentTransactionItem>
    {
        public DateTime Date { get; private set; }
        public string DateDisplay { get; set; } = string.Empty;
        public string DayOfWeekDisplay { get; set; } = string.Empty;

        public string TotalIncomeDisplay { get; set; } = string.Empty;
        public string TotalExpenseDisplay { get; set; } = string.Empty;

        public TransactionGroup(DateTime date, IEnumerable<RecentTransactionItem> items) : base(items)
        {
            Date = date;
        }
    }
}