using SQLite;

namespace ExpenseTracker.Models
{
    public class ExchangeRate
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string SourceCurrency { get; set; } = string.Empty;

        [Indexed]
        public string TargetCurrency { get; set; } = string.Empty;

        public decimal Rate { get; set; }

        [Indexed]
        public DateTime Date { get; set; }
    }
}