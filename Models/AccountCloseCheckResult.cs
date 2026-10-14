namespace ExpenseTracker.Models;

public sealed record AccountCloseCheckResult(
    bool CanClose,
    decimal CurrentBalance,
    string Currency);
