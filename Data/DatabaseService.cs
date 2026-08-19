using SQLite;
using ExpenseTracker.Models;

namespace ExpenseTracker.Data
{
    public class DatabaseService
    {
        // Obiekt reprezentujący połączenie z bazą
        private SQLiteAsyncConnection _database = null!;

        // Inicjalizacja bazy danych (tworzenie pliku i tabel z szyfrowaniem SQLCipher)
        private async Task InitAsync()
        {
            // Jeśli połączenie już istnieje, nie robimy nic
            if (_database != null)
                return;

            // Ustalenie bezpiecznej ścieżki do pliku na danym systemie (Android/Windows)
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "ExpenseTracker.db3");

            //if (File.Exists(dbPath)) File.Delete(dbPath);

            // NOWOŚĆ: Wyciągamy hasło ze sprzętowego, szyfrowanego schowka telefonu
            var dbPassword = await SecureStorage.Default.GetAsync("DbPassword");

            if (string.IsNullOrEmpty(dbPassword))
            {
                // Ekstremalne zabezpieczenie: jeśli aplikacja jakoś tu dotrze bez hasła, rzucamy wyjątek
                throw new Exception("Brak głównego hasła do bazy danych!");
            }

            // NOWOŚĆ: Konfiguracja SQLCipher z 256-bitowym kluczem AES
            var options = new SQLiteConnectionString(dbPath, true, key: dbPassword);

            // Otwarcie połączenia z zaszyfrowaną bazą
            _database = new SQLiteAsyncConnection(options);

            // Magia SQLite: Automatyczne tworzenie tabel na podstawie naszych klas
            await _database.CreateTableAsync<Account>();
            await _database.CreateTableAsync<Category>();
            await _database.CreateTableAsync<Project>();
            await _database.CreateTableAsync<Transaction>();
            await _database.CreateTableAsync<ExchangeRate>();
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
        /*public async Task<decimal> GetAccountBalanceAsync(int accountId)
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
                    // Genialne w swojej prostocie: mnożymy przez odwrotność kursu (1 / kurs)
                    balance += transfer.Amount * (1m / transfer.ExchangeRate.Value);
                }
                else
                {
                    balance += transfer.Amount;
                }
            }

            return balance;
        } */

        public async Task<Dictionary<int, decimal>>GetAllAccountBalancesAsync()
        {
            await InitAsync();

            var accounts = await _database
                .Table<Account>()
                .ToListAsync();

            var transactions = await _database
                .Table<Transaction>()
                .ToListAsync();

            var result = accounts.ToDictionary(
                a => a.Id,
                a => a.InitialBalance);

            foreach (var transaction in transactions)
            {
                switch (transaction.Type)
                {
                    case TransactionType.Income:

                        result[transaction.AccountId] +=
                            transaction.Amount;

                        break;

                    case TransactionType.Expense:

                        result[transaction.AccountId] -=
                            transaction.Amount;

                        break;

                    case TransactionType.Transfer:

                        result[transaction.AccountId] -=
                            transaction.Amount;

                        if (transaction.DestinationAccountId.HasValue)
                        {
                            var amountToAdd = transaction.Amount;

                            if (transaction.ExchangeRate.HasValue &&
                                transaction.ExchangeRate > 0)
                            {
                                amountToAdd *=
                                    (1m /
                                     transaction.ExchangeRate.Value);
                            }

                            result[
                                transaction.DestinationAccountId.Value]
                                += amountToAdd;
                        }

                        break;
                }
            }

            return result;
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

        //===================================================
        // --- ZARZĄDZANIE KURSAMI WALUT ---
        //===================================================

        public async Task<List<ExchangeRate>> GetExchangeRatesAsync()
        {
            await InitAsync();
            return await _database.Table<ExchangeRate>().OrderByDescending(e => e.Date).ToListAsync();
        }

        public async Task SaveExchangeRateAsync(ExchangeRate rate)
        {
            await InitAsync();
            if (rate.Id != 0)
                await _database.UpdateAsync(rate);
            else
                await _database.InsertAsync(rate);
        }

        public async Task DeleteExchangeRateAsync(ExchangeRate rate)
        {
            await InitAsync();
            await _database.DeleteAsync(rate);
        }

        // MAGIA: Inteligentne pobieranie kursu na konkretny dzień (lub ostatniego znanego)
        public async Task<decimal?> GetApplicableExchangeRateAsync(string sourceCurrency, string targetCurrency, DateTime transactionDate)
        {
            await InitAsync();

            // Szukamy najnowszego kursu, który został dodany przed datą transakcji lub dokładnie w tym samym dniu
            var rate = await _database.Table<ExchangeRate>()
                .Where(e => e.SourceCurrency == sourceCurrency && e.TargetCurrency == targetCurrency && e.Date <= transactionDate)
                .OrderByDescending(e => e.Date)
                .FirstOrDefaultAsync();

            if (rate != null)
                return rate.Rate;

            // Jeśli użytkownik wpisał kurs w odwrotną stronę (np. mamy PLN->EUR, a szukamy EUR->PLN)
            var reverseRate = await _database.Table<ExchangeRate>()
                .Where(e => e.SourceCurrency == targetCurrency && e.TargetCurrency == sourceCurrency && e.Date <= transactionDate)
                .OrderByDescending(e => e.Date)
                .FirstOrDefaultAsync();

            if (reverseRate != null && reverseRate.Rate > 0)
                return 1m / reverseRate.Rate; // Zwracamy matematyczną odwrotność

            return null; // Brak kursu w bazie
        }

        // ===================================================
        // --- RESETOWANIE BAZY DANYCH (FACTORY RESET) ---
        // ===================================================

        public async Task WipeAllDataAsync()
        {
            await InitAsync();

            // Kaskadowo usuwamy wszystko. Kolejność nie ma aż takiego znaczenia dla SQLite w trybie prostej bazy,
            // ale dobrą praktyką jest usuwanie najpierw dzieci (transakcji), potem rodziców (konta/kategorie).
            await _database.DeleteAllAsync<Transaction>();
            await _database.DeleteAllAsync<ExchangeRate>();
            await _database.DeleteAllAsync<Account>();
            await _database.DeleteAllAsync<Project>();
            await _database.DeleteAllAsync<Category>();

            // Czyszczenie ustawień zapisanych w preferencjach (opcjonalnie, ale wskazane przy "Factory Reset")
            Preferences.Default.Remove("DefaultCurrency");
            Preferences.Default.Remove("FavoriteCurrencies");
            // Nie usuwamy wybranego języka i motywu, żeby aplikacja nie zgłupiała
        }

    }
}