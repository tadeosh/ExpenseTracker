using ExpenseTracker.Models;

namespace ExpenseTracker.Services.Interfaces
{
    public interface IDatabaseService
    {
        // Konta
        Task<List<Account>> GetAccountsAsync();
        Task<Account> GetAccountAsync(int accountId);
        Task<int> SaveAccountAsync(Account account);
        Task<int> DeleteAccountAsync(Account account);
        Task<Dictionary<int, decimal>> GetAllAccountBalancesAsync();

        // Transakcje
        Task<Transaction?> GetTransactionAsync(int transactionId);
        Task<List<Transaction>> GetTransactionsAsync();
        Task<int> SaveTransactionAsync(Transaction transaction);
        Task<int> DeleteTransactionAsync(Transaction transaction);
        Task<List<Transaction>> GetRecentTransactionsAsync(int limit = 10);
        Task<List<Transaction>> GetFilteredTransactionsAsync(int? accountId, int? categoryId, int? projectId, decimal? minAmount, decimal? maxAmount, DateTime? startDate=null, DateTime? endDate=null);
        Task<List<TransactionDetailDto>> GetTransactionsWithDetailsAsync(
                int? accountId, int? categoryId, int? projectId,
                decimal? minAmount, decimal? maxAmount,
                string? searchText, string sortColumn, bool isAscending, DateTime? startDate = null, DateTime? endDate = null);
        Task<List<TransactionDetailDto>> GetRecentTransactionsWithDetailsAsync(int limit = 30);

        //Transakcje Cykliczne

        Task<List<RecurringTransaction>> GetActiveRecurringTransactionsAsync();
        Task<List<RecurringTransaction>> GetRecurringTransactionsAsync();
        Task<RecurringTransaction?> GetRecurringTransactionAsync(int id);
        Task<int> DeleteRecurringTransactionAsync(RecurringTransaction recurringTransaction);
        Task<int> SaveRecurringTransactionAsync(RecurringTransaction recurringTransaction);
        Task RunInTransactionAsync(Action<SQLite.SQLiteConnection> action);

        // Raporty

        Task<List<CategoryExpenseSummaryDto>> GetCurrentMonthExpensesAsync(); //HomePage

        // Kategorie
        Task<List<Category>> GetCategoriesAsync(bool includeArchived = false);
        Task<int> SaveCategoryAsync(Category category);
        Task<int> DeleteCategoryAsync(Category category);

        // Projekty
        Task<List<Project>> GetProjectsAsync(bool includeArchived = false);
        Task<int> SaveProjectAsync(Project project);
        Task<int> DeleteProjectAsync(Project project);

        // Kursy walut
        Task<List<ExchangeRate>> GetExchangeRatesAsync();
        Task SaveExchangeRateAsync(ExchangeRate rate);
        Task DeleteExchangeRateAsync(ExchangeRate rate);
        Task<decimal?> GetApplicableExchangeRateAsync(string sourceCurrency, string targetCurrency, DateTime transactionDate);
        Task<bool> ExchangeRateExistsAsync(string sourceCurrency, string targetCurrency, DateTime date, int excludeId = 0);
        Task<int> SaveOrUpdateDailyExchangeRateAsync(string sourceCurrency, string targetCurrency, decimal rate, DateTime date);

        // Zarządzanie
        Task WipeAllDataAsync();
    }
}
