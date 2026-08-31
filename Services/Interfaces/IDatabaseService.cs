using ExpenseTracker.Models;

namespace ExpenseTracker.Services.Interfaces
{
    public interface IDatabaseService
    {
        // Konta
        Task<List<Account>> GetAccountsAsync();
        Task<int> SaveAccountAsync(Account account);
        Task<int> DeleteAccountAsync(Account account);
        Task<Dictionary<int, decimal>> GetAllAccountBalancesAsync();

        // Transakcje
        Task<List<Transaction>> GetTransactionsAsync();
        Task<int> SaveTransactionAsync(Transaction transaction);
        Task<int> DeleteTransactionAsync(Transaction transaction);
        Task<List<Transaction>> GetRecentTransactionsAsync(int limit = 10);
        Task<List<Transaction>> GetFilteredTransactionsAsync(int? accountId, int? categoryId, int? projectId, decimal? minAmount, decimal? maxAmount);

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

        // Zarządzanie
        Task WipeAllDataAsync();
    }
}