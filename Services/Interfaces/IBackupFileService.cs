using ExpenseTracker.Models.Backup;

namespace ExpenseTracker.Services.Interfaces;

public interface IBackupFileService
{
    Task<BackupFileSaveResult> SaveBackupAsync(PortableBackupCreationResult backup, CancellationToken cancellationToken = default);

    Task<BackupFilePickResult> PickBackupAsync(CancellationToken cancellationToken = default);

    void DeletePickedTemporaryFile(BackupFilePickResult pickedBackup);
}
