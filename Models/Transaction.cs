using SQLite;
using System;

namespace ExpenseTracker.Models
{
    // Definiujemy naszą własną listę typów transakcji
    public enum TransactionType
    {
        Expense,   // Wydatek
        Income,    // Przychód
        Transfer   // Transfer między kontami
    }

    public class Transaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public decimal Amount { get; set; }

        [Indexed]
        public DateTime Date { get; set; }

        public string? Description { get; set; }

        // NOWOŚĆ: Typ transakcji (wybiera jedną z wartości zdefiniowanych wyżej)
        [Indexed]
        public TransactionType Type { get; set; }

        // Znak zapytania sprawia, że kategoria jest opcjonalna (przydatne przy transferach)
        [Indexed]
        public int? CategoryId { get; set; }

        // Projekt nadal jest opcjonalny
        [Indexed]
        public int? ProjectId { get; set; }

        // Konto główne (dla wydatku: skąd pobrano, dla przychodu: gdzie wpłacono, dla transferu: z jakiego konta wyszło)
        [Indexed]
        public int AccountId { get; set; }

        // NOWOŚĆ: Konto docelowe - używane TYLKO przy typie 'Transfer'
        [Indexed]
        public int? DestinationAccountId { get; set; }

        // NOWOŚĆ: Kurs wymiany walut - wpisywany przez użytkownika (opcjonalny, bo nie każdy transfer to zmiana waluty)
        public decimal? ExchangeRate { get; set; }
    }
}