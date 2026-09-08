using ExpenseTracker.Models.Reports;

namespace ExpenseTracker.Services.Interfaces
{
    public interface IReportService
    {
        Task<List<CategorySummary>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate);
    }
}
