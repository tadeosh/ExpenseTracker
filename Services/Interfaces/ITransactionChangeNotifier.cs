namespace ExpenseTracker.Services.Interfaces;

public interface ITransactionChangeNotifier
{
    void NotifyTransactionsChanged();
}
