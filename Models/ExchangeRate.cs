using SQLite;

namespace ExpenseTracker.Models
{
    public class ExchangeRate
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string SourceCurrency { get; set; } = string.Empty;
        public string TargetCurrency { get; set; } = string.Empty;

        public decimal Rate { get; set; }
        public DateTime Date { get; set; }
    }
}