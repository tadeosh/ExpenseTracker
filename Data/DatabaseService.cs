using SQLite;
using ExpenseTracker.Models;
using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.Data
{
    public class DatabaseService : IDatabaseService
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

        public async Task<Dictionary<int, decimal>> GetAllAccountBalancesAsync()
        {
            await InitAsync();
            var result = new Dictionary<int, decimal>();

            try
            {
                // 1. Pobieramy początkowe salda kont
                var accounts = await _database.Table<Account>().ToListAsync();
                result = accounts.ToDictionary(a => a.Id, a => a.InitialBalance);

                // UWAGA: Tabela nazywa się "Transaction", co w SQL jest słowem kluczowym, 
                // dlatego w surowych zapytaniach zabezpieczamy ją cudzysłowami: \"Transaction\"

                // 2. Sumujemy Przychody (+)
                var incomes = await _database.QueryAsync<BalanceResult>(
                    "SELECT AccountId, SUM(Amount) as Total FROM \"Transaction\" WHERE Type = ? GROUP BY AccountId",
                    (int)TransactionType.Income);

                foreach (var income in incomes)
                {
                    if (result.ContainsKey(income.AccountId))
                        result[income.AccountId] += income.Total;
                }

                // 3. Sumujemy Wydatki (-)
                var expenses = await _database.QueryAsync<BalanceResult>(
                    "SELECT AccountId, SUM(Amount) as Total FROM \"Transaction\" WHERE Type = ? GROUP BY AccountId",
                    (int)TransactionType.Expense);

                foreach (var expense in expenses)
                {
                    if (result.ContainsKey(expense.AccountId))
                        result[expense.AccountId] -= expense.Total;
                }

                // 4. Sumujemy Transfery wychodzące z danego konta (-)
                var transfersOut = await _database.QueryAsync<BalanceResult>(
                    "SELECT AccountId, SUM(Amount) as Total FROM \"Transaction\" WHERE Type = ? GROUP BY AccountId",
                    (int)TransactionType.Transfer);

                foreach (var transferOut in transfersOut)
                {
                    if (result.ContainsKey(transferOut.AccountId))
                        result[transferOut.AccountId] -= transferOut.Total;
                }

                // 5. Sumujemy Transfery przychodzące na dane konto (+) z uwzględnieniem kursu (ExchangeRate)
                // Logika matematyczna (1 / ExchangeRate) jest wykonywana w locie przez silnik SQLite!
                var transfersInQuery = @"
            SELECT DestinationAccountId as AccountId, 
                   SUM(
                       CASE 
                           WHEN ExchangeRate IS NOT NULL AND ExchangeRate > 0 THEN Amount * (1.0 / ExchangeRate)
                           ELSE Amount 
                       END
                   ) as Total 
            FROM ""Transaction"" 
            WHERE Type = ? AND DestinationAccountId IS NOT NULL 
            GROUP BY DestinationAccountId";

                var transfersIn = await _database.QueryAsync<BalanceResult>(transfersInQuery, (int)TransactionType.Transfer);

                foreach (var transferIn in transfersIn)
                {
                    if (result.ContainsKey(transferIn.AccountId))
                        result[transferIn.AccountId] += transferIn.Total;
                }
            }
            catch (Exception ex)
            {
                // Tutaj docelowo powinno znaleźć się wstrzyknięte ILogger<DatabaseService>
                Console.WriteLine($"[CRITICAL] Błąd podczas obliczania sald: {ex.Message}");
                throw; // Rzucamy dalej, aby ViewModel mógł pokazać błąd użytkownikowi
            }

            return result;
        }

        public async Task<List<Transaction>> GetFilteredTransactionsAsync(int? accountId, int? categoryId, int? projectId, decimal? minAmount, decimal? maxAmount)
        {
            await InitAsync();

            // Startujemy zapytanie (jeszcze nie idzie do bazy)
            var query = _database.Table<Transaction>();

            // Dynamicznie doklejamy warunki WHERE w SQL, jeśli parametry nie są nullami
            if (accountId.HasValue)
                query = query.Where(t => t.AccountId == accountId.Value);

            if (categoryId.HasValue)
                query = query.Where(t => t.CategoryId == categoryId.Value);

            if (projectId.HasValue)
                query = query.Where(t => t.ProjectId == projectId.Value);

            if (minAmount.HasValue)
                query = query.Where(t => t.Amount >= minAmount.Value);

            if (maxAmount.HasValue)
                query = query.Where(t => t.Amount <= maxAmount.Value);

            // Dopiero tutaj faktycznie wysyłamy zapytanie do bazy i pobieramy WĄSKI wycinek danych!
            return await query.ToListAsync();
        }

        // =================== Klasa pomocnicza =============================
        // Klasa używana wyłącznie wewnętrznie do rzutowania wyników zapytań agregujących SQL
        public class BalanceResult
        {
            public int AccountId { get; set; }
            public decimal Total { get; set; }
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
            await _database.RunInTransactionAsync(conn =>
            {
                conn.DeleteAll<Transaction>();
                conn.DeleteAll<ExchangeRate>();
                conn.DeleteAll<Account>();
                conn.DeleteAll<Project>();
                conn.DeleteAll<Category>();
            });

            // Czyszczenie ustawień zapisanych w preferencjach (opcjonalnie, ale wskazane przy "Factory Reset")
            Preferences.Default.Remove("DefaultCurrency");
            Preferences.Default.Remove("FavoriteCurrencies");
            // Nie usuwamy wybranego języka i motywu, żeby aplikacja nie zgłupiała
        }

    }
}