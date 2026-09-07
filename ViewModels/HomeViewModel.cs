using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ExpenseTracker.Data;
using ExpenseTracker.Helpers;
using ExpenseTracker.Messages;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly ISettingsService _settingsService;

        private bool _needsReload = true;

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

        // raport/wykresy
        [ObservableProperty]
        public partial ObservableCollection<ISeries> ExpenseSeries { get; set; } = new();

        [ObservableProperty]
        public partial string CurrentMonthTotalDisplay { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasExpensesThisMonth { get; set; } = false;
        // Sterowanie wizualne legendą (szary tekst omija domyślną, ciężką czerń)
        public SolidColorPaint LegendTextPaint { get; } = new SolidColorPaint(SKColors.DimGray);
        [ObservableProperty]
        public partial ObservableCollection<ExpenseLegendItem> ExpenseLegend { get; set; } = new();

        public HomeViewModel(IDatabaseService databaseService, ISettingsService settingsService)
        {
            _databaseService = databaseService;
            _settingsService = settingsService;
            WeakReferenceMessenger.Default.Register<HomeViewModel, TransactionsChangedMessage>(this, (r, m) =>
            {
                // Teraz kompilator wie, że 'r' to TransactionsViewModel, więc ma dostęp do pola
                r._needsReload = true;
            });
        }

        public void Receive(TransactionsChangedMessage message)
        {
            _needsReload = true;
        }

        public async Task LoadDataIfNeededAsync()
        {
            if (!_needsReload) return;
            await Task.Delay(300); // Ochrona animacji przejścia
            await LoadDataAsync();
            _needsReload = false;
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
            try
            {
                // --- 1. BEZPIECZNE ŁADOWANIE KONT ---
                var accounts = await _databaseService.GetAccountsAsync();
                var accountDictionary = accounts.ToDictionary(a => a.Id, a => a.Name);

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
                        Id = acc.Id,
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
                var transactions = await _databaseService.GetRecentTransactionsWithDetailsAsync(10);

                var groupedData = transactions
                    .GroupBy(t => t.Date.Date)
                    .Select(g =>
                    {
                        decimal dailyIncome = g.Where(x => x.Type == (int)TransactionType.Income).Sum(x => x.Amount);
                        decimal dailyExpense = g.Where(x => x.Type == (int)TransactionType.Expense).Sum(x => x.Amount);

                        var items = g.Select(t =>
                        {                            
                            return new TransactionDisplayItem
                            {
                                // Pełen obiekt bazy (potrzebny do Edycji/Usuwania)
                                Transaction = new Transaction
                                {
                                    Id = t.Id,
                                    Amount = t.Amount,
                                    Date = t.Date,
                                    Type = (TransactionType)t.Type,
                                    AccountId = t.AccountId
                                },
                                CategoryName = t.CategoryName == "-" ? AppResources.CategoryNone : t.CategoryName,
                                Description = t.Description,
                                AccountName = t.AccountName,
                                ShowAccount = true, // Zawsze pokazujemy konto na pulpicie
                                ProjectName = t.ProjectName == "-" ? "" : t.ProjectName,
                                SignedAmount = t.SignedAmount,
                                IsTransferIn = t.IsTransferIn,
                                Currency = t.AccountCurrency ?? ""
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
                    .ToList(); // OrderByDescending pominięto, bo zrobiliśmy to w SQL!

                var tempGroups = new ObservableCollection<TransactionGroup>();
                foreach (var group in groupedData)
                {
                    tempGroups.Add(group);
                }

                GroupedTransactions = tempGroups;
                HasNoTransactions = GroupedTransactions.Count == 0;
                HasTransactions = GroupedTransactions.Count > 0;

                // WYKRES ----------------------
                var monthExpenses = await _databaseService.GetCurrentMonthExpensesAsync();
                var categorySums = new Dictionary<string, (decimal Total, string Color)>();

                foreach (var expense in monthExpenses)
                {
                    // 1. Logika przewalutowania
                    decimal convertedAmount = expense.Amount;
                    if (expense.AccountCurrency != defaultCurrency)
                    {
                        var rateFromDb = await _databaseService.GetApplicableExchangeRateAsync(expense.AccountCurrency, defaultCurrency, expense.Date);
                        decimal exchangeRate = (rateFromDb.HasValue && rateFromDb.Value > 0) ? rateFromDb.Value : 1m;
                        convertedAmount = expense.Amount * (1m / exchangeRate);
                    }

                    // 2. Logika tłumaczenia "Brak Kategorii"
                    string catName = expense.CategoryName == "-" ? AppResources.CategoryNone : expense.CategoryName;

                    // 3. Grupowanie C# (szybkie działanie na Dictionary)
                    if (categorySums.ContainsKey(catName))
                    {
                        var current = categorySums[catName];
                        categorySums[catName] = (current.Total + convertedAmount, current.Color);
                    }
                    else
                    {
                        categorySums[catName] = (convertedAmount, expense.ColorHex);
                    }
                }

                // 4. Sortowanie i agregacja "Pozostałe" (Top 5 + Inne)
                var allSortedCategories = categorySums
                    .Select(kvp => new { Name = kvp.Key, Total = kvp.Value.Total, Color = kvp.Value.Color })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                var displayCategories = allSortedCategories.Take(4).ToList();
                var remainingCategories = allSortedCategories.Skip(4).ToList();

                // Jeśli mamy więcej niż 5 kategorii, zbijamy resztę w jedną
                if (remainingCategories.Any())
                {
                    decimal otherTotal = remainingCategories.Sum(x => x.Total);
                    displayCategories.Add(new
                    {
                        Name = AppResources.OtherCategories,
                        Total = otherTotal,
                        Color = "#B0BEC5" // Subtelny, chłodny szary (Material Blue Grey) dla grupy "Inne"
                    });
                }

                var tempSeries = new ObservableCollection<ISeries>();
                var tempLegend = new List<ExpenseLegendItem>(); // NOWOŚĆ
                decimal monthChartTotal = 0;

                foreach (var item in displayCategories)
                {
                    monthChartTotal += item.Total;

                    // 1. Ochrona SkiaSharp (Wykres)
                    if (!SKColor.TryParse(item.Color, out var parsedColor))
                    {
                        parsedColor = SKColors.Gray;
                    }

                    // 2. Ochrona MAUI (Legenda XAML)
                    Color dotColor = Colors.Gray;
                    if (Color.TryParse(item.Color, out var parsedMauiColor))
                    {
                        dotColor = parsedMauiColor;
                    }

                    tempSeries.Add(new PieSeries<decimal>
                    {
                        Values = new decimal[] { item.Total },
                        Name = item.Name,
                        Fill = new SolidColorPaint(parsedColor),
                        InnerRadius = 40, // Twoja nowa wartość
                        MaxRadialColumnWidth = 50,
                        // ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Model:N2} {defaultCurrency}" //tooltip
                    });

                    // Zasilamy natywną legendę
                    tempLegend.Add(new ExpenseLegendItem
                    {
                        Name = item.Name,
                        AmountDisplay = $"{item.Total:N2} {defaultCurrency}",
                        DotColor = dotColor
                    });
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    //ExpenseSeries.Clear();
                    //ExpenseLegend.Clear(); // Czyścimy starą legendę

                    //foreach (var series in tempSeries) ExpenseSeries.Add(series);
                    //foreach (var leg in tempLegend) ExpenseLegend.Add(leg); // Dodajemy nową
                    ExpenseSeries = new ObservableCollection<ISeries>(tempSeries);
                    ExpenseLegend = new ObservableCollection<ExpenseLegendItem>(tempLegend);

                    CurrentMonthTotalDisplay = $"{monthChartTotal:N2}\n{defaultCurrency}";
                    HasExpensesThisMonth = ExpenseSeries.Count > 0;
                });
            }
            catch (Exception ex)
            {
                // Logowanie błędu do konsoli lub pliku logów
                Console.WriteLine($"Error loading data: {ex.Message}");
                await Shell.Current.DisplayAlertAsync(AppResources.ErrorTitle, AppResources.ErrorLoadingData, AppResources.UnderstoodBtn);
            }
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

        //=============================CRUD=========================
        [RelayCommand]
        private async Task EditTransactionAsync(TransactionDisplayItem item)
        {
            if (item?.Transaction == null) return;
            var navParams = new Dictionary<string, object> { { "TransactionId", item.Transaction.Id.ToString() } };
            await Shell.Current.GoToAsync(RoutesHelper.AddTransactionPage, navParams);
        }

        [RelayCommand]
        private async Task DeleteTransactionAsync(TransactionDisplayItem item)
        {
            if (item?.Transaction == null) return;

            bool isConfirmed = await Shell.Current.DisplayAlertAsync(
                AppResources.WarningTitle, AppResources.DeleteConfirmationText, AppResources.YesBtn, AppResources.CancelBtn);

            if (!isConfirmed) return;

            try
            {
                await _databaseService.DeleteTransactionAsync(item.Transaction);

                // Szukanie i usuwanie transakcji z odpowiedniej grupy na Dashboardzie
                var group = GroupedTransactions.FirstOrDefault(g => g.Contains(item));
                if (group != null)
                {
                    group.Remove(item);
                    if (group.Count == 0) GroupedTransactions.Remove(group); // Usunięcie dnia, jeśli pusty
                    HasTransactions = GroupedTransactions.Any();
                    HasNoTransactions = !HasTransactions;
                }

                // Opcjonalnie: Przeładowanie NetWorth
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }

    //Pomocnicza klasa do legendy wykresu
    public class ExpenseLegendItem
    {
        public string Name { get; set; } = string.Empty;
        public string AmountDisplay { get; set; } = string.Empty;
        public Color DotColor { get; set; } = Colors.Gray;
    }
    public class AccountBalanceItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    //public class RecentTransactionItem
    //{
    //    public string CategoryName { get; set; } = string.Empty;
    //    public string SubcategoryName { get; set; } = string.Empty;

    //    public string Description { get; set; } = string.Empty;
    //    public string AccountDisplay { get; set; } = string.Empty;

    //    public string ProjectName { get; set; } = string.Empty;
    //    public string SubprojectName { get; set; } = string.Empty;

    //    public string AmountDisplay { get; set; } = string.Empty;
    //    //public Color AmountColor { get; set; } = Colors.Gray;
    //    // ZMIANA: Zamiast sztywnego 'Color', przekazujemy Enum do decyzji interfejsu
    //    public TransactionType Type { get; set; }
    //}

    public class TransactionGroup : ObservableCollection<TransactionDisplayItem>
    {
        public DateTime Date { get; private set; }
        public string DateDisplay { get; set; } = string.Empty;
        public string DayOfWeekDisplay { get; set; } = string.Empty;

        public string TotalIncomeDisplay { get; set; } = string.Empty;
        public string TotalExpenseDisplay { get; set; } = string.Empty;

        public TransactionGroup(DateTime date, IEnumerable<TransactionDisplayItem> items) : base(items)
        {
            Date = date;
        }
    }
}