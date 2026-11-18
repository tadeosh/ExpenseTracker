using ExpenseTracker.Models.Backup;
using Microsoft.Extensions.Logging;
using SQLite;

namespace ExpenseTracker.Data;

public partial class DatabaseService
{
    private const string RestoreTargetSchemaName = "restore_target";
    private const string SafetyBackupSchemaName = "restore_safety";

    internal Func<Task>? RestoreAfterReplacementHook { get; set; }

    public async Task<DatabaseRestoreResult> RestoreEncryptedDatabaseAsync(string sourceDatabasePath, string sourcePassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePassword);

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSourcePath = Path.GetFullPath(sourceDatabasePath);
        var normalizedActivePath = Path.GetFullPath(DatabasePath);

        if (!File.Exists(normalizedSourcePath))
            throw new FileNotFoundException("Plik bazy przeznaczonej do przywrócenia nie istnieje.", normalizedSourcePath);

        if (string.Equals(normalizedSourcePath, normalizedActivePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Źródłowa baza restore nie może być aktywną bazą aplikacji.");

        await _maintenanceLock.WaitAsync(cancellationToken);

        var operationId = Guid.NewGuid().ToString("N");
        var activeDirectory = Path.GetDirectoryName(normalizedActivePath);

        if (string.IsNullOrWhiteSpace(activeDirectory))
        {
            _maintenanceLock.Release();
            throw new InvalidOperationException("Nie udało się określić katalogu aktywnej bazy danych.");
        }

        var stagedDatabasePath = Path.Combine(activeDirectory, $"{DatabaseFileName}.restore-stage-{operationId}");
        var safetyDatabasePath = Path.Combine(activeDirectory, $"{DatabaseFileName}.restore-safety-{operationId}");

        var activeDatabaseWasReplaced = false;
        var rollbackCompleted = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await InitAsync();

            var sourceValidation = await ValidateDatabaseCoreAsync(normalizedSourcePath, sourcePassword, cancellationToken);

            if (!sourceValidation.IsValid)
                throw new InvalidOperationException(sourceValidation.ErrorMessage ?? "Źródłowa baza restore nie przeszła walidacji.");

            var localDatabasePassword = await GetActiveDatabasePasswordAsync();

            if (string.IsNullOrWhiteSpace(localDatabasePassword))
                throw new InvalidOperationException("Brak lokalnego klucza aktywnej bazy danych.");

            DeleteDatabaseFiles(stagedDatabasePath);
            DeleteDatabaseFiles(safetyDatabasePath);

            await ExportActiveDatabaseToSafetyAsync(safetyDatabasePath, localDatabasePassword, cancellationToken);

            var safetyValidation = await ValidateDatabaseCoreAsync(safetyDatabasePath, localDatabasePassword, cancellationToken);

            if (!safetyValidation.IsValid)
                throw new InvalidOperationException($"Nie udało się utworzyć poprawnej kopii bezpieczeństwa: {safetyValidation.ErrorMessage}");

            await ConvertRestoreSourceToLocalDatabaseAsync(
                normalizedSourcePath,
                sourcePassword,
                stagedDatabasePath,
                localDatabasePassword,
                sourceValidation.SchemaVersion,
                cancellationToken);

            var stagedValidation = await ValidateDatabaseCoreAsync(stagedDatabasePath, localDatabasePassword, cancellationToken);

            if (!stagedValidation.IsValid)
                throw new InvalidOperationException($"Przygotowana baza restore nie przeszła walidacji: {stagedValidation.ErrorMessage}");

            if (stagedValidation.SchemaVersion != sourceValidation.SchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Wersja schematu przygotowanej bazy ({stagedValidation.SchemaVersion}) nie zgadza się ze źródłem ({sourceValidation.SchemaVersion}).");
            }

            cancellationToken.ThrowIfCancellationRequested();

            await CloseConnectionCoreAsync();

            DeleteDatabaseFiles(normalizedActivePath);

            File.Move(stagedDatabasePath, normalizedActivePath);
            activeDatabaseWasReplaced = true;

            if (RestoreAfterReplacementHook is not null)
                await RestoreAfterReplacementHook();

            cancellationToken.ThrowIfCancellationRequested();

            await InitAsync();

            var restoredValidation = await ValidateDatabaseCoreAsync(normalizedActivePath, localDatabasePassword, cancellationToken);

            if (!restoredValidation.IsValid)
                throw new InvalidOperationException($"Przywrócona baza nie przeszła walidacji końcowej: {restoredValidation.ErrorMessage}");

            _logger.LogInformation(
                "Przywrócono bazę danych z {SourcePath}. Wersja schematu: {SchemaVersion}.",
                normalizedSourcePath,
                restoredValidation.SchemaVersion);

            DeleteDatabaseFiles(safetyDatabasePath);

            return DatabaseRestoreResult.Success(restoredValidation.SchemaVersion);
        }
        catch (OperationCanceledException) when (!activeDatabaseWasReplaced)
        {
            throw;
        }
        catch (Exception restoreException) when (activeDatabaseWasReplaced)
        {
            _logger.LogError(
                restoreException,
                "Restore bazy nie powiódł się po podmianie aktywnego pliku. Rozpoczynanie rollbacku.");

            try
            {
                await CloseConnectionCoreAsync();

                DeleteDatabaseFiles(normalizedActivePath);

                if (!File.Exists(safetyDatabasePath))
                    throw new InvalidOperationException("Brak lokalnej kopii bezpieczeństwa wymaganej do rollbacku.");

                File.Move(safetyDatabasePath, normalizedActivePath);
                rollbackCompleted = true;

                await InitAsync();

                var localDatabasePassword = await GetActiveDatabasePasswordAsync();

                var rollbackValidation = await ValidateDatabaseCoreAsync(
                    normalizedActivePath,
                    localDatabasePassword,
                    CancellationToken.None);

                if (!rollbackValidation.IsValid)
                    throw new InvalidOperationException($"Rollback przywrócił bazę, która nie przeszła walidacji: {rollbackValidation.ErrorMessage}");

                _logger.LogWarning(
                    "Rollback restore zakończył się powodzeniem. Przywrócono poprzednią bazę w wersji schematu {SchemaVersion}.",
                    rollbackValidation.SchemaVersion);
            }
            catch (Exception rollbackException)
            {
                _logger.LogCritical(
                    rollbackException,
                    "Rollback bazy danych nie powiódł się. Oryginalny błąd restore: {RestoreError}",
                    restoreException.Message);

                throw new AggregateException(
                    "Restore oraz automatyczny rollback bazy danych nie powiodły się.",
                    restoreException,
                    rollbackException);
            }

            throw new InvalidOperationException(
                "Nie udało się przywrócić kopii zapasowej. Poprzednia baza została automatycznie przywrócona.",
                restoreException);
        }
        finally
        {
            RestoreAfterReplacementHook = null;

            DeleteDatabaseFiles(stagedDatabasePath);

            if (!activeDatabaseWasReplaced || rollbackCompleted)
                DeleteDatabaseFiles(safetyDatabasePath);

            _maintenanceLock.Release();
        }
    }

    private async Task ExportActiveDatabaseToSafetyAsync(string destinationPath, string localDatabasePassword, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DeleteDatabaseFiles(destinationPath);

        var escapedDestinationPath = EscapeSqlLiteral(destinationPath);
        var escapedPassword = EscapeSqlLiteral(localDatabasePassword);
        var attached = false;

        try
        {
            await _database.ExecuteAsync(
                $"ATTACH DATABASE '{escapedDestinationPath}' AS {SafetyBackupSchemaName} KEY '{escapedPassword}'");

            attached = true;

            await _database.ExecuteScalarAsync<string>(
                $"SELECT sqlcipher_export('{SafetyBackupSchemaName}')");

            var schemaVersion = await GetSchemaVersionCoreAsync();

            await _database.ExecuteAsync(
                $"PRAGMA {SafetyBackupSchemaName}.user_version = {schemaVersion}");

            cancellationToken.ThrowIfCancellationRequested();

            await _database.ExecuteAsync(
                $"DETACH DATABASE {SafetyBackupSchemaName}");

            attached = false;
        }
        finally
        {
            if (attached)
            {
                try
                {
                    await _database.ExecuteAsync($"DETACH DATABASE {SafetyBackupSchemaName}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nie udało się odłączyć lokalnej bazy bezpieczeństwa.");
                }
            }
        }

        if (!File.Exists(destinationPath))
            throw new InvalidOperationException("Nie udało się utworzyć lokalnej kopii bezpieczeństwa przed restore.");
    }

    private async Task ConvertRestoreSourceToLocalDatabaseAsync(
        string sourcePath,
        string sourcePassword,
        string destinationPath,
        string destinationPassword,
        int schemaVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DeleteDatabaseFiles(destinationPath);

        SQLiteAsyncConnection? sourceConnection = null;
        var targetAttached = false;

        try
        {
            var sourceOptions = new SQLiteConnectionString(sourcePath, true, key: sourcePassword);
            sourceConnection = new SQLiteAsyncConnection(sourceOptions);

            var sourceIntegrity = await sourceConnection.ExecuteScalarAsync<string>("PRAGMA integrity_check");

            if (!string.Equals(sourceIntegrity, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Źródłowa baza restore jest uszkodzona: {sourceIntegrity}");

            var escapedDestinationPath = EscapeSqlLiteral(destinationPath);
            var escapedDestinationPassword = EscapeSqlLiteral(destinationPassword);

            await sourceConnection.ExecuteAsync(
                $"ATTACH DATABASE '{escapedDestinationPath}' AS {RestoreTargetSchemaName} KEY '{escapedDestinationPassword}'");

            targetAttached = true;

            await sourceConnection.ExecuteScalarAsync<string>(
                $"SELECT sqlcipher_export('{RestoreTargetSchemaName}')");

            await sourceConnection.ExecuteAsync(
                $"PRAGMA {RestoreTargetSchemaName}.user_version = {schemaVersion}");

            cancellationToken.ThrowIfCancellationRequested();

            await sourceConnection.ExecuteAsync(
                $"DETACH DATABASE {RestoreTargetSchemaName}");

            targetAttached = false;
        }
        finally
        {
            if (sourceConnection is not null)
            {
                if (targetAttached)
                {
                    try
                    {
                        await sourceConnection.ExecuteAsync($"DETACH DATABASE {RestoreTargetSchemaName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Nie udało się odłączyć bazy staging restore.");
                    }
                }

                try
                {
                    await sourceConnection.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nie udało się zamknąć źródłowej bazy restore.");
                }
            }
        }

        if (!File.Exists(destinationPath))
            throw new InvalidOperationException("Nie udało się przygotować lokalnej bazy staging dla restore.");
    }

    private async Task<string> GetActiveDatabasePasswordAsync()
    {
        if (!string.IsNullOrWhiteSpace(_databasePasswordOverride))
            return _databasePasswordOverride;

        var password = await SecureStorage.Default.GetAsync("DbPassword");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Brak głównego hasła do aktywnej bazy danych.");

        return password;
    }
}
