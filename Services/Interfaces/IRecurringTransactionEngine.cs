namespace ExpenseTracker.Services.Interfaces
{
    public interface IRecurringTransactionEngine
    {
        Task ProcessPendingTransactionsAsync();
    }
}