public class CategoryExpenseSummaryDto
{
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
    public string AccountCurrency { get; set; } = string.Empty;
}