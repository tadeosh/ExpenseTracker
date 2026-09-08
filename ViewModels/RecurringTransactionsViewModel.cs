using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Helpers;
using ExpenseTracker.Models;
using ExpenseTracker.Services.Interfaces;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    // Klasa pomocnicza do wyświetlania szablonów w UI ==============================
    public partial class RecurringTransactionDisplayItem : ObservableObject
    {
        public RecurringTransaction Transaction { get; }

        public int Id => Transaction.Id;
        public decimal Amount => Transaction.Amount;
        public string Description => Transaction.Description;
        public DateTime NextDueDate => Transaction.NextDueDate;

        // Zapewnia natychmiastowe odświeżenie UI po kliknięciu "Zawieś/Wznów"
        [ObservableProperty]
        public partial bool IsActive { get; set; }

        // Gotowy tekst łączący wartość i gramatykę, np. "2 tygodnie", "1 miesiąc"
        public string RecurrenceText =>
            $"{Transaction.RecurrenceInterval} {PluralizationHelper.GetUnitDisplayName(Transaction.RecurrenceUnit, Transaction.RecurrenceInterval)}";

        public RecurringTransactionDisplayItem(RecurringTransaction transaction)
        {
            Transaction = transaction;
            IsActive = transaction.IsActive;
        }
    }

    // ========================== klasa główna ViewModel dla strony szablonów cyklicznych ==========================
    public partial class RecurringTransactionsViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        public partial ObservableCollection<RecurringTransactionDisplayItem> Templates { get; set; } = new();

        public RecurringTransactionsViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task LoadDataAsync()
        {
           var allTemplates = await _databaseService.GetRecurringTransactionsAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Mapowanie encji na modele widoku
                var displayItems = allTemplates
                    .Select(t => new RecurringTransactionDisplayItem(t))
                    .OrderByDescending(t => t.IsActive)
                    .ThenBy(t => t.NextDueDate);

                Templates = new ObservableCollection<RecurringTransactionDisplayItem>(displayItems);
            });
        }

        [RelayCommand]
        private async Task ToggleActiveStatusAsync(RecurringTransactionDisplayItem item)
        {
            // Aktualizujemy encję oraz stan widoku
            item.Transaction.IsActive = !item.Transaction.IsActive;
            item.IsActive = item.Transaction.IsActive;

            await _databaseService.SaveRecurringTransactionAsync(item.Transaction);
            // Zauważ: nie wywołujemy już LoadDataAsync(), bo XAML odświeży się sam dzięki [ObservableProperty]!
        }

        [RelayCommand]
        private async Task DeleteTemplateAsync(RecurringTransactionDisplayItem item)
        {
            bool confirm = await Shell.Current.DisplayAlertAsync("Usuń", "Czy na pewno chcesz usunąć ten cykl?", "Tak", "Nie");
            if (!confirm) return;

            await _databaseService.DeleteRecurringTransactionAsync(item.Transaction);
            Templates.Remove(item);
        }

        [RelayCommand]
        private async Task EditTemplateAsync(RecurringTransactionDisplayItem item)
        {
            await Shell.Current.GoToAsync($"{nameof(Views.AddTransactionPage)}?EditRecurringId={item.Transaction.Id}");
        }

        [RelayCommand]
        private async Task AddNewAsync()
        {
            await Shell.Current.GoToAsync($"{nameof(Views.AddTransactionPage)}?IsRecurringDefault=True");
        }
    }

}