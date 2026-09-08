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

            var today = DateTime.Today; // Zawsze 00:00:00
            var transactionsToInsert = new List<Transaction>();
            var templatesToUpdate = new List<RecurringTransaction>();

            foreach (var template in activeTemplates)
            {
                bool hasChanges = false;

                // Bezpieczne sprawdzanie samej daty bez części godzinowej
                while (template.NextDueDate.Date <= today && (template.EndDate == null || template.NextDueDate.Date <= template.EndDate.Value.Date))
                {
                    decimal? applicableExchangeRate = null;
                    string descriptionSuffix = string.Empty;

                    // LOGIKA TRANSFERU I KURSÓW WALUT
                    if (template.Type == TransactionType.Transfer && template.DestinationAccountId.HasValue)
                    {
                        var sourceAcc = await _databaseService.GetAccountAsync(template.AccountId);
                        var destAcc = await _databaseService.GetAccountAsync(template.DestinationAccountId.Value);

                        if (sourceAcc != null && destAcc != null && sourceAcc.Currency != destAcc.Currency)
                        {
                            // Pobieramy historyczny kurs na dzień wygenerowania zaległej transakcji
                            var rate = await _databaseService.GetApplicableExchangeRateAsync(sourceAcc.Currency, destAcc.Currency, template.NextDueDate.Date);

                            if (rate.HasValue)
                            {
                                applicableExchangeRate = rate.Value;
                            }
                            else
                            {
                                // Bezpiecznik: Brak kursu. Ustawiamy 1.0, by nie wysypać przeliczeń, i oznaczamy opis.
                                applicableExchangeRate = 1.0m;
                                descriptionSuffix = " [Brak ustalonego kursu]";
                            }
                        }
                    }

                    transactionsToInsert.Add(new Transaction
                    {
                        Amount = template.Amount,
                        Type = (TransactionType)template.Type,
                        Description = template.Description + descriptionSuffix,
                        Date = template.NextDueDate.Date,
                        AccountId = template.AccountId,
                        CategoryId = template.CategoryId,
                        ProjectId = template.ProjectId,
                        DestinationAccountId = template.DestinationAccountId,
                        ExchangeRate = applicableExchangeRate
                    });

                    template.NextDueDate = CalculateNextDate(template.NextDueDate.Date, template.RecurrenceInterval, template.RecurrenceUnit);
                    hasChanges = true;
                }

                if (template.EndDate.HasValue && template.NextDueDate.Date > template.EndDate.Value.Date)
                {
                    template.IsActive = false;
                    hasChanges = true;
                }

                if (hasChanges) templatesToUpdate.Add(template);
            }

            if (transactionsToInsert.Any())
            {
                await _databaseService.RunInTransactionAsync(conn =>
                {
                    conn.InsertAll(transactionsToInsert);
                    conn.UpdateAll(templatesToUpdate);
                });

                // Rozgłoszenie zmiany wymusza aktualizację UI 
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage());
                });
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