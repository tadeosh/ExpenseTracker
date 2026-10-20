using ExpenseTracker.Helpers;
using ExpenseTracker.Models;
using ExpenseTracker.Models.Reports;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using System.Collections.Concurrent;

namespace ExpenseTracker.Services
{
    internal enum TrendGrouping
    {
        Day,
        Week,
        Month
    }
    public class ReportService : IReportService
    {
        private readonly IDatabaseService _databaseService;

        public ReportService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        private static int ResolveTopLevelCategoryId(int? categoryId, IReadOnlyDictionary<int, Category> categoryById)
        {
            if (!categoryId.HasValue ||
                categoryId.Value == 0)
            {
                return 0;
            }

            if (!categoryById.TryGetValue(categoryId.Value,out var category))
            {
                // Transakcja wskazuje nieistniejącą kategorię.
                // Zachowujemy ją jako "Bez kategorii", zamiast
                // przypisywać do przypadkowego rodzica.
                return 0;
            }

            if (!category.ParentId.HasValue)
                return category.Id;

            if (categoryById.ContainsKey(category.ParentId.Value))
            {
                return category.ParentId.Value;
            }

            // Osierocona podkategoria: jej ParentId wskazuje rekord,
            // którego już nie ma. Nie tracimy jej wydatków, tylko
            // traktujemy ją jako samodzielną kategorię główną.
            return category.Id;
        }

        private async Task<decimal> ConvertReportAmountAsync(decimal amount,int accountId,DateTime transactionDate,string baseCurrency,IReadOnlyDictionary<int, string> accountCurrencyById,
            IDictionary<(string SourceCurrency,string TargetCurrency,DateTime Date),decimal?> exchangeRateCache)
        {
            var normalizedBaseCurrency =
                baseCurrency
                    .Trim()
                    .ToUpperInvariant();

            var sourceCurrency =
                accountCurrencyById.TryGetValue(
                    accountId,
                    out var accountCurrency)
                    ? accountCurrency
                        .Trim()
                        .ToUpperInvariant()
                    : normalizedBaseCurrency;

            if (string.Equals(
                    sourceCurrency,
                    normalizedBaseCurrency,
                    StringComparison.OrdinalIgnoreCase))
            {
                return amount;
            }

            var cacheKey = (
                SourceCurrency: sourceCurrency,
                TargetCurrency: normalizedBaseCurrency,
                Date: transactionDate.Date);

            if (!exchangeRateCache.TryGetValue(
                    cacheKey,
                    out var exchangeRate))
            {
                exchangeRate =
                    await _databaseService
                        .GetApplicableExchangeRateAsync(
                            sourceCurrency,
                            normalizedBaseCurrency,
                            transactionDate.Date);

                exchangeRateCache[cacheKey] =
                    exchangeRate;
            }

            if (!exchangeRate.HasValue ||
                exchangeRate.Value <= 0m)
            {
                // Zachowujemy dotychczasowe zachowanie raportu:
                // brak kursu nie usuwa transakcji z zestawienia.
                // Osobny kontrakt błędów kursowych uporządkujemy
                // wspólnie dla wszystkich raportów.
                return amount;
            }

            return amount *
                   (1m / exchangeRate.Value);
        }

