using ExpenseTracker.Models.Backup;

namespace ExpenseTracker.Services.Interfaces;

public interface IPortableBackupService
{
    Task<PortableBackupCreationResult> CreateBackupAsync(string backupPassword, CancellationToken cancellationToken = default);

    Task<PortableBackupValidationResult> ValidateBackupAsync(string backupFilePath, string backupPassword, CancellationToken cancellationToken = default);

    void DeleteTemporaryBackup(PortableBackupCreationResult backup);
}
