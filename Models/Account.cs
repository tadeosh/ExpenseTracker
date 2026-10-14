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

        // Data, od której InitialBalance rozpoczyna historię salda konta.
        // Null oznacza stare konto wymagające świadomego uzupełnienia danych.
        public DateTime? InitialBalanceDate { get; set; }

        public DateTime? ClosedDate { get; set; }

        [Ignore]
        public bool IsClosed => ClosedDate.HasValue;

        [Ignore]
        public bool IsActive => !ClosedDate.HasValue;
    }
}