        public async Task<List<CategorySummary>> GetCategoryBreakdownAsync(ReportFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(context.BaseCurrency);

            var transactions =
                await _databaseService.GetFilteredTransactionsAsync(context.AccountId,context.CategoryId,context.ProjectId,null,null,context.StartDate,context.EndDate);

            var targetType =
                context.TransactionType ??
                TransactionType.Expense;

            var relevantTransactions = transactions
                .Where(transaction =>
                    transaction.Type == targetType)
                .ToList();

            if (relevantTransactions.Count == 0)
                return [];

            // Pobieramy również kategorie archiwalne, ponieważ archiwizacja
            // nie może zmienić historycznych raportów.
            var categories =
                await _databaseService.GetCategoriesAsync(
                    includeArchived: true);

            // Zamknięte konto również może posiadać historyczne operacje.
            // Potrzebujemy jego waluty do prawidłowego przewalutowania.
            var accounts =
                await _databaseService.GetAccountsAsync(
                    includeClosed: true);

            var categoryById = categories.ToDictionary(
                category => category.Id);

            var accountCurrencyById = accounts.ToDictionary(
                account => account.Id,
                account => account.Currency);

            var categoryTotals =
                new Dictionary<int, decimal>();

            var exchangeRateCache =
                new Dictionary<
                    (
                        string SourceCurrency,
                        string TargetCurrency,
                        DateTime Date
                    ),
                    decimal?>();

            decimal totalAmount = 0m;

            foreach (var transaction in relevantTransactions)
            {
                var convertedAmount =
                    await ConvertReportAmountAsync(
                        transaction.Amount,
                        transaction.AccountId,
                        transaction.Date,
                        context.BaseCurrency,
                        accountCurrencyById,
                        exchangeRateCache);

                var topLevelCategoryId =
                    ResolveTopLevelCategoryId(
                        transaction.CategoryId,
                        categoryById);

                if (categoryTotals.TryGetValue(
                        topLevelCategoryId,
                        out var currentTotal))
                {
                    categoryTotals[topLevelCategoryId] =
                        currentTotal + convertedAmount;
                }
                else
                {
                    categoryTotals[topLevelCategoryId] =
                        convertedAmount;
                }

                totalAmount += convertedAmount;
            }

            if (totalAmount == 0m)
                return [];

            return categoryTotals
                .Where(item => item.Value != 0m)
                .Select(item =>
                {
                    if (item.Key == 0)
                    {
                        return new CategorySummary(
                            CategoryId: 0,
                            CategoryName: AppResources.CategoryNone,
                            ColorHex: "#808080",
                            TotalAmount: item.Value,
                            Percentage:
                                (double)(
                                    item.Value /
                                    totalAmount *
                                    100m));
                    }

                    if (!categoryById.TryGetValue(
                            item.Key,
                            out var category))
                    {
                        return new CategorySummary(
                            CategoryId: item.Key,
                            CategoryName:
                                AppResources.CategoryNone,
                            ColorHex: "#808080",
                            TotalAmount: item.Value,
                            Percentage:
                                (double)(
                                    item.Value /
                                    totalAmount *
                                    100m));
                    }

                    return new CategorySummary(
                        CategoryId: category.Id,
                        CategoryName: category.Name,
                        ColorHex: category.ColorHex,
                        TotalAmount: item.Value,
                        Percentage:
                            (double)(
                                item.Value /
                                totalAmount *
                                100m));
                })
                .OrderByDescending(item =>
                    item.TotalAmount)
                .ToList();
        }

        public async Task<List<CategorySummary>> GetCategoryDrillDownAsync(ReportFilterContext context, int parentCategoryId)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(context.BaseCurrency);

            var categories = await _databaseService.GetCategoriesAsync(includeArchived: true);
            var parentCategory = categories.FirstOrDefault(category => category.Id == parentCategoryId);

            if (parentCategory is null)
                return [];

            var childCategories = categories.Where(category => category.ParentId == parentCategoryId).ToList();
            var relevantCategoryIds = childCategories.Select(category => category.Id).ToHashSet();
            relevantCategoryIds.Add(parentCategoryId);

            // Celowo nie przekazujemy context.CategoryId.
            // Drill-down sam kontroluje, które elementy drzewa kategorii należą do wyniku.
            var transactions = await _databaseService.GetFilteredTransactionsAsync(context.AccountId, null, context.ProjectId, null, null, context.StartDate, context.EndDate);

            var targetType = context.TransactionType ?? TransactionType.Expense;

            var relevantTransactions = transactions
                .Where(transaction => transaction.Type == targetType)
                .Where(transaction => transaction.CategoryId.HasValue && relevantCategoryIds.Contains(transaction.CategoryId.Value))
                .ToList();

            if (relevantTransactions.Count == 0)
                return [];

            var accounts = await _databaseService.GetAccountsAsync(includeClosed: true);
            var accountCurrencyById = accounts.ToDictionary(account => account.Id, account => account.Currency);
            var categoryById = categories.ToDictionary(category => category.Id);
            var categoryTotals = new Dictionary<int, decimal>();
            var exchangeRateCache = new Dictionary<(string SourceCurrency, string TargetCurrency, DateTime Date), decimal?>();

            decimal totalAmount = 0m;

            foreach (var transaction in relevantTransactions)
            {
                var convertedAmount = await ConvertReportAmountAsync(transaction.Amount, transaction.AccountId, transaction.Date, context.BaseCurrency, accountCurrencyById, exchangeRateCache);

                // Transakcja przypisana bezpośrednio do rodzica trafia
                // do syntetycznej pozycji "Bez podkategorii" (Id = 0).
                var resultCategoryId = transaction.CategoryId == parentCategoryId ? 0 : transaction.CategoryId!.Value;

                if (categoryTotals.TryGetValue(resultCategoryId, out var currentTotal))
                    categoryTotals[resultCategoryId] = currentTotal + convertedAmount;
                else
                    categoryTotals[resultCategoryId] = convertedAmount;

                totalAmount += convertedAmount;
            }

            if (totalAmount == 0m)
                return [];

            return categoryTotals
                .Where(item => item.Value != 0m)
                .Select(item =>
                {
                    if (item.Key == 0)
                    {
                        return new CategorySummary(
                            CategoryId: 0,
                            CategoryName: AppResources.Reports_NoSubcategory,
                            ColorHex: "#808080",
                            TotalAmount: item.Value,
                            Percentage: (double)(item.Value / totalAmount * 100m));
                    }

                    var category = categoryById[item.Key];

                    return new CategorySummary(
                        CategoryId: category.Id,
                        CategoryName: category.Name,
                        ColorHex: category.ColorHex,
                        TotalAmount: item.Value,
                        Percentage: (double)(item.Value / totalAmount * 100m));
                })
                .OrderByDescending(item => item.TotalAmount)
                .ToList();
        }

        // ================= Projekty==========================
        public async Task<List<ProjectSummary>> GetProjectBreakdownAsync(ReportFilterContext context)
        {
            var transactions = await _databaseService.GetFilteredTransactionsAsync(
                context.AccountId, context.CategoryId, context.ProjectId,
                null, null, context.StartDate, context.EndDate);

            var targetType = context.TransactionType ?? TransactionType.Expense;
            transactions = transactions.Where(t => t.Type == targetType).ToList();

            if (!transactions.Any()) return new List<ProjectSummary>();

            var projects = await _databaseService.GetProjectsAsync(includeArchived: true);
            var accounts = await _databaseService.GetAccountsAsync(includeClosed: true);
            var accountDict = accounts.ToDictionary(a => a.Id, a => a.Currency);

            decimal totalAmount = 0;
            var projectTotals = new ConcurrentDictionary<int, decimal>();
            object syncRoot = new();

            foreach (var t in transactions)
            {
                var currency = accountDict.TryGetValue(t.AccountId, out var curr) ? curr : context.BaseCurrency;
                decimal convertedAmount = t.Amount;

                if (currency != context.BaseCurrency)
                {
                    var rate = await _databaseService.GetApplicableExchangeRateAsync(currency, context.BaseCurrency, t.Date);
                    if (rate.HasValue && rate.Value > 0)
                    {
                        convertedAmount = convertedAmount * (1.0m / rate.Value);
                    }
                }

                // Agregacja po ProjectId (null traktujemy jako 0 - "Brak projektu")
                projectTotals.AddOrUpdate(t.ProjectId ?? 0, convertedAmount, (_, current) => current + convertedAmount);

                lock (syncRoot)
                {
                    totalAmount += convertedAmount;
                }
            }

            if (totalAmount == 0) return new List<ProjectSummary>();

            var sortedProjectTotals = projectTotals
                .OrderByDescending(kvp => kvp.Value)
                .ToList();

            var result = new List<ProjectSummary>();
            int colorIndex = 0;

            // 2. Mapujemy na DTO i przypisujemy dynamiczny kolor
            foreach (var kvp in sortedProjectTotals)
            {
                var proj = projects.FirstOrDefault(p => p.Id == kvp.Key);

                result.Add(new ProjectSummary(
                    ProjectId: kvp.Key,
                    ProjectName: proj?.Name ?? AppResources.NoProject,
                    ColorHex: ColorPaletteHelper.GetColor(colorIndex++), // NOWOŚĆ: Zewnętrzny generator
                    TotalAmount: kvp.Value,
                    Percentage: (double)(kvp.Value / totalAmount * 100)
                ));
            }

            return result;
        }
        // ===================Top transactions==========================
        public async Task<List<TransactionDetailDto>> GetTopTransactionsAsync(ReportFilterContext context, int limit = 10)
        {
            // 1. Wykorzystujemy bazę do wykonania najcięższej pracy (JOINy z kategoriami i kontami)
            // Nie sortujemy jeszcze po Amount, bo baza nie zna docelowych kursów walut!
            var transactionsDto = await _databaseService.GetTransactionsWithDetailsAsync(
                context.AccountId, context.CategoryId, context.ProjectId,
                null, null, null,
                sortColumn: "Date", // Domyślne sortowanie
                isAscending: false,
                startDate: context.StartDate,
                endDate: context.EndDate);

            // 2. Filtrujemy tylko wybrany typ (np. Wydatki)
            var targetType = (int)(context.TransactionType ?? TransactionType.Expense);
            var expenses = transactionsDto.Where(t => t.Type == targetType).ToList();

            if (!expenses.Any()) return new List<TransactionDetailDto>();

            // 3. Przeliczamy waluty w pamięci dla odfiltrowanego zbioru
            foreach (var t in expenses)
            {
                if (t.AccountCurrency != context.BaseCurrency)
                {
                    var rate = await _databaseService.GetApplicableExchangeRateAsync(t.AccountCurrency, context.BaseCurrency, t.Date);
                    if (rate.HasValue && rate.Value > 0)
                    {
                        t.Amount = t.Amount * (1.0m / rate.Value);
                        t.SignedAmount = -t.Amount;
                        t.AccountCurrency = context.BaseCurrency;
                    }
                }
            }

            // 4. Sortowanie po rzeczywistej (przewalutowanej) kwocie
            return expenses
                .OrderByDescending(t => t.Amount)
                .Take(limit)
                .ToList();
        }

        //==================== TRENDY ==========================
        public async Task<List<TrendDataPoint>> GetTrendAsync(ReportFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var startDate = context.StartDate.Date;
            var endDate = context.EndDate.Date;

            if (startDate > endDate)
                throw new ArgumentException("Data początkowa nie może być późniejsza niż data końcowa.", nameof(context));

            var transactions = await _databaseService.GetFilteredTransactionsAsync(context.AccountId, context.CategoryId, context.ProjectId, null, null, startDate, endDate);

            var relevantTransactions = transactions
                .Where(x => x.Type == TransactionType.Income || x.Type == TransactionType.Expense)
                .ToList();

            var accounts = await _databaseService.GetAccountsAsync(includeClosed: true);
            var accountCurrencies = accounts.ToDictionary(x => x.Id, x => x.Currency);

            var grouping = GetTrendGrouping(startDate, endDate);
            var totals = new Dictionary<DateTime, (decimal Income, decimal Expense)>();

            foreach (var transaction in relevantTransactions)
            {
                var amount = transaction.Amount;
                var sourceCurrency = accountCurrencies.TryGetValue(transaction.AccountId, out var currency) ? currency : context.BaseCurrency;

                if (!string.Equals(sourceCurrency, context.BaseCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    var rate = await _databaseService.GetApplicableExchangeRateAsync(sourceCurrency, context.BaseCurrency, transaction.Date);

                    if (rate.HasValue && rate.Value > 0)
                        amount *= 1.0m / rate.Value;
                }

                var bucketDate = GetBucketStart(transaction.Date, grouping);

                if (!totals.TryGetValue(bucketDate, out var total))
                    total = (0m, 0m);

                if (transaction.Type == TransactionType.Income)
                    total.Income += amount;
                else if (transaction.Type == TransactionType.Expense)
                    total.Expense += amount;

                totals[bucketDate] = total;
            }

            var points = new List<TrendDataPoint>();

            foreach (var bucketDate in GenerateBucketDates(startDate, endDate, grouping))
            {
                var total = totals.TryGetValue(bucketDate, out var value) ? value : (Income: 0m, Expense: 0m);
                points.Add(new TrendDataPoint(bucketDate, total.Income, total.Expense));
            }

            return points;
        }

        internal static TrendGrouping GetTrendGrouping(DateTime startDate, DateTime endDate)
        {
            var inclusiveDayCount = (endDate.Date - startDate.Date).Days + 1;

            if (inclusiveDayCount <= 60)
                return TrendGrouping.Day;

            if (inclusiveDayCount <= 180)
                return TrendGrouping.Week;

            return TrendGrouping.Month;
        }

        internal static DateTime GetBucketStart(DateTime date, TrendGrouping grouping)
        {
            var normalizedDate = date.Date;

            return grouping switch
            {
                TrendGrouping.Day => normalizedDate,
                TrendGrouping.Week => GetIsoWeekStart(normalizedDate),
                TrendGrouping.Month => new DateTime(normalizedDate.Year, normalizedDate.Month, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, null)
            };
        }

        internal static DateTime GetIsoWeekStart(DateTime date)
        {
            var difference = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return date.Date.AddDays(-difference);
        }

        internal static IEnumerable<DateTime> GenerateBucketDates(DateTime startDate, DateTime endDate, TrendGrouping grouping)
        {
            var current = GetBucketStart(startDate, grouping);
            var last = GetBucketStart(endDate, grouping);

            while (current <= last)
            {
                yield return current;

                current = grouping switch
                {
                    TrendGrouping.Day => current.AddDays(1),
                    TrendGrouping.Week => current.AddDays(7),
                    TrendGrouping.Month => current.AddMonths(1),
                    _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, null)
                };
            }
        }

        // =================== Majątek ==========================
        public async Task<List<BalanceHistoryPoint>> GetBalanceHistoryAsync(ReportFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var startDate = context.StartDate.Date;
            var endDate = context.EndDate.Date;

            if (startDate > endDate)
                throw new ArgumentException(
                    "Data początkowa nie może być późniejsza niż data końcowa.",
                    nameof(context));

            var allAccounts = await _databaseService.GetAccountsAsync(includeClosed: true);

            var accounts = context.AccountId.HasValue
                ? allAccounts
                    .Where(account => account.Id == context.AccountId.Value)
                    .ToList()
                : allAccounts;

            if (accounts.Count == 0)
                return new List<BalanceHistoryPoint>();

            var accountsWithoutInitialBalanceDate = accounts
                .Where(account => !account.InitialBalanceDate.HasValue)
                .ToList();

            if (accountsWithoutInitialBalanceDate.Count > 0)
            {
                var accountNames = string.Join(
                    ", ",
                    accountsWithoutInitialBalanceDate.Select(account => account.Name));

                throw new InvalidOperationException(AppResources.Reports_WealthErrorMessage+$"{accountNames}.");
            }

            var accountById = accounts.ToDictionary(account => account.Id);

            var allTransactions = await _databaseService.GetTransactionsAsync();

            // Musimy pobrać również transakcje wcześniejsze niż StartDate,
            // ponieważ wpływają one na saldo pierwszego punktu raportu.
            var transactions = allTransactions
                .Where(transaction => transaction.Date.Date <= endDate)
                .Where(transaction =>
                    accountById.ContainsKey(transaction.AccountId) ||
                    (
                        transaction.DestinationAccountId.HasValue &&
                        accountById.ContainsKey(transaction.DestinationAccountId.Value)
                    ))
                .OrderBy(transaction => transaction.Date)
                .ThenBy(transaction => transaction.Id)
                .ToList();

            var grouping = GetTrendGrouping(startDate, endDate);

            var balances = accounts.ToDictionary(
                account => account.Id,
                _ => 0m);

            var activatedAccountIds = new HashSet<int>();
            var transactionIndex = 0;

            var points = new List<BalanceHistoryPoint>();

            var exchangeRateCache = new Dictionary<
                (string SourceCurrency, string TargetCurrency, DateTime Date),
                decimal?>();

            foreach (var bucketDate in GenerateBucketDates(startDate, endDate, grouping))
            {
                var snapshotDate = GetBalanceSnapshotDate(bucketDate, endDate, grouping);

                ActivateAccounts(
                    accounts,
                    balances,
                    activatedAccountIds,
                    snapshotDate);

                while (
                    transactionIndex < transactions.Count &&
                    transactions[transactionIndex].Date.Date <= snapshotDate)
                {
                    ApplyTransactionToBalances(
                        transactions[transactionIndex],
                        accountById,
                        balances,
                        activatedAccountIds);

                    transactionIndex++;
                }

                var totalBalance = await CalculateTotalBalanceAsync(
                    accounts,
                    balances,
                    activatedAccountIds,
                    context.BaseCurrency,
                    snapshotDate,
                    exchangeRateCache);

                points.Add(new BalanceHistoryPoint(
                    bucketDate,
                    totalBalance));
            }

            return points;
        }

        private static DateTime GetBalanceSnapshotDate(DateTime bucketDate, DateTime reportEndDate, TrendGrouping grouping)
        {
            var bucketEndDate = grouping switch
            {
                TrendGrouping.Day => bucketDate.Date,

                TrendGrouping.Week => bucketDate.Date.AddDays(6),

                TrendGrouping.Month => bucketDate.Date
                    .AddMonths(1)
                    .AddDays(-1),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(grouping),
                    grouping,
                    null)
            };

