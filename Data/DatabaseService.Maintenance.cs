using ExpenseTracker.Models;
using ExpenseTracker.Models.Backup;
using ExpenseTracker.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SQLite;

namespace ExpenseTracker.Data;

public partial class DatabaseService : IDatabaseMaintenanceService
{
    private const string DatabaseFileName = "ExpenseTracker.db3";
    private const int SupportedSchemaVersion = 1;
    private const string BackupSchemaName = "portable_backup";
    private const int MinimumBackupPasswordLength = 10;

    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _maintenanceLock = new(1, 1);

    private ILogger<DatabaseService> _logger = NullLogger<DatabaseService>.Instance;

    public int CurrentSchemaVersion => SupportedSchemaVersion;

    public string DatabasePath
    {
        get
        {
            if (_database is not null)
                return _database.DatabasePath;

            return Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
        }
    }

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await InitAsync();

        cancellationToken.ThrowIfCancellationRequested();

        return await GetSchemaVersionCoreAsync();
    }

    public async Task ExportEncryptedDatabaseAsync(string destinationPath, string backupPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPassword);

        if (backupPassword.Length < MinimumBackupPasswordLength)
        {
            throw new ArgumentException(
                $"Hasło kopii zapasowej musi zawierać co najmniej {MinimumBackupPasswordLength} znaków.",
                nameof(backupPassword));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedDestinationPath = Path.GetFullPath(destinationPath);
        var normalizedSourcePath = Path.GetFullPath(DatabasePath);

        if (string.Equals(normalizedDestinationPath, normalizedSourcePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Plik kopii zapasowej nie może nadpisywać aktywnej bazy danych.");
        }

        var destinationDirectory = Path.GetDirectoryName(normalizedDestinationPath);

        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new InvalidOperationException("Nie udało się określić katalogu docelowego kopii.");

        Directory.CreateDirectory(destinationDirectory);

        await _maintenanceLock.WaitAsync(cancellationToken);

        try
        {
            await InitAsync();

            cancellationToken.ThrowIfCancellationRequested();

            DeleteDatabaseFiles(normalizedDestinationPath);

            var escapedDestinationPath = EscapeSqlLiteral(normalizedDestinationPath);
            var escapedBackupPassword = EscapeSqlLiteral(backupPassword);
            var databaseAttached = false;

            try
            {
                var attachSql =
                    $"ATTACH DATABASE '{escapedDestinationPath}' AS {BackupSchemaName} KEY '{escapedBackupPassword}'";

                await _database.ExecuteAsync(attachSql);

                databaseAttached = true;

                cancellationToken.ThrowIfCancellationRequested();

                await _database.ExecuteScalarAsync<string>(
                    $"SELECT sqlcipher_export('{BackupSchemaName}')");

                await _database.ExecuteAsync(
                    $"PRAGMA {BackupSchemaName}.user_version = {SupportedSchemaVersion}");

                cancellationToken.ThrowIfCancellationRequested();

                await _database.ExecuteAsync(
                    $"DETACH DATABASE {BackupSchemaName}");

                databaseAttached = false;
            }
            finally
            {
                if (databaseAttached)
                {
                    try
                    {
                        await _database.ExecuteAsync(
                            $"DETACH DATABASE {BackupSchemaName}");
                    }
                    catch (Exception detachException)
                    {
                        _logger.LogWarning(
                            detachException,
                            "Nie udało się odłączyć eksportowanej bazy {DatabasePath}.",
                            normalizedDestinationPath);
                    }
                }
            }

            if (!File.Exists(normalizedDestinationPath))
            {
                throw new InvalidOperationException(
                    "SQLCipher nie utworzył pliku kopii zapasowej.");
            }

            var validationResult = await ValidateDatabaseCoreAsync(
                normalizedDestinationPath,
                backupPassword,
                cancellationToken);

            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException(
                    $"Utworzona kopia nie przeszła walidacji: {validationResult.ErrorMessage}");
            }

            _logger.LogInformation(
                "Utworzono zaszyfrowaną kopię bazy w {DatabasePath}. Wersja schematu: {SchemaVersion}.",
                normalizedDestinationPath,
                validationResult.SchemaVersion);
        }
        catch (Exception ex)
        {
            DeleteDatabaseFiles(normalizedDestinationPath);

            _logger.LogError(
                ex,
                "Nie udało się utworzyć zaszyfrowanej kopii bazy w {DatabasePath}.",
                normalizedDestinationPath);

            throw;
        }
        finally
        {
            _maintenanceLock.Release();
        }
    }

    public async Task<DatabaseValidationResult> ValidateDatabaseAsync(string databasePath, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        cancellationToken.ThrowIfCancellationRequested();

        return await ValidateDatabaseCoreAsync(
            Path.GetFullPath(databasePath),
            password,
            cancellationToken);
    }

    public async Task CloseConnectionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _maintenanceLock.WaitAsync(cancellationToken);

        try
        {
            await CloseConnectionCoreAsync();
        }
        finally
        {
            _maintenanceLock.Release();
        }
    }

    private async Task<DatabaseValidationResult> ValidateDatabaseCoreAsync(string databasePath, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(databasePath))
        {
            return DatabaseValidationResult.Failure(
                "Plik bazy danych nie istnieje.");
        }

        SQLiteAsyncConnection? validationConnection = null;

        try
        {
            var connectionString = new SQLiteConnectionString(
                databasePath,
                true,
                key: password);

            validationConnection = new SQLiteAsyncConnection(connectionString);

            var integrityResult = await validationConnection.ExecuteScalarAsync<string>(
                "PRAGMA integrity_check");

            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseValidationResult.Failure(
                    $"Kontrola integralności bazy zakończyła się wynikiem: {integrityResult}");
            }

            var schemaVersion = await validationConnection.ExecuteScalarAsync<int>(
                "PRAGMA user_version");

            if (schemaVersion <= 0)
            {
                return DatabaseValidationResult.Failure(
                    "Kopia nie zawiera obsługiwanej wersji schematu.");
            }

            if (schemaVersion > SupportedSchemaVersion)
            {
                return DatabaseValidationResult.Failure(
                    $"Kopia używa nowszej wersji schematu {schemaVersion}. Obsługiwana wersja to {SupportedSchemaVersion}.");
            }

            var requiredTables = new[]
            {
                "Account",
                "Category",
                "Project",
                "Transaction",
                "ExchangeRate",
                "RecurringTransaction"
            };

            foreach (var tableName in requiredTables)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tableCount = await validationConnection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?",
                    tableName);

                if (tableCount != 1)
                {
                    return DatabaseValidationResult.Failure(
                        $"W kopii brakuje wymaganej tabeli {tableName}.");
                }
            }

            return DatabaseValidationResult.Success(schemaVersion);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Walidacja bazy {DatabasePath} nie powiodła się.",
                databasePath);

            return DatabaseValidationResult.Failure(
                "Nie udało się otworzyć kopii. Plik może być uszkodzony albo hasło jest nieprawidłowe.");
        }
        finally
        {
            if (validationConnection is not null)
            {
                try
                {
                    await validationConnection.CloseAsync();
                }
                catch (Exception closeException)
                {
                    _logger.LogWarning(
                        closeException,
                        "Nie udało się zamknąć połączenia walidacyjnego do {DatabasePath}.",
                        databasePath);
                }
            }
        }
    }

    private async Task InitializeSchemaAsync()
    {
        await _database.CreateTableAsync<Account>();
        await _database.CreateTableAsync<Category>();
        await _database.CreateTableAsync<Project>();
        await _database.CreateTableAsync<Transaction>();
        await _database.CreateTableAsync<ExchangeRate>();
        await _database.CreateTableAsync<RecurringTransaction>();

        var schemaVersion = await GetSchemaVersionCoreAsync();

        if (schemaVersion > SupportedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Wersja schematu bazy danych {schemaVersion} jest nowsza niż obsługiwana wersja {SupportedSchemaVersion}.");
        }

        if (schemaVersion < 1)
            await ApplyMigrationToVersion1Async();
    }

    private async Task ApplyMigrationToVersion1Async()
    {
        _logger.LogInformation(
            "Rozpoczynanie migracji bazy danych do wersji 1.");

        await _database.ExecuteAsync(
            "UPDATE Category SET IsArchived = 0 WHERE IsArchived IS NULL");

        await _database.ExecuteAsync(
            "UPDATE Project SET IsArchived = 0 WHERE IsArchived IS NULL");

        await SetSchemaVersionCoreAsync(1);

        _logger.LogInformation(
            "Migracja bazy danych do wersji 1 została zakończona.");
    }

    private async Task<int> GetSchemaVersionCoreAsync()
    {
        return await _database.ExecuteScalarAsync<int>(
            "PRAGMA user_version");
    }

    private async Task SetSchemaVersionCoreAsync(int version)
    {
        if (version < 0)
            throw new ArgumentOutOfRangeException(nameof(version));

        if (version > SupportedSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                $"Nie można ustawić wersji schematu wyższej niż {SupportedSchemaVersion}.");
        }

        await _database.ExecuteAsync(
            $"PRAGMA user_version = {version}");
    }

    private async Task CloseConnectionCoreAsync()
    {
        if (_database is null)
        {
            SQLiteAsyncConnection.ResetPool();
            return;
        }

        var database = _database;

        try
        {
            _logger.LogInformation(
                "Zamykanie połączenia z bazą danych {DatabasePath}.",
                database.DatabasePath);

            await database.CloseAsync();

            SQLiteAsyncConnection.ResetPool();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Nie udało się poprawnie zamknąć połączenia z bazą danych.");

            throw;
        }
        finally
        {
            _database = null!;
        }
    }

    private async Task ResetConnectionAfterInitializationFailureAsync()
    {
        if (_database is null)
        {
            SQLiteAsyncConnection.ResetPool();
            return;
        }

        try
        {
            await _database.CloseAsync();

            SQLiteAsyncConnection.ResetPool();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Nie udało się zamknąć połączenia po błędzie inicjalizacji bazy.");
        }
        finally
        {
            _database = null!;
        }
    }

    private static string EscapeSqlLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static void DeleteDatabaseFiles(string databasePath)
    {
        DeleteFileIfExists(databasePath);
        DeleteFileIfExists($"{databasePath}-wal");
        DeleteFileIfExists($"{databasePath}-shm");
        DeleteFileIfExists($"{databasePath}-journal");
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
