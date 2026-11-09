using CommunityToolkit.Maui.Storage;
using ExpenseTracker.Models.Backup;
using ExpenseTracker.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace ExpenseTracker.Services;

public sealed class BackupFileService : IBackupFileService
{
    private const string BackupExtension = ".etbackup";
    private const string BackupMimeType = "application/octet-stream";

    private readonly IFileSaver _fileSaver;
    private readonly ITemporaryDirectoryProvider _temporaryDirectoryProvider;
    private readonly ILogger<BackupFileService> _logger;

    public BackupFileService(IFileSaver fileSaver, ITemporaryDirectoryProvider temporaryDirectoryProvider, ILogger<BackupFileService> logger)
    {
        _fileSaver = fileSaver ?? throw new ArgumentNullException(nameof(fileSaver));
        _temporaryDirectoryProvider = temporaryDirectoryProvider ?? throw new ArgumentNullException(nameof(temporaryDirectoryProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BackupFileSaveResult> SaveBackupAsync(PortableBackupCreationResult backup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backup);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(backup.TemporaryFilePath) || !File.Exists(backup.TemporaryFilePath))
            return BackupFileSaveResult.Failure("Tymczasowy plik kopii zapasowej nie istnieje.");

        if (string.IsNullOrWhiteSpace(backup.SuggestedFileName))
            return BackupFileSaveResult.Failure("Nie udało się określić nazwy pliku kopii zapasowej.");

        try
        {
            await using var backupStream = new FileStream(backup.TemporaryFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);

            var result = await _fileSaver.SaveAsync(backup.SuggestedFileName, backupStream, cancellationToken);

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Kopia zapasowa została zapisana w lokalizacji {BackupPath}.", result.FilePath);

                return BackupFileSaveResult.Success(result.FilePath);
            }

            if (result.Exception is OperationCanceledException || cancellationToken.IsCancellationRequested)
                return BackupFileSaveResult.Cancelled();

            var errorMessage = result.Exception?.Message ?? "Nie udało się zapisać kopii zapasowej.";

            _logger.LogWarning(result.Exception, "Zapisywanie kopii zapasowej nie powiodło się.");

            return BackupFileSaveResult.Failure(errorMessage);
        }
        catch (OperationCanceledException)
        {
            return BackupFileSaveResult.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wystąpił błąd podczas zapisywania kopii zapasowej.");

            return BackupFileSaveResult.Failure("Nie udało się zapisać kopii zapasowej.");
        }
    }

    public async Task<BackupFilePickResult> PickBackupAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Wybierz kopię zapasową ExpenseTrackera",
                FileTypes = CreateBackupFileType()
            };

            var selectedFile = await FilePicker.Default.PickAsync(options);

            if (selectedFile is null)
                return BackupFilePickResult.Cancelled();

            if (!selectedFile.FileName.EndsWith(BackupExtension, StringComparison.OrdinalIgnoreCase))
                return BackupFilePickResult.Failure("Wybrany plik nie ma rozszerzenia .etbackup.");

            var importDirectory = CreateImportDirectory();
            var temporaryFilePath = Path.Combine(importDirectory, $"{Guid.NewGuid():N}{BackupExtension}");

            try
            {
                await using var sourceStream = await selectedFile.OpenReadAsync();
                await using var destinationStream = new FileStream(temporaryFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);

                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
                await destinationStream.FlushAsync(cancellationToken);
            }
            catch
            {
                DeleteFileIfExists(temporaryFilePath);
                DeleteDirectoryIfEmpty(importDirectory);

                throw;
            }

            _logger.LogInformation("Wybraną kopię {OriginalFileName} skopiowano do katalogu tymczasowego.", selectedFile.FileName);

            return BackupFilePickResult.Success(temporaryFilePath, selectedFile.FileName);
        }
        catch (OperationCanceledException)
        {
            return BackupFilePickResult.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wystąpił błąd podczas wybierania kopii zapasowej.");

            return BackupFilePickResult.Failure("Nie udało się odczytać wybranej kopii zapasowej.");
        }
    }

    public void DeletePickedTemporaryFile(BackupFilePickResult pickedBackup)
    {
        ArgumentNullException.ThrowIfNull(pickedBackup);

        if (string.IsNullOrWhiteSpace(pickedBackup.TemporaryFilePath))
            return;

        var temporaryFilePath = pickedBackup.TemporaryFilePath;
        var directory = Path.GetDirectoryName(temporaryFilePath);

        DeleteFileIfExists(temporaryFilePath);

        if (!string.IsNullOrWhiteSpace(directory))
            DeleteDirectoryIfEmpty(directory);
    }

    private string CreateImportDirectory()
    {
        var directory = Path.Combine(_temporaryDirectoryProvider.TemporaryDirectory, "ExpenseTrackerBackupImport", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static FilePickerFileType CreateBackupFileType()
    {
        return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, new[] { BackupMimeType } },
            { DevicePlatform.WinUI, new[] { BackupExtension } },
            { DevicePlatform.iOS, new[] { "public.data" } },
            { DevicePlatform.MacCatalyst, new[] { "public.data" } }
        });
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void DeleteDirectoryIfEmpty(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            if (!Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path);
        }
        catch
        {
            // Cleanup nie powinien przesłonić właściwego wyniku operacji.
        }
    }
}