            return bucketEndDate > reportEndDate
                ? reportEndDate.Date
                : bucketEndDate;
        }

        private static void ActivateAccounts(IEnumerable<Account> accounts, IDictionary<int, decimal> balances, ISet<int> activatedAccountIds, DateTime snapshotDate)
        {
            foreach (var account in accounts)
            {
                if (activatedAccountIds.Contains(account.Id))
                    continue;

                if (!account.InitialBalanceDate.HasValue)
                    continue;

                if (account.InitialBalanceDate.Value.Date > snapshotDate.Date)
                    continue;

                balances[account.Id] = account.InitialBalance;
                activatedAccountIds.Add(account.Id);
            }
        }

        private static void ApplyTransactionToBalances(Transaction transaction, IReadOnlyDictionary<int, Account> accountById, IDictionary<int, decimal> balances, ISet<int> activatedAccountIds)
        {
            switch (transaction.Type)
            {
                case TransactionType.Income:
                    ApplyAmountToAccount(
                        transaction.AccountId,
                        transaction.Date,
                        transaction.Amount,
                        accountById,
                        balances,
                        activatedAccountIds);

                    break;

                case TransactionType.Expense:
                    ApplyAmountToAccount(
                        transaction.AccountId,
                        transaction.Date,
                        -transaction.Amount,
                        accountById,
                        balances,
                        activatedAccountIds);

                    break;

                case TransactionType.Transfer:
                    ApplyAmountToAccount(
                        transaction.AccountId,
                        transaction.Date,
                        -transaction.Amount,
                        accountById,
                        balances,
                        activatedAccountIds);

                    if (transaction.DestinationAccountId.HasValue)
                    {
                        var destinationAmount =
                            transaction.ExchangeRate.HasValue &&
                            transaction.ExchangeRate.Value > 0
                                ? transaction.Amount *
                                  (1.0m / transaction.ExchangeRate.Value)
                                : transaction.Amount;

                        ApplyAmountToAccount(
                            transaction.DestinationAccountId.Value,
                            transaction.Date,
                            destinationAmount,
                            accountById,
                            balances,
                            activatedAccountIds);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(transaction.Type), transaction.Type, "Nieobsługiwany typ transakcji.");
            }
        }

        private static void ApplyAmountToAccount(int accountId, DateTime transactionDate, decimal amount, IReadOnlyDictionary<int, Account> accountById, IDictionary<int, decimal> balances, ISet<int> activatedAccountIds)
        {
            if (!accountById.TryGetValue(accountId, out var account))
                return;

            if (!account.InitialBalanceDate.HasValue)
                return;

            if (transactionDate.Date < account.InitialBalanceDate.Value.Date)
                return;

            if (!activatedAccountIds.Contains(accountId))
                return;

            balances[accountId] += amount;
        }

        private async Task<decimal> CalculateTotalBalanceAsync(
            IEnumerable<Account> accounts,
            IReadOnlyDictionary<int, decimal> balances,
            ISet<int> activatedAccountIds,
            string baseCurrency,
            DateTime snapshotDate,
            IDictionary<(string SourceCurrency, string TargetCurrency, DateTime Date), decimal?> exchangeRateCache)
        {
            ArgumentNullException.ThrowIfNull(accounts);
            ArgumentNullException.ThrowIfNull(balances);
            ArgumentNullException.ThrowIfNull(activatedAccountIds);
            ArgumentNullException.ThrowIfNull(exchangeRateCache);
            ArgumentException.ThrowIfNullOrWhiteSpace(baseCurrency);

            var normalizedBaseCurrency = baseCurrency
                .Trim()
                .ToUpperInvariant();

            decimal totalBalance = 0m;

            foreach (var account in accounts)
            {
                if (!activatedAccountIds.Contains(account.Id))
                    continue;

                if (!balances.TryGetValue(account.Id, out var accountBalance))
                    continue;

                // Zerowe saldo nie wymaga kursu walutowego.
                // 0 jednostek dowolnej waluty zawsze wnosi 0 do majątku.
                if (accountBalance == 0m)
                    continue;

                var accountCurrency = account.Currency
                    .Trim()
                    .ToUpperInvariant();

                if (string.Equals(
                        accountCurrency,
                        normalizedBaseCurrency,
                        StringComparison.OrdinalIgnoreCase))
                {
                    totalBalance += accountBalance;
                    continue;
                }

                var cacheKey = (
                    SourceCurrency: accountCurrency,
                    TargetCurrency: normalizedBaseCurrency,
                    Date: snapshotDate.Date);

                if (!exchangeRateCache.TryGetValue(cacheKey, out var exchangeRate))
                {
                    exchangeRate = await _databaseService
                        .GetApplicableExchangeRateAsync(
                            accountCurrency,
                            normalizedBaseCurrency,
                            snapshotDate.Date);

                    exchangeRateCache[cacheKey] = exchangeRate;
                }

                if (!exchangeRate.HasValue || exchangeRate.Value <= 0m)
                {
                    throw new InvalidOperationException(AppResources.Reports_WealthMissingExchangeRate1 + $" {accountCurrency} → " + $"{normalizedBaseCurrency} " 
                        + AppResources.Reports_WealthMissingExchangeRate2 + $" {snapshotDate.Date:d}.");
                }

                var convertedBalance =
                    accountBalance * (1.0m / exchangeRate.Value);

                totalBalance += convertedBalance;
            }

            return totalBalance;
        }
    }   
}
