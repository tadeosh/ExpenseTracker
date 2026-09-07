using CommunityToolkit.Mvvm.Messaging.Messages;

namespace ExpenseTracker.Messages
{
    // Czysta deklaracja, że nastąpiła zmiana w transakcjach
    public class TransactionsChangedMessage : ValueChangedMessage<bool>
    {
        public TransactionsChangedMessage() : base(true) { }
    }
}