namespace ExpenseTracker.Models.Reports
{
    // Używamy rekordu, aby zapewnić niezmienność (Immutability) danych raportowych
    public record CategorySummary(int CategoryId, string CategoryName, string ColorHex, decimal TotalAmount, double Percentage);
    public record ProjectSummary(int ProjectId, string ProjectName, string ColorHex, decimal TotalAmount, double Percentage);

    public record TrendDataPoint(DateTime Date, decimal Income, decimal Expense);

    public record AccountBalanceSummary(int AccountId, string AccountName, decimal StartBalance, decimal EndBalance, decimal Difference);
}
