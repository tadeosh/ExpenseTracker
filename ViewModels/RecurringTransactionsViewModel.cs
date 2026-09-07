using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Services.Interfaces;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    public partial class RecurringTransactionsViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        public partial ObservableCollection<RecurringTransaction> Templates { get; set; } = new();

        public RecurringTransactionsViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task LoadDataAsync()
        {
            // Pobieramy wszystkie szablony (aktywne i nieaktywne)
           var allTemplates = await _databaseService.GetRecurringTransactionsAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Templates = new ObservableCollection<RecurringTransaction>(allTemplates.OrderByDescending(t => t.IsActive).ThenBy(t => t.NextDueDate));
            });
        }

        [RelayCommand]
        private async Task ToggleActiveStatusAsync(RecurringTransaction template)
        {
            template.IsActive = !template.IsActive;
            await _databaseService.SaveRecurringTransactionAsync(template);
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task DeleteTemplateAsync(RecurringTransaction template)
        {
            bool confirm = await Shell.Current.DisplayAlertAsync("Usuń", "Czy na pewno chcesz usunąć ten cykl?", "Tak", "Nie");
            if (!confirm) return;

            await _databaseService.DeleteRecurringTransactionAsync(template);
            Templates.Remove(template);
        }

        [RelayCommand]
        private async Task AddNewAsync()
        {
            // Wysyłamy parametr wymuszający włączenie trybu cyklicznego
            await Shell.Current.GoToAsync($"{nameof(Views.AddTransactionPage)}?IsRecurringDefault=True");
        }

        [RelayCommand]
        private async Task EditTemplateAsync(RecurringTransaction template)
        {
            // Wysyłamy ID szablonu do załadowania
            await Shell.Current.GoToAsync($"{nameof(Views.AddTransactionPage)}?EditRecurringId={template.Id}");
        }
    }
}