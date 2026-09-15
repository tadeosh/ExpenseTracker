using ExpenseTracker.Models;
using ExpenseTracker.Services.Interfaces;
using Microsoft.Extensions.Logging;
using ExpenseTracker.Resources.Strings;


namespace ExpenseTracker.Services
{
    public class RecurringTransactionEngine : IRecurringTransactionEngine
    {
        private readonly IDatabaseService _databaseService;
        private readonly ITransactionChangeNotifier _transactionChangeNotifier;
        private readonly ILogger<RecurringTransactionEngine> _logger;

        private readonly SemaphoreSlim _processingLock = new(1, 1);

        public RecurringTransactionEngine(IDatabaseService databaseService, ITransactionChangeNotifier transactionChangeNotifier, ILogger<RecurringTransactionEngine> logger)
        {
            _databaseService = databaseService;
            _transactionChangeNotifier = transactionChangeNotifier;
            _logger = logger;
        }

        public async Task ProcessPendingTransactionsAsync()
        {
            await _processingLock.WaitAsync();

            try
            {
                await ProcessPendingTransactionsInternalAsync();
            }
            finally
            {
                _processingLock.Release();
            }
        }

        private async Task ProcessPendingTransactionsInternalAsync()
        {
            var activeTemplates = await _databaseService.GetActiveRecurringTransactionsAsync();

            if (!activeTemplates.Any())
                return;

            var today = DateTime.Today;
            var transactionsToInsert = new List<Transaction>();
            var templatesToUpdate = new List<RecurringTransaction>();

            foreach (var template in activeTemplates)
            {
                if (template.RecurrenceInterval <= 0)
                {
                    _logger.LogWarning("Pominięto szablon cykliczny {TemplateId}, ponieważ interwał {Interval} jest nieprawidłowy.", template.Id, template.RecurrenceInterval);
                    continue;
                }

                try
                {
                    var generatedTransactions = new List<Transaction>();
                    var nextDueDate = template.NextDueDate.Date;

                    while (nextDueDate <= today && (!template.EndDate.HasValue || nextDueDate <= template.EndDate.Value.Date))
                    {
                        decimal? applicableExchangeRate = null;
                        var descriptionSuffix = string.Empty;

                        if (template.Type == TransactionType.Transfer && template.DestinationAccountId.HasValue)
                        {
                            var sourceAccount = await _databaseService.GetAccountAsync(template.AccountId);
                            var destinationAccount = await _databaseService.GetAccountAsync(template.DestinationAccountId.Value);

                            if (sourceAccount.Currency != destinationAccount.Currency)
                            {
                                var rate = await _databaseService.GetApplicableExchangeRateAsync(sourceAccount.Currency, destinationAccount.Currency, nextDueDate);

                                if (rate.HasValue)
                                {
                                    applicableExchangeRate = rate.Value;
                                }
                                else
                                {
                                    applicableExchangeRate = 1.0m;
                                    descriptionSuffix = $" [{AppResources.MissingRatesTitle}]";
                                }
                            }
                        }

                        generatedTransactions.Add(new Transaction
                        {
                            Amount = template.Amount,
                            Type = template.Type,
                            Description = template.Description + descriptionSuffix,
                            Date = nextDueDate,
                            AccountId = template.AccountId,
                            CategoryId = template.CategoryId,
                            ProjectId = template.ProjectId,
                            DestinationAccountId = template.DestinationAccountId,
                            ExchangeRate = applicableExchangeRate
                        });

                        nextDueDate = CalculateNextDate(nextDueDate, template.RecurrenceInterval, template.RecurrenceUnit);
                    }

                    var shouldDeactivate = template.EndDate.HasValue && nextDueDate > template.EndDate.Value.Date;
                    var nextDateChanged = nextDueDate != template.NextDueDate.Date;
                    var activeStateChanged = shouldDeactivate && template.IsActive;

                    if (!nextDateChanged && !activeStateChanged)
                        continue;

                    template.NextDueDate = nextDueDate;

                    if (shouldDeactivate)
                        template.IsActive = false;

                    transactionsToInsert.AddRange(generatedTransactions);
                    templatesToUpdate.Add(template);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się przetworzyć szablonu transakcji cyklicznej {TemplateId}. Szablon został pominięty.", template.Id);
                }
            }

            if (!transactionsToInsert.Any() && !templatesToUpdate.Any())
                return;

            await _databaseService.RunInTransactionAsync(connection =>
            {
                if (transactionsToInsert.Any())
                    connection.InsertAll(transactionsToInsert);

                if (templatesToUpdate.Any())
                    connection.UpdateAll(templatesToUpdate);
            });

            if (transactionsToInsert.Any())
                _transactionChangeNotifier.NotifyTransactionsChanged();
        }

        internal static DateTime CalculateNextDate(DateTime current, int interval, RecurrenceUnit unit) => unit switch
        {
            RecurrenceUnit.Days => current.AddDays(interval),
            RecurrenceUnit.Weeks => current.AddDays(interval * 7),
            RecurrenceUnit.Months => current.AddMonths(interval),
            RecurrenceUnit.Years => current.AddYears(interval),
            _ => current.AddMonths(interval)
        };
    }
}
