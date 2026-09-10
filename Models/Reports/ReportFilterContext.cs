using ExpenseTracker.Models;

namespace ExpenseTracker.Models.Reports
{
    public class ReportFilterContext
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? AccountId { get; set; }
        public int? CategoryId { get; set; }
        public int? ProjectId { get; set; }
        public TransactionType? TransactionType { get; set; }
        public string BaseCurrency { get; set; } = "EUR";
    }
}
