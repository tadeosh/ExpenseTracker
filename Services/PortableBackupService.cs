using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ExpenseTracker.Models.Backup;
using ExpenseTracker.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Services;

public sealed class PortableBackupService : IPortableBackupService
{
    private const string ManifestEntryName = "manifest.json";
    private const string DatabaseEntryName = "database.db3";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IDatabaseMaintenanceService _databaseMaintenanceService;
    private readonly ITemporaryDirectoryProvider _temporaryDirectoryProvider;
    private readonly ILogger<PortableBackupService> _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public PortableBackupService(IDatabaseMaintenanceService databaseMaintenanceService, ITemporaryDirectoryProvider temporaryDirectoryProvider, ILogger<PortableBackupService> logger)
    {
        _databaseMaintenanceService = databaseMaintenanceService ?? throw new ArgumentNullException(nameof(databaseMaintenanceService));
        _temporaryDirectoryProvider = temporaryDirectoryProvider ?? throw new ArgumentNullException(nameof(temporaryDirectoryProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PortableBackupCreationResult> CreateBackupAsync(string backupPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPassword);

        cancellationToken.ThrowIfCancellationRequested();

        await _operationLock.WaitAsync(cancellationToken);

        string? workingDirectory = null;
        string? archivePath = null;

        try
        {
            workingDirectory = CreateWorkingDirectory();

            var exportedDatabasePath = Path.Combine(workingDirectory, DatabaseEntryName);
            archivePath = Path.Combine(_temporaryDirectoryProvider.TemporaryDirectory, CreateTemporaryArchiveName());

            DeleteFileIfExists(archivePath);

            await _databaseMaintenanceService.ExportEncryptedDatabaseAsync(exportedDatabasePath, backupPassword, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var databaseSha256 = await CalculateSha256Async(exportedDatabasePath, cancellationToken);
            var schemaVersion = await _databaseMaintenanceService.GetSchemaVersionAsync(cancellationToken);
            var createdAtUtc = DateTimeOffset.UtcNow;

            var manifest = new BackupManifest
            {
                FormatVersion = BackupManifest.CurrentFormatVersion,
                Application = BackupManifest.ExpectedApplicationName,
                AppVersion = GetApplicationVersion(),
                DatabaseSchemaVersion = schemaVersion,
                CreatedAtUtc = createdAtUtc,
                DatabaseFileName = DatabaseEntryName,
                DatabaseSha256 = databaseSha256,
                Encryption = "SQLCipher",
                PasswordProtection = "UserProvided"
            };

            await CreateArchiveAsync(archivePath, exportedDatabasePath, manifest, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(archivePath))
                throw new InvalidOperationException("Nie udało się utworzyć pliku kopii zapasowej.");

            _logger.LogInformation("Utworzono przenośną kopię zapasową {BackupPath}. Wersja formatu: {FormatVersion}, wersja schematu: {SchemaVersion}.", archivePath, manifest.FormatVersion, manifest.DatabaseSchemaVersion);

            return new PortableBackupCreationResult
            {
                TemporaryFilePath = archivePath,
                SuggestedFileName = CreateSuggestedFileName(createdAtUtc),
                Manifest = manifest
            };
        }
        catch (OperationCanceledException)
        {
            if (!string.IsNullOrWhiteSpace(archivePath))
                DeleteFileIfExists(archivePath);

            throw;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(archivePath))
                DeleteFileIfExists(archivePath);

            _logger.LogError(ex, "Nie udało się utworzyć przenośnej kopii zapasowej.");

            throw;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(workingDirectory))
                DeleteDirectoryIfExists(workingDirectory);

            _operationLock.Release();
        }
    }

    public async Task<PortableBackupValidationResult> ValidateBackupAsync(string backupFilePath, string backupPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPassword);

        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(backupFilePath))
            return PortableBackupValidationResult.Failure("Plik kopii zapasowej nie istnieje.");

        await _operationLock.WaitAsync(cancellationToken);

        string? workingDirectory = null;

