using CommunityToolkit.Mvvm.Messaging;
using ExpenseTracker.Messages;
using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.Services;

public sealed class TransactionChangeNotifier : ITransactionChangeNotifier
{
    public void NotifyTransactionsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage());
        });
    }
}
