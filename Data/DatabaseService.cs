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

            // 2. NOWOŚĆ: Migracja starych danych
            // Zamienia wartości NULL w nowej kolumnie na 0 (false), przywracając je na ekrany
            await _database.ExecuteAsync("UPDATE Category SET IsArchived = 0 WHERE IsArchived IS NULL");
            await _database.ExecuteAsync("UPDATE Project SET IsArchived = 0 WHERE IsArchived IS NULL");
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

        public async Task<Transaction?> GetTransactionAsync(int id)
        {
            await InitAsync();
            return await _database.Table<Transaction>().FirstOrDefaultAsync(t => t.Id == id);
        }

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

        //= pobieranie  ostatnich transakcji z pełnymi danymi (JOIN z kategorią, projektem i kontem)
        public async Task<List<TransactionDetailDto>> GetRecentTransactionsWithDetailsAsync(int limit = 30)
        {
            await InitAsync();

            var sql = @"
                WITH Perspectives AS (
                -- 1. Zwykłe Przychody (1) i Wydatki (0)
                SELECT 
                    t.Id, t.Amount, t.Date, t.Description, t.Type, t.AccountId,
                    c.Name AS CategoryName, p.Name AS ProjectName, a.Name AS AccountName,
                    CASE WHEN t.Type = 0 THEN -t.Amount ELSE t.Amount END AS SignedAmount,
                    0 AS IsTransferIn,
                    a.Currency AS AccountCurrency -- NOWOŚĆ
                FROM ""Transaction"" t
                LEFT JOIN Category c ON t.CategoryId = c.Id
                LEFT JOIN Project p ON t.ProjectId = p.Id
                LEFT JOIN Account a ON t.AccountId = a.Id
                WHERE t.Type IN (0, 1)

                UNION ALL

                -- 2. Transfery WYCHODZĄCE (Perspektywa Konta Źródłowego)
                SELECT 
                    t.Id, t.Amount, t.Date, t.Description, t.Type, t.AccountId,
                    c.Name AS CategoryName, p.Name AS ProjectName, a.Name AS AccountName,
                    -t.Amount AS SignedAmount,
                    0 AS IsTransferIn,
                    a.Currency AS AccountCurrency -- NOWOŚĆ
                FROM ""Transaction"" t
                LEFT JOIN Category c ON t.CategoryId = c.Id
                LEFT JOIN Project p ON t.ProjectId = p.Id
                LEFT JOIN Account a ON t.AccountId = a.Id
                WHERE t.Type = 2

                UNION ALL

                -- 3. Transfery PRZYCHODZĄCE (Perspektywa Konta Docelowego + Kurs)
                SELECT 
                    t.Id, 
                    CASE WHEN t.ExchangeRate IS NOT NULL AND t.ExchangeRate > 0 
                         THEN (t.Amount * (1.0 / t.ExchangeRate)) 
                         ELSE t.Amount END AS Amount,
                    t.Date, t.Description, t.Type, t.DestinationAccountId AS AccountId,
                    c.Name AS CategoryName, p.Name AS ProjectName, aDest.Name AS AccountName,
                    CASE WHEN t.ExchangeRate IS NOT NULL AND t.ExchangeRate > 0 
                         THEN (t.Amount * (1.0 / t.ExchangeRate)) 
                         ELSE t.Amount END AS SignedAmount,
                    1 AS IsTransferIn,
                    aDest.Currency AS AccountCurrency -- NOWOŚĆ
                FROM ""Transaction"" t
                LEFT JOIN Category c ON t.CategoryId = c.Id
                LEFT JOIN Project p ON t.ProjectId = p.Id
                LEFT JOIN Account aDest ON t.DestinationAccountId = aDest.Id
                WHERE t.Type = 2 AND t.DestinationAccountId IS NOT NULL
            )
            SELECT 
                Id, Amount, Date, Description, Type, AccountId,
                IFNULL(CategoryName, '-') AS CategoryName,
                IFNULL(ProjectName, '-') AS ProjectName,
                IFNULL(AccountName, '-') AS AccountName,
                SignedAmount,
                IsTransferIn,
                AccountCurrency -- NOWOŚĆ W GŁÓWNYM SELECT
                FROM Perspectives
                ORDER BY Date DESC, Id DESC
                LIMIT ?";

            return await _database.QueryAsync<TransactionDetailDto>(sql, limit);
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

        public async Task<List<TransactionDetailDto>> GetTransactionsWithDetailsAsync(
    int? accountId, int? categoryId, int? projectId,
    decimal? minAmount, decimal? maxAmount,
    string? searchText, string sortColumn, bool isAscending)
        {
            await InitAsync();

            var sql = @"
        WITH Perspectives AS (
            -- 1. Zwykłe Przychody (1) i Wydatki (0)
            SELECT 
                t.Id, t.Amount, t.Date, t.Description, t.Type, t.AccountId, 
                t.CategoryId, t.ProjectId, t.AccountId AS FilterAccountId,
                c.Name AS CategoryName, p.Name AS ProjectName, a.Name AS AccountName,
                CASE WHEN t.Type = 0 THEN -t.Amount ELSE t.Amount END AS SignedAmount,
                0 AS IsTransferIn,
                a.Currency AS AccountCurrency
            FROM ""Transaction"" t
            LEFT JOIN Category c ON t.CategoryId = c.Id
            LEFT JOIN Project p ON t.ProjectId = p.Id
            LEFT JOIN Account a ON t.AccountId = a.Id
            WHERE t.Type IN (0, 1)

            UNION ALL

            -- 2. Transfery WYCHODZĄCE (Perspektywa Konta Źródłowego)
            SELECT 
                t.Id, t.Amount, t.Date, t.Description, t.Type, t.AccountId, 
                t.CategoryId, t.ProjectId, t.AccountId AS FilterAccountId,
                c.Name AS CategoryName, p.Name AS ProjectName, a.Name AS AccountName,
                -t.Amount AS SignedAmount,
                0 AS IsTransferIn,
                a.Currency AS AccountCurrency
            FROM ""Transaction"" t
            LEFT JOIN Category c ON t.CategoryId = c.Id
            LEFT JOIN Project p ON t.ProjectId = p.Id
            LEFT JOIN Account a ON t.AccountId = a.Id
            WHERE t.Type = 2

            UNION ALL

            -- 3. Transfery PRZYCHODZĄCE (Perspektywa Konta Docelowego + Kurs)
            SELECT 
                t.Id, 
                CASE WHEN t.ExchangeRate IS NOT NULL AND t.ExchangeRate > 0 
                     THEN (t.Amount * (1.0 / t.ExchangeRate)) 
                     ELSE t.Amount END AS Amount,
                t.Date, t.Description, t.Type, t.DestinationAccountId AS AccountId, 
                t.CategoryId, t.ProjectId, t.DestinationAccountId AS FilterAccountId,
                c.Name AS CategoryName, p.Name AS ProjectName, aDest.Name AS AccountName,
                CASE WHEN t.ExchangeRate IS NOT NULL AND t.ExchangeRate > 0 
                     THEN (t.Amount * (1.0 / t.ExchangeRate)) 
                     ELSE t.Amount END AS SignedAmount,
                1 AS IsTransferIn,
                aDest.Currency AS AccountCurrency
            FROM ""Transaction"" t
            LEFT JOIN Category c ON t.CategoryId = c.Id
            LEFT JOIN Project p ON t.ProjectId = p.Id
            LEFT JOIN Account aDest ON t.DestinationAccountId = aDest.Id
            WHERE t.Type = 2 AND t.DestinationAccountId IS NOT NULL
        )
        SELECT 
            Id, Amount, Date, Description, Type, AccountId,
            IFNULL(CategoryName, '-') AS CategoryName,
            IFNULL(ProjectName, '-') AS ProjectName,
            IFNULL(AccountName, '-') AS AccountName,
            SignedAmount,
            IsTransferIn,
            AccountCurrency
        FROM Perspectives
        WHERE 1=1 ";

            var args = new List<object>();

            // Doklejanie filtrów (teraz kolumny FilterAccountId, CategoryId i ProjectId znów istnieją w CTE!)
            if (accountId.HasValue) { sql += " AND FilterAccountId = ? "; args.Add(accountId.Value); }
            if (categoryId.HasValue) { sql += " AND CategoryId = ? "; args.Add(categoryId.Value); }
            if (projectId.HasValue) { sql += " AND ProjectId = ? "; args.Add(projectId.Value); }
            if (minAmount.HasValue) { sql += " AND Amount >= ? "; args.Add(minAmount.Value); }
            if (maxAmount.HasValue) { sql += " AND Amount <= ? "; args.Add(maxAmount.Value); }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                sql += " AND (Description LIKE ? OR CategoryName LIKE ? OR ProjectName LIKE ?) ";
                var likeParam = $"%{searchText}%";
                args.AddRange(new object[] { likeParam, likeParam, likeParam });
            }

            string direction = isAscending ? "ASC" : "DESC";
            sql += sortColumn switch
            {
                "Date" => $" ORDER BY Date {direction}, Id {direction} ",
                "Account" => $" ORDER BY AccountName COLLATE NOCASE {direction} ",
                "Category" => $" ORDER BY CategoryName COLLATE NOCASE {direction} ",
                "Description" => $" ORDER BY Description COLLATE NOCASE {direction} ",
                "Project" => $" ORDER BY ProjectName COLLATE NOCASE {direction} ",
                "Amount" => $" ORDER BY SignedAmount {direction} ",
                _ => $" ORDER BY Date DESC, Id DESC "
            };

            return await _database.QueryAsync<TransactionDetailDto>(sql, args.ToArray());
        }

        // =================== Klasa pomocnicza =============================
        // Klasa używana wyłącznie wewnętrznie do rzutowania wyników zapytań agregujących SQL
        public class BalanceResult
        {
            public int AccountId { get; set; }
            public decimal Total { get; set; }
        }

        // ====================================================================
        //       OPERACJE DLA WYKRESÓW I RAPORTÓW (STATISTICS)
        // ====================================================================

        public async Task<List<CategoryExpenseSummaryDto>> GetCurrentMonthExpensesAsync()
        {
            await InitAsync();

            var startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var nextMonth = startDate.AddMonths(1);

            // Wyciągamy surowe dane. Waluty i tłumaczenia obsłuży logika biznesowa C#
            var sql = @"
                SELECT 
                    t.Amount, 
                    t.Date,
                    IFNULL(c.Name, '-') AS CategoryName,
                    IFNULL(c.ColorHex, '#9E9E9E') AS ColorHex,
                    a.Currency AS AccountCurrency
                FROM ""Transaction"" t
                LEFT JOIN Category c ON t.CategoryId = c.Id
                LEFT JOIN Account a ON t.AccountId = a.Id
                WHERE t.Type = 0 AND t.Date >= ? AND t.Date < ?";

            return await _database.QueryAsync<CategoryExpenseSummaryDto>(sql, startDate, nextMonth);
        }

        // ==========================================
        // OPERACJE DLA KATEGORII (CATEGORIES)
        // ==========================================

        public async Task<List<Category>> GetCategoriesAsync(bool includeArchived = false)
        {
            await InitAsync();
            var query = _database.Table<Category>();

            // Filtrujemy tylko aktywne, chyba że ktoś wyraźnie zażąda zarchiwizowanych
            if (!includeArchived)
            {
                query = query.Where(c => !c.IsArchived);
            }

            return await query.ToListAsync();
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

            // 1. Oznaczamy główną kategorię jako usuniętą
            category.IsArchived = true;
            int result = await _database.UpdateAsync(category);

            // 2. Kaskadowe ukrywanie podkategorii (Integralność UX)
            var subCategories = await _database.Table<Category>().Where(c => c.ParentId == category.Id).ToListAsync();
            foreach (var sub in subCategories)
            {
                sub.IsArchived = true;
                await _database.UpdateAsync(sub);
            }

            return result;
        }

        // ==========================================
        // OPERACJE DLA PROJEKTÓW (PROJECTS)
        // ==========================================

        public async Task<List<Project>> GetProjectsAsync(bool includeArchived = false)
        {
            await InitAsync();
            var query = _database.Table<Project>();

            if (!includeArchived)
            {
                query = query.Where(p => !p.IsArchived);
            }

            return await query.ToListAsync();
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
            project.IsArchived = true;
            int result = await _database.UpdateAsync(project);

            // Kaskadowe ukrywanie podprojektów
            var subProjects = await _database.Table<Project>().Where(p => p.ParentId == project.Id).ToListAsync();
            foreach (var sub in subProjects)
            {
                sub.IsArchived = true;
                await _database.UpdateAsync(sub);
            }

            return result;
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