using ExpenseTracker.Models;
using ExpenseTracker.Services.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using ExpenseTracker.Messages;

namespace ExpenseTracker.Services
{
    public class RecurringTransactionEngine : IRecurringTransactionEngine
    {
        private readonly IDatabaseService _databaseService;

        public RecurringTransactionEngine(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task ProcessPendingTransactionsAsync()
        {
            var activeTemplates = await _databaseService.GetActiveRecurringTransactionsAsync();
            if (!activeTemplates.Any()) return;

            var today = DateTime.Today;
            var transactionsToInsert = new List<Transaction>();
            var templatesToUpdate = new List<RecurringTransaction>();

            foreach (var template in activeTemplates)
            {
                bool hasChanges = false;

                // Nadrabianie zaległości (Catch-up)
                while (template.NextDueDate <= today && (template.EndDate == null || template.NextDueDate <= template.EndDate))
                {
                    transactionsToInsert.Add(new Transaction
                    {
                        Amount = template.Amount,
                        Type = (TransactionType)template.Type,
                        Description = template.Description,
                        Date = template.NextDueDate, // Data historyczna z momentu, gdy transakcja powinna mieć miejsce
                        AccountId = template.AccountId,
                        CategoryId = template.CategoryId,
                        ProjectId = template.ProjectId
                    });

                    template.NextDueDate = CalculateNextDate(template.NextDueDate, template.RecurrenceInterval, template.RecurrenceUnit);
                    hasChanges = true;
                }

                // Dodatkowa ochrona: jeśli wyliczyliśmy daty poza EndDate, dezaktywujemy szablon
                if (template.EndDate.HasValue && template.NextDueDate > template.EndDate.Value)
                {
                    template.IsActive = false;
                    hasChanges = true;
                }

                if (hasChanges) templatesToUpdate.Add(template);
            }

            if (transactionsToInsert.Any())
            {
                // Bezpieczny, atomowy zapis wszystkiego naraz (błyskawiczne na Android/Windows)
                await _databaseService.RunInTransactionAsync(conn =>
                {
                    conn.InsertAll(transactionsToInsert);
                    conn.UpdateAll(templatesToUpdate);
                });

                // Odświeżamy UI dla ekranu głównego i listy transakcji
                WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage());
            }
        }

        private DateTime CalculateNextDate(DateTime current, int interval, RecurrenceUnit unit) => unit switch
        {
            RecurrenceUnit.Days => current.AddDays(interval),
            RecurrenceUnit.Weeks => current.AddDays(interval * 7),
            RecurrenceUnit.Months => current.AddMonths(interval),
            RecurrenceUnit.Years => current.AddYears(interval),
            _ => current.AddMonths(interval)
        };
    }
}