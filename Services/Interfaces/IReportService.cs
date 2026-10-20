using ExpenseTracker.Models.Reports;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services.Interfaces
{
    public interface IReportService
    {
     
        Task<List<CategorySummary>> GetCategoryBreakdownAsync(ReportFilterContext context);
        Task<List<CategorySummary>> GetCategoryDrillDownAsync(ReportFilterContext context,int parentCategoryId);

        Task<List<ProjectSummary>> GetProjectBreakdownAsync(ReportFilterContext context);
        Task<List<TrendDataPoint>> GetTrendAsync(ReportFilterContext context);
        Task<List<TransactionDetailDto>> GetTopTransactionsAsync(ReportFilterContext context, int limit = 10);

        Task<List<BalanceHistoryPoint>> GetBalanceHistoryAsync(ReportFilterContext context);
    }
}
