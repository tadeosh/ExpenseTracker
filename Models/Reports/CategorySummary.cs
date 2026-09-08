namespace ExpenseTracker.Models.Reports
{
    public class CategorySummary
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string ColorHex { get; set; } = "#808080";
        public double Percentage { get; set; }
    }
}
