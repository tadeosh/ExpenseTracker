using ExpenseTracker.Models;
using SQLite;


namespace ExpenseTracker.Models
{
    public enum RecurrenceUnit
    {
        Days,
        Weeks,
        Months,
        Years
    }

    public class RecurringTransaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Dane transakcji
        public decimal Amount { get; set; }
        public TransactionType Type { get; set; } // Mapowane na TransactionType
        public string Description { get; set; } = string.Empty;
        public int AccountId { get; set; }
        // DODANE: Obsługa transferów
        public int? DestinationAccountId { get; set; }
        public int? CategoryId { get; set; }
        public int? ProjectId { get; set; }

        // Silnik N x Interwał
        public int RecurrenceInterval { get; set; } = 1; // "Co ile" (np. 2)
        public RecurrenceUnit RecurrenceUnit { get; set; } = RecurrenceUnit.Months; // "Czego" (np. Tygodni)

        public DateTime NextDueDate { get; set; }
        public DateTime? EndDate { get; set; } // Null oznacza w nieskończoność
        public bool IsActive { get; set; } = true;
    }
}