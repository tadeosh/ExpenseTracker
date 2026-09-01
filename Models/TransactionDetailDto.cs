using System;

namespace ExpenseTracker.Models
{
    public class TransactionDetailDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Type { get; set; }
        public int AccountId { get; set; }

        // Złączone nazwy słowników
        public string CategoryName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;

        // Wyliczane dynamicznie przez SQL
        public decimal SignedAmount { get; set; }

        // NOWOŚĆ: Flaga informująca, czy jest to perspektywa wpływu (dla transferów)
        public bool IsTransferIn { get; set; }
    }
}