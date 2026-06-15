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
    }
}