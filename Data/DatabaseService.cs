using SQLite;
using ExpenseTracker.Models;

namespace ExpenseTracker.Data
{
    public class DatabaseService
    {
        // Obiekt reprezentujący połączenie z bazą
        private SQLiteAsyncConnection _database = null!;

        // Inicjalizacja bazy danych (tworzenie pliku i tabel)
        private async Task InitAsync()
        {
            // Jeśli połączenie już istnieje, nie robimy nic
            if (_database != null)
                return;

            // Ustalenie bezpiecznej ścieżki do pliku na danym systemie (Android/Windows)
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "ExpenseTracker.db3");

            // Otwarcie połączenia z bazą
            _database = new SQLiteAsyncConnection(dbPath);

            // Magia SQLite: Automatyczne tworzenie tabel na podstawie naszych klas
            await _database.CreateTableAsync<Account>();
            await _database.CreateTableAsync<Category>();
            await _database.CreateTableAsync<Project>();
            await _database.CreateTableAsync<Transaction>();
        }

        // ==========================================
        // OPERACJE DLA KONT (ACCOUNTS)
        // ==========================================

        public async Task<List<Account>> GetAccountsAsync()
        {
            await InitAsync();
            return await _database.Table<Account>().ToListAsync();
        }

        public async Task<int> SaveAccountAsync(Account account)
        {
            await InitAsync();

            // Jeśli obiekt ma Id większe od 0, to znaczy, że już istnieje w bazie (Aktualizacja)
            if (account.Id != 0)
            {
                return await _database.UpdateAsync(account);
            }
            // W przeciwnym razie jest to nowy obiekt (Tworzenie)
            else
            {
                return await _database.InsertAsync(account);
            }
        }

        public async Task<int> DeleteAccountAsync(Account account)
        {
            await InitAsync();
            return await _database.DeleteAsync(account);
        }

        // ==========================================
        // OPERACJE DLA TRANSAKCJI (TRANSACTIONS)
        // ==========================================

        public async Task<List<Transaction>> GetTransactionsAsync()
        {
            await InitAsync();
            return await _database.Table<Transaction>().ToListAsync();
        }

        public async Task<int> SaveTransactionAsync(Transaction transaction)
        {
            await InitAsync();

            if (transaction.Id != 0)
            {
                return await _database.UpdateAsync(transaction);
            }
            else
            {
                return await _database.InsertAsync(transaction);
            }
        }

        public async Task<int> DeleteTransactionAsync(Transaction transaction)
        {
            await InitAsync();
            return await _database.DeleteAsync(transaction);
        }

        // Pobiera X najnowszych transakcji do podglądu na stronie głównej
        public async Task<List<Transaction>> GetRecentTransactionsAsync(int limit = 10)
        {
            await InitAsync();
            return await _database.Table<Transaction>()
                                  .OrderByDescending(t => t.Date)
                                  .Take(limit)
                                  .ToListAsync();
        }

        // Magia architektury: Dynamiczne wyliczanie aktualnego salda konta
        public async Task<decimal> GetAccountBalanceAsync(int accountId)
        {
            await InitAsync();

            var account = await _database.Table<Account>().Where(a => a.Id == accountId).FirstOrDefaultAsync();
            if (account == null) return 0;

            // Pobieramy wszystkie transakcje z bazy
            var transactions = await _database.Table<Transaction>().ToListAsync();

            // Zaczynamy od salda początkowego
            decimal balance = account.InitialBalance;

            // 1. Przychody (+)
            balance += transactions.Where(t => t.AccountId == accountId && t.Type == TransactionType.Income).Sum(t => t.Amount);

            // 2. Wydatki (-)
            balance -= transactions.Where(t => t.AccountId == accountId && t.Type == TransactionType.Expense).Sum(t => t.Amount);

            // 3. Transfery wychodzące z tego konta (-)
            balance -= transactions.Where(t => t.AccountId == accountId && t.Type == TransactionType.Transfer).Sum(t => t.Amount);

            // 4. Transfery przychodzące na to konto (+) wraz z przelicznikiem walut!
            var incomingTransfers = transactions.Where(t => t.DestinationAccountId == accountId && t.Type == TransactionType.Transfer);
            foreach (var transfer in incomingTransfers)
            {
                if (transfer.ExchangeRate.HasValue && transfer.ExchangeRate > 0)
                {
                    balance += transfer.Amount * transfer.ExchangeRate.Value;
                }
                else
                {
                    balance += transfer.Amount;
                }
            }

            return balance;
        }

        // ==========================================
        // OPERACJE DLA KATEGORII (CATEGORIES)
        // ==========================================

        public async Task<List<Category>> GetCategoriesAsync()
        {
            await InitAsync();
            return await _database.Table<Category>().ToListAsync();
        }

        public async Task<int> SaveCategoryAsync(Category category)
        {
            await InitAsync();
            if (category.Id != 0) return await _database.UpdateAsync(category);
            else return await _database.InsertAsync(category);
        }

        public async Task<int> DeleteCategoryAsync(Category category)
        {
            await InitAsync();
            return await _database.DeleteAsync(category);
        }

        // ==========================================
        // OPERACJE DLA PROJEKTÓW (PROJECTS)
        // ==========================================

        public async Task<List<Project>> GetProjectsAsync()
        {
            await InitAsync();
            return await _database.Table<Project>().ToListAsync();
        }

        public async Task<int> SaveProjectAsync(Project project)
        {
            await InitAsync();
            if (project.Id != 0) return await _database.UpdateAsync(project);
            else return await _database.InsertAsync(project);
        }

        public async Task<int> DeleteProjectAsync(Project project)
        {
            await InitAsync();
            return await _database.DeleteAsync(project);
        }
    }
}