using ExpenseTracker.Models;
using ExpenseTracker.Models.Reports;
using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.Services
{
    public class ReportService : IReportService
    {
        private readonly IDatabaseService _databaseService;

        public ReportService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<CategorySummary>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate)
        {          
            // Pobieramy transakcje z danego okresu. 
            // Opcjonalnie w IDatabaseService dodaj metodę: GetTransactionsAsync(startDate, endDate)
            var allTransactions = await _databaseService.GetTransactionsAsync();
            var categories = await _databaseService.GetCategoriesAsync();

            var expenses = allTransactions
                .Where(t => t.Type == TransactionType.Expense
                         && t.Date.Date >= startDate.Date
                         && t.Date.Date <= endDate.Date)
                .ToList();

            if (!expenses.Any()) return new List<CategorySummary>();

            var totalExpenses = expenses.Sum(e => e.Amount);

            var grouped = expenses
                .GroupBy(e => e.CategoryId)
                .Select(g =>
                {
                    var cat = categories.FirstOrDefault(c => c.Id == g.Key);
                    var amount = g.Sum(e => e.Amount);
                    return new CategorySummary
                    {
                        CategoryName = cat?.Name ?? "Brak kategorii",
                        ColorHex = cat?.ColorHex ?? "#808080",
                        TotalAmount = amount,
                        Percentage = (double)(amount / totalExpenses * 100)
                    };
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();

            return grouped;
        }
    }
}
