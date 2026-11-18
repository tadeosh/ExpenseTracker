using ExpenseTracker.Models.Backup;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text.Json;

namespace ExpenseTracker.Services;

public sealed partial class PortableBackupService
{
    public async Task<PortableBackupRestoreResult> RestoreBackupAsync(string backupFilePath, string backupPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPassword);

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedBackupPath = Path.GetFullPath(backupFilePath);

        if (!File.Exists(normalizedBackupPath))
            throw new FileNotFoundException("Plik kopii zapasowej nie istnieje.", normalizedBackupPath);

        await _operationLock.WaitAsync(cancellationToken);

        string? workingDirectory = null;

        try
        {
            workingDirectory = CreateWorkingDirectory();

            var extractedDatabasePath = Path.Combine(workingDirectory, DatabaseEntryName);

            var manifest = await ExtractAndValidatePortableBackupAsync(
                normalizedBackupPath,
                extractedDatabasePath,
                backupPassword,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var databaseRestoreResult = await _databaseMaintenanceService.RestoreEncryptedDatabaseAsync(
                extractedDatabasePath,
                backupPassword,
                cancellationToken);

            if (!databaseRestoreResult.IsSuccessful)
                throw new InvalidOperationException("Przywracanie bazy danych nie zakończyło się powodzeniem.");

            if (databaseRestoreResult.RestoredSchemaVersion != manifest.DatabaseSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Wersja przywróconej bazy ({databaseRestoreResult.RestoredSchemaVersion}) nie zgadza się z wersją manifestu ({manifest.DatabaseSchemaVersion}).");
            }

            _logger.LogInformation(
                "Przywrócono przenośną kopię {BackupPath}. Wersja formatu: {FormatVersion}, wersja schematu: {SchemaVersion}.",
                normalizedBackupPath,
                manifest.FormatVersion,
                databaseRestoreResult.RestoredSchemaVersion);

            return PortableBackupRestoreResult.Success(manifest, databaseRestoreResult);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie udało się przywrócić przenośnej kopii {BackupPath}.", normalizedBackupPath);

            throw;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(workingDirectory))
                DeleteDirectoryIfExists(workingDirectory);

            _operationLock.Release();
        }
    }

    private async Task<BackupManifest> ExtractAndValidatePortableBackupAsync(
        string backupFilePath,
        string extractedDatabasePath,
        string backupPassword,
        CancellationToken cancellationToken)
    {
        BackupManifest manifest;

        try
        {
            using var archive = ZipFile.OpenRead(backupFilePath);

            var manifestEntry = archive.GetEntry(ManifestEntryName);

            if (manifestEntry is null)
                throw new InvalidDataException("Kopia zapasowa nie zawiera pliku manifest.json.");

            await using (var manifestStream = manifestEntry.Open())
            {
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(
                    manifestStream,
                    JsonOptions,
                    cancellationToken)
                    ?? throw new InvalidDataException("Plik manifest.json jest pusty.");
            }

            var manifestValidation = ValidateManifest(manifest);

            if (manifestValidation is not null)
            {
                throw new InvalidDataException(
                    manifestValidation.ErrorMessage
                    ?? "Manifest kopii zapasowej jest nieprawidłowy.");
            }

            var databaseEntry = archive.GetEntry(manifest.DatabaseFileName);

            if (databaseEntry is null)
            {
                throw new InvalidDataException(
                    $"Kopia zapasowa nie zawiera pliku {manifest.DatabaseFileName}.");
            }

            await using var databaseEntryStream = databaseEntry.Open();

            await using var extractedDatabaseStream = new FileStream(
                extractedDatabasePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                true);

            await databaseEntryStream.CopyToAsync(
                extractedDatabaseStream,
                cancellationToken);

            await extractedDatabaseStream.FlushAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                "Plik nie jest prawidłową kopią zapasową ExpenseTrackera.",
                ex);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var actualSha256 = await CalculateSha256Async(
            extractedDatabasePath,
            cancellationToken);

        if (!string.Equals(
                actualSha256,
                manifest.DatabaseSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Suma kontrolna bazy danych jest nieprawidłowa. Kopia może być uszkodzona albo zmodyfikowana.");
        }

        var databaseValidation = await _databaseMaintenanceService.ValidateDatabaseAsync(
            extractedDatabasePath,
            backupPassword,
            cancellationToken);

        if (!databaseValidation.IsValid)
        {
            throw new InvalidDataException(
                databaseValidation.ErrorMessage
                ?? "Walidacja bazy danych z kopii zapasowej nie powiodła się.");
        }

        if (databaseValidation.SchemaVersion != manifest.DatabaseSchemaVersion)
        {
            throw new InvalidDataException(
                $"Wersja schematu w manifeście ({manifest.DatabaseSchemaVersion}) nie zgadza się z wersją bazy ({databaseValidation.SchemaVersion}).");
        }

        return manifest;
    }
}