        try
        {
            workingDirectory = CreateWorkingDirectory();

            var extractedDatabasePath = Path.Combine(workingDirectory, DatabaseEntryName);

            BackupManifest manifest;

            try
            {
                using var archive = ZipFile.OpenRead(backupFilePath);

                var manifestEntry = archive.GetEntry(ManifestEntryName);

                if (manifestEntry is null)
                    return PortableBackupValidationResult.Failure("Kopia zapasowa nie zawiera pliku manifest.json.");

                await using (var manifestStream = manifestEntry.Open())
                {
                    manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream, JsonOptions, cancellationToken)
                        ?? throw new InvalidDataException("Plik manifest.json jest pusty.");
                }

                var manifestValidation = ValidateManifest(manifest);

                if (manifestValidation is not null)
                    return manifestValidation;

                var databaseEntry = archive.GetEntry(manifest.DatabaseFileName);

                if (databaseEntry is null)
                    return PortableBackupValidationResult.Failure($"Kopia zapasowa nie zawiera pliku {manifest.DatabaseFileName}.", manifest);

                await using var databaseEntryStream = databaseEntry.Open();
                await using var extractedDatabaseStream = new FileStream(extractedDatabasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);

                await databaseEntryStream.CopyToAsync(extractedDatabaseStream, cancellationToken);
                await extractedDatabaseStream.FlushAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidDataException ex)
            {
                _logger.LogWarning(ex, "Plik {BackupPath} nie jest prawidłowym archiwum kopii zapasowej.", backupFilePath);

                return PortableBackupValidationResult.Failure("Plik nie jest prawidłową kopią zapasową ExpenseTrackera.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var actualSha256 = await CalculateSha256Async(extractedDatabasePath, cancellationToken);

            if (!string.Equals(actualSha256, manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
                return PortableBackupValidationResult.Failure("Suma kontrolna bazy danych jest nieprawidłowa. Kopia może być uszkodzona albo zmodyfikowana.", manifest);

            var databaseValidation = await _databaseMaintenanceService.ValidateDatabaseAsync(extractedDatabasePath, backupPassword, cancellationToken);

            if (!databaseValidation.IsValid)
                return PortableBackupValidationResult.Failure(databaseValidation.ErrorMessage ?? "Walidacja bazy danych nie powiodła się.", manifest);

            if (databaseValidation.SchemaVersion != manifest.DatabaseSchemaVersion)
                return PortableBackupValidationResult.Failure($"Wersja schematu w manifeście ({manifest.DatabaseSchemaVersion}) nie zgadza się z wersją bazy ({databaseValidation.SchemaVersion}).", manifest);

            return PortableBackupValidationResult.Success(manifest, databaseValidation);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Walidacja przenośnej kopii {BackupPath} nie powiodła się.", backupFilePath);

            return PortableBackupValidationResult.Failure("Nie udało się zweryfikować kopii zapasowej. Plik może być uszkodzony albo nieobsługiwany.");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(workingDirectory))
                DeleteDirectoryIfExists(workingDirectory);

            _operationLock.Release();
        }
    }

    public void DeleteTemporaryBackup(PortableBackupCreationResult backup)
    {
        ArgumentNullException.ThrowIfNull(backup);

        DeleteFileIfExists(backup.TemporaryFilePath);
    }

    private async Task CreateArchiveAsync(string archivePath, string databasePath, BackupManifest manifest, CancellationToken cancellationToken)
    {
        await using var archiveStream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: false);

        var databaseEntry = archive.CreateEntry(DatabaseEntryName, CompressionLevel.Optimal);

        await using (var databaseEntryStream = databaseEntry.Open())
        await using (var databaseStream = new FileStream(databasePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
        {
            await databaseStream.CopyToAsync(databaseEntryStream, cancellationToken);
        }

        var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);

        await using var manifestEntryStream = manifestEntry.Open();

        await JsonSerializer.SerializeAsync(manifestEntryStream, manifest, JsonOptions, cancellationToken);
    }

    private PortableBackupValidationResult? ValidateManifest(BackupManifest manifest)
    {
        if (manifest.FormatVersion != BackupManifest.CurrentFormatVersion)
            return PortableBackupValidationResult.Failure($"Nieobsługiwana wersja formatu kopii: {manifest.FormatVersion}. Obsługiwana wersja: {BackupManifest.CurrentFormatVersion}.", manifest);

        if (!string.Equals(manifest.Application, BackupManifest.ExpectedApplicationName, StringComparison.Ordinal))
            return PortableBackupValidationResult.Failure("Wybrany plik nie jest kopią zapasową ExpenseTrackera.", manifest);

        if (manifest.DatabaseSchemaVersion <= 0)
            return PortableBackupValidationResult.Failure("Manifest zawiera nieprawidłową wersję schematu bazy.", manifest);

        if (manifest.DatabaseSchemaVersion > _databaseMaintenanceService.CurrentSchemaVersion)
            return PortableBackupValidationResult.Failure($"Kopia używa nowszej wersji schematu {manifest.DatabaseSchemaVersion}. Obsługiwana wersja to {_databaseMaintenanceService.CurrentSchemaVersion}.", manifest);

        if (string.IsNullOrWhiteSpace(manifest.DatabaseFileName))
            return PortableBackupValidationResult.Failure("Manifest nie zawiera nazwy pliku bazy danych.", manifest);

        if (!string.Equals(manifest.DatabaseFileName, DatabaseEntryName, StringComparison.Ordinal))
            return PortableBackupValidationResult.Failure("Manifest wskazuje nieobsługiwaną nazwę pliku bazy danych.", manifest);

        if (string.IsNullOrWhiteSpace(manifest.DatabaseSha256))
            return PortableBackupValidationResult.Failure("Manifest nie zawiera sumy kontrolnej bazy danych.", manifest);

        if (!string.Equals(manifest.Encryption, "SQLCipher", StringComparison.OrdinalIgnoreCase))
            return PortableBackupValidationResult.Failure("Kopia używa nieobsługiwanego mechanizmu szyfrowania.", manifest);

        return null;
    }

    private static async Task<string> CalculateSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);

        return Convert.ToHexString(hash);
    }

    private static string GetApplicationVersion()
    {
        var version = typeof(PortableBackupService).Assembly.GetName().Version;

        return version?.ToString() ?? "unknown";
    }

    private static string CreateSuggestedFileName(DateTimeOffset createdAtUtc)
    {
        return $"ExpenseTracker-backup-{createdAtUtc:yyyy-MM-dd-HHmmss}.etbackup";
    }

    private static string CreateTemporaryArchiveName()
    {
        return $"ExpenseTracker-{Guid.NewGuid():N}.etbackup";
    }

    private string CreateWorkingDirectory()
    {
        var directory = Path.Combine(_temporaryDirectoryProvider.TemporaryDirectory, "ExpenseTrackerBackup", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Cleanup nie może przesłonić właściwego wyniku operacji.
        }
    }
}
