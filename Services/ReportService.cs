using ExpenseTracker.Helpers;
using ExpenseTracker.Models;
using ExpenseTracker.Models.Reports;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using System.Collections.Concurrent;

namespace ExpenseTracker.Services
{
    public class ReportService : IReportService
    {
        private readonly IDatabaseService _databaseService;

        public ReportService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<CategorySummary>> GetCategoryBreakdownAsync(ReportFilterContext context)
        {
            // 1. Pobieramy wyłącznie przefiltrowane rekordy (operacja we/wy ograniczona do minimum)
            var transactions = await _databaseService.GetFilteredTransactionsAsync(
                context.AccountId, context.CategoryId, context.ProjectId,
                null, null, context.StartDate, context.EndDate);

            // Filtrujemy tylko typ z kontekstu (domyślnie Expense)
            var targetType = context.TransactionType ?? TransactionType.Expense;
            transactions = transactions.Where(t => t.Type == targetType).ToList();

            if (!transactions.Any()) return new List<CategorySummary>();

            // 2. Pobieramy słowniki referencyjne z IDatabaseService
            var categories = await _databaseService.GetCategoriesAsync(includeArchived: true);
            var accounts = await _databaseService.GetAccountsAsync();
            var accountDict = accounts.ToDictionary(a => a.Id, a => a.Currency);

            decimal totalAmount = 0;
            var categoryTotals = new ConcurrentDictionary<int, decimal>();
            object syncRoot = new();

            // 3. Równoległe przeliczanie z użyciem Twojego mechanizmu walut
            // Dla optymalizacji < 2 sekund nie używamy Task.WhenAll w pętli. 
            // Zamiast tego zrobimy pre-fetch kursów lub konwersję liniową, 
            // ale dzięki małemu zbiorowi po filtrze, asynchroniczna pętla wystarczy.
            foreach (var t in transactions)
            {
                var currency = accountDict.TryGetValue(t.AccountId, out var curr) ? curr : context.BaseCurrency;
                decimal convertedAmount = t.Amount;

                // Użycie Twojej metody z DatabaseService.cs
                if (currency != context.BaseCurrency)
                {
                    var rate = await _databaseService.GetApplicableExchangeRateAsync(currency, context.BaseCurrency, t.Date);
                    if (rate.HasValue && rate.Value > 0)
                    {
                        convertedAmount = convertedAmount * (1.0m / rate.Value);
                    }
                }

                categoryTotals.AddOrUpdate(t.CategoryId ?? 0, convertedAmount, (_, current) => current + convertedAmount);

                lock (syncRoot)
                {
                    totalAmount += convertedAmount;
                }
            }

            if (totalAmount == 0) return new List<CategorySummary>();

            // 4. Mapowanie
            return categoryTotals
                .Select(kvp =>
                {
                    var cat = categories.FirstOrDefault(c => c.Id == kvp.Key);
                    return new CategorySummary(
                        CategoryId: kvp.Key,
                        CategoryName: cat?.Name ?? AppResources.CategoryNone,
                       // ProjectName = proj?.Name ?? AppResources.NoProject,
                        ColorHex: cat?.ColorHex ?? "#808080",
                        TotalAmount: kvp.Value,
                        Percentage: (double)(kvp.Value / totalAmount * 100)
                    );
                })
                .OrderByDescending(x => x.TotalAmount)
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
            var accounts = await _databaseService.GetAccountsAsync();
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

        public Task<List<TrendDataPoint>> GetTrendAsync(ReportFilterContext context)
        {
            // Do implementacji w kolejnym kroku
            throw new NotImplementedException();
        }
    }
}
