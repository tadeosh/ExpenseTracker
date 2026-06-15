using SQLite;

namespace ExpenseTracker.Models
{
    public class Account
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;// np. "Konto główne", "Gotówka"

        public string Currency { get; set; } = string.Empty; // np. "PLN", "EUR", "USD"

        // Stan konta w momencie jego dodania do aplikacji
        public decimal InitialBalance { get; set; }
    }
}