using ExpenseTracker.Models.Backup;

namespace ExpenseTracker.Services.Interfaces;

public interface IDatabaseMaintenanceService
{
    string DatabasePath { get; }

    int CurrentSchemaVersion { get; }

    Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default);

    Task ExportEncryptedDatabaseAsync(string destinationPath, string backupPassword, CancellationToken cancellationToken = default);

    Task<DatabaseValidationResult> ValidateDatabaseAsync(string databasePath, string password, CancellationToken cancellationToken = default);

    Task CloseConnectionAsync(CancellationToken cancellationToken = default);
}
