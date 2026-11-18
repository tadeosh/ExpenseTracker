using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Helpers;
using ExpenseTracker.Models;
using ExpenseTracker.Models.Backup;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ExpenseTracker.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const int MinimumBackupPasswordLength = 10;

    private readonly IDatabaseService _databaseService;
    private readonly ISettingsService _settingsService;
    private readonly IPortableBackupService _portableBackupService;
    private readonly IBackupFileService _backupFileService;
    private readonly ILogger<SettingsViewModel> _logger;

    private readonly List<CultureInfo> _supportedCultures =
    [
        new CultureInfo("en"),
        new CultureInfo("pl"),
        new CultureInfo("de")
    ];

    private bool _isInitializing = true;

    public ObservableCollection<string> Languages { get; } = [];
    public ObservableCollection<string> Themes { get; } = [];

    [ObservableProperty]
    public partial string? SelectedCurrency { get; set; }

    [ObservableProperty]
    public partial string? SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial string? SelectedTheme { get; set; }

    [ObservableProperty]
    public partial string BackupPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BackupPasswordConfirmation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RestorePassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBackupOperationRunning { get; set; }

    [ObservableProperty]
    public partial string BackupStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BackupErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RestoreStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RestoreErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasValidatedBackup { get; set; }

    [ObservableProperty]
    public partial string ValidatedBackupFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ValidatedBackupCreatedAt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ValidatedBackupAppVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ValidatedBackupSchemaVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial UiMessageType BackupMessageType { get; set; }
    public bool IsBackupSuccess => BackupMessageType == UiMessageType.Success;

    public bool IsBackupWarning => BackupMessageType == UiMessageType.Warning;

    public bool IsBackupError => BackupMessageType == UiMessageType.Error;

    public bool IsBackupInteractionEnabled => !IsBackupOperationRunning;
    private BackupFilePickResult? _validatedBackup;
    private string? _validatedBackupPassword;

    [ObservableProperty]
    public partial UiMessageType RestoreMessageType { get; set; }

    public bool IsRestoreSuccess => RestoreMessageType == UiMessageType.Success;

    public bool IsRestoreWarning => RestoreMessageType == UiMessageType.Warning;

    public bool IsRestoreError => RestoreMessageType == UiMessageType.Error;

    partial void OnRestoreMessageTypeChanged(UiMessageType value)
    {
        OnPropertyChanged(nameof(IsRestoreSuccess));
        OnPropertyChanged(nameof(IsRestoreWarning));
        OnPropertyChanged(nameof(IsRestoreError));
    }

    public SettingsViewModel(
        IDatabaseService databaseService,
        ISettingsService settingsService,
        IPortableBackupService portableBackupService,
        IBackupFileService backupFileService,
        ILogger<SettingsViewModel> logger)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _portableBackupService = portableBackupService ?? throw new ArgumentNullException(nameof(portableBackupService));
        _backupFileService = backupFileService ?? throw new ArgumentNullException(nameof(backupFileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeData();
    }

    private void InitializeData()
    {
        _isInitializing = true;

        var savedCurrencyCode = _settingsService.DefaultCurrency;
        SelectedCurrency = CurrencyHelper.FormatDisplay(savedCurrencyCode);

        foreach (var culture in _supportedCultures)
            Languages.Add(culture.NativeName);

        var currentCulture = AppResources.Culture ?? CultureInfo.CurrentUICulture;
        var cultureMatch = _supportedCultures.FirstOrDefault(culture => culture.TwoLetterISOLanguageName == currentCulture.TwoLetterISOLanguageName);

        SelectedLanguage = cultureMatch is not null ? cultureMatch.NativeName : Languages.First();

        Themes.Add(AppResources.ThemeLight);
        Themes.Add(AppResources.ThemeDark);
        Themes.Add(AppResources.ThemeHighContrast);

        var themeIndex = _settingsService.AppTheme;
        SelectedTheme = themeIndex >= 0 && themeIndex < Themes.Count ? Themes[themeIndex] : Themes[0];

        _isInitializing = false;
    }

    partial void OnSelectedCurrencyChanged(string? value)
    {
        if (_isInitializing || string.IsNullOrEmpty(value) || value.Contains("──", StringComparison.Ordinal))
            return;

        var cleanCode = CurrencyHelper.ExtractCode(value);

        if (!string.IsNullOrEmpty(cleanCode))
            _settingsService.DefaultCurrency = cleanCode;
    }

    partial void OnSelectedLanguageChanged(string? value)
    {
        if (_isInitializing || string.IsNullOrEmpty(value))
            return;

        var selectedCulture = _supportedCultures.FirstOrDefault(culture => culture.NativeName == value);

        if (selectedCulture is null)
            return;

        _settingsService.AppLanguage = selectedCulture.TwoLetterISOLanguageName;
        SetLanguage(selectedCulture);
    }

    partial void OnSelectedThemeChanged(string? value)
    {
        if (_isInitializing || string.IsNullOrEmpty(value))
            return;

        var index = Themes.IndexOf(value);

        if (index == -1)
            return;

        _settingsService.AppTheme = index;
        App.ApplyTheme(index);
    }

    partial void OnIsBackupOperationRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBackupInteractionEnabled));
    }

    private void SetLanguage(CultureInfo culture)
    {
        if (AppResources.Culture?.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName)
            return;

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        AppResources.Culture = culture;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current?.Windows.Count > 0)
                Application.Current.Windows[0].Page = new AppShell();
        });
    }

    partial void OnBackupMessageTypeChanged(UiMessageType value)
    {
        OnPropertyChanged(nameof(IsBackupSuccess));
        OnPropertyChanged(nameof(IsBackupWarning));
        OnPropertyChanged(nameof(IsBackupError));
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        ClearBackupMessages();

        if (!ValidateBackupPassword())
            return;

        IsBackupOperationRunning = true;

        PortableBackupCreationResult? backup = null;

        try
        {
            backup = await _portableBackupService.CreateBackupAsync(BackupPassword);

            var saveResult = await _backupFileService.SaveBackupAsync(backup);

            if (!saveResult.IsSuccessful)
            {
                BackupMessageType = UiMessageType.Warning;
                BackupStatusMessage = AppResources.Backup_SaveNotCompleted;
                return;
            }

            BackupMessageType = UiMessageType.Success;
            BackupStatusMessage = string.IsNullOrWhiteSpace(saveResult.FilePath)
                ? AppResources.Backup_SaveSuccess
                : string.Format(CultureInfo.CurrentCulture, AppResources.Backup_SaveSuccessWithPath, saveResult.FilePath);

            BackupPassword = string.Empty;
            BackupPasswordConfirmation = string.Empty;
        }
        catch (OperationCanceledException)
        {
            BackupMessageType = UiMessageType.Warning;
            BackupStatusMessage = AppResources.Backup_SaveNotCompleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie udało się utworzyć i zapisać kopii zapasowej.");
            BackupMessageType = UiMessageType.Error;
            BackupErrorMessage = AppResources.Backup_CreateFailed;
        }
        finally
        {
            if (backup is not null)
                _portableBackupService.DeleteTemporaryBackup(backup);

            IsBackupOperationRunning = false;
        }
    }

    [RelayCommand]
    private async Task PickAndValidateBackupAsync()
    {
        ClearRestoreMessages();
        ClearValidatedBackup();

        if (string.IsNullOrWhiteSpace(RestorePassword))
        {
            RestoreMessageType = UiMessageType.Error;
            RestoreErrorMessage = AppResources.Backup_PasswordRequired;
            return;
        }

        IsBackupOperationRunning = true;

        BackupFilePickResult? pickedBackup = null;

        try
        {
            pickedBackup = await _backupFileService.PickBackupAsync();

            if (pickedBackup.IsCancelled)
            {
                RestoreMessageType = UiMessageType.Warning;
                RestoreStatusMessage = AppResources.Backup_PickCancelled;
                return;
            }

            if (!pickedBackup.IsSuccessful || string.IsNullOrWhiteSpace(pickedBackup.TemporaryFilePath))
            {
                RestoreMessageType = UiMessageType.Error;
                RestoreErrorMessage = AppResources.Backup_PickFailed;
                return;
            }

            var validationResult = await _portableBackupService.ValidateBackupAsync(pickedBackup.TemporaryFilePath, RestorePassword);

            if (!validationResult.IsValid || validationResult.Manifest is null)
            {
                RestoreMessageType = UiMessageType.Error;
                RestoreErrorMessage = string.IsNullOrWhiteSpace(validationResult.ErrorMessage)
                    ? AppResources.Backup_ValidationFailed
                    : validationResult.ErrorMessage;

                return;
            }

            _validatedBackup = pickedBackup;
            _validatedBackupPassword = RestorePassword;

            ApplyValidatedBackupMetadata(pickedBackup, validationResult.Manifest);

            RestoreMessageType = UiMessageType.Success;
            RestoreStatusMessage = AppResources.Backup_ValidationSuccess;

            pickedBackup = null;
            RestorePassword = string.Empty;
        }
        catch (OperationCanceledException)
        {
            RestoreMessageType = UiMessageType.Warning;
            RestoreStatusMessage = AppResources.Backup_PickCancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie udało się wybrać lub zweryfikować kopii zapasowej.");

            RestoreMessageType = UiMessageType.Error;
            RestoreErrorMessage = AppResources.Backup_ValidationFailed;
        }
        finally
        {
            if (pickedBackup is not null)
                _backupFileService.DeletePickedTemporaryFile(pickedBackup);

            IsBackupOperationRunning = false;
        }
    }

    [RelayCommand]
    private async Task RestoreValidatedBackupAsync()
    {
        ClearRestoreMessages();

        if (_validatedBackup is null ||
            string.IsNullOrWhiteSpace(_validatedBackup.TemporaryFilePath) ||
            !File.Exists(_validatedBackup.TemporaryFilePath) ||
            string.IsNullOrWhiteSpace(_validatedBackupPassword))
        {
            ClearValidatedBackup();

            RestoreMessageType = UiMessageType.Error;
            RestoreErrorMessage = AppResources.Backup_NoValidatedBackup;
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.Backup_RestoreConfirmationTitle,
            AppResources.Backup_RestoreConfirmationMessage,
            AppResources.Backup_RestoreConfirmButton,
            AppResources.CancelBtn);

        if (!confirmed)
        {
            RestoreMessageType = UiMessageType.Warning;
            RestoreStatusMessage = AppResources.Backup_RestoreCancelled;
            return;
        }

        IsBackupOperationRunning = true;

        try
        {
            var result = await _portableBackupService.RestoreBackupAsync(
                _validatedBackup.TemporaryFilePath,
                _validatedBackupPassword);

            if (!result.IsSuccessful)
                throw new InvalidOperationException("Restore nie zwrócił wyniku sukcesu.");

            RestoreMessageType = UiMessageType.Success;
            RestoreStatusMessage = AppResources.Backup_RestoreSuccess;

            await Shell.Current.DisplayAlertAsync(
                AppResources.SuccessTitle,
                AppResources.Backup_RestoreSuccess,
                AppResources.OkBtn);

            ClearValidatedBackup();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Application.Current?.Windows.Count > 0)
                    Application.Current.Windows[0].Page = new AppShell();
            });
        }
        catch (OperationCanceledException)
        {
            RestoreMessageType = UiMessageType.Warning;
            RestoreStatusMessage = AppResources.Backup_RestoreCancelled;

            ClearValidatedBackup();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie udało się przywrócić kopii zapasowej.");

            RestoreMessageType = UiMessageType.Error;
            RestoreErrorMessage = ex.Message.Contains(
                "Poprzednia baza została automatycznie przywrócona",
                StringComparison.OrdinalIgnoreCase)
                ? AppResources.Backup_RestoreFailedRollbackSucceeded
                : AppResources.Backup_RestoreFailed;

            ClearValidatedBackup();
        }
        finally
        {
            IsBackupOperationRunning = false;
        }
    }

    [RelayCommand]
    private void CancelValidatedBackup()
    {
        ClearValidatedBackup();
        ClearRestoreMessages();

        RestoreMessageType = UiMessageType.Warning;
        RestoreStatusMessage = AppResources.Backup_ValidatedBackupCleared;
    }

    private bool ValidateBackupPassword()
    {
        if (string.IsNullOrWhiteSpace(BackupPassword))
        {
            BackupMessageType = UiMessageType.Error;
            BackupErrorMessage = AppResources.Backup_PasswordRequired;
            return false;
        }

        if (BackupPassword.Length < MinimumBackupPasswordLength)
        {
            BackupMessageType = UiMessageType.Error;
            BackupErrorMessage = string.Format(CultureInfo.CurrentCulture, AppResources.Backup_PasswordMinimumLength, MinimumBackupPasswordLength);
            return false;
        }

        if (!string.Equals(BackupPassword, BackupPasswordConfirmation, StringComparison.Ordinal))
        {
            BackupMessageType = UiMessageType.Error;
            BackupErrorMessage = AppResources.Backup_PasswordsDoNotMatch;
            return false;
        }

        return true;
    }

    private void ApplyValidatedBackupMetadata(BackupFilePickResult pickedBackup, BackupManifest manifest)
    {
        HasValidatedBackup = true;
        ValidatedBackupFileName = pickedBackup.OriginalFileName ?? string.Empty;
        ValidatedBackupCreatedAt = manifest.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        ValidatedBackupAppVersion = manifest.AppVersion;
        ValidatedBackupSchemaVersion = manifest.DatabaseSchemaVersion.ToString(CultureInfo.CurrentCulture);
    }

    private void ClearValidatedBackup()
    {
        if (_validatedBackup is not null)
            _backupFileService.DeletePickedTemporaryFile(_validatedBackup);

        _validatedBackup = null;
        _validatedBackupPassword = null;

        HasValidatedBackup = false;
        ValidatedBackupFileName = string.Empty;
        ValidatedBackupCreatedAt = string.Empty;
        ValidatedBackupAppVersion = string.Empty;
        ValidatedBackupSchemaVersion = string.Empty;
    }

    private void ClearBackupMessages()
    {
        BackupStatusMessage = string.Empty;
        BackupErrorMessage = string.Empty;
        BackupMessageType = UiMessageType.None;
    }

    private void ClearRestoreMessages()
    {
        RestoreStatusMessage = string.Empty;
        RestoreErrorMessage = string.Empty;
        RestoreMessageType = UiMessageType.None;
    }

    [RelayCommand]
    private async Task ManageCategoriesAsync()
    {
        await Shell.Current.GoToAsync("CategoriesPage");
    }

    [RelayCommand]
    private async Task ManageProjectsAsync()
    {
        await Shell.Current.GoToAsync("ProjectsPage");
    }

    [RelayCommand]
    private async Task ManageExchangeRatesAsync()
    {
        await Shell.Current.GoToAsync("ExchangeRatesPage");
    }

    [RelayCommand]
    private async Task ManageFavoriteCurrenciesAsync()
    {
        await Shell.Current.GoToAsync("FavoriteCurrenciesPage");
    }

    [RelayCommand]
    private async Task WipeDataAsync()
    {
        var firstWarning = await Shell.Current.DisplayAlertAsync(AppResources.WarningTitle, AppResources.WipeDataWarningMessage, AppResources.YesBtn, AppResources.CancelBtn);

        if (!firstWarning)
            return;

        var finalWarning = await Shell.Current.DisplayAlertAsync(AppResources.WipeDataFinalWarningTitle, AppResources.WipeDataFinalWarningMessage, AppResources.DestroyDataBtn, AppResources.CancelBtn);

        if (!finalWarning)
            return;

        try
        {
            await _databaseService.WipeAllDataAsync();
            await Shell.Current.DisplayAlertAsync(AppResources.SuccessTitle, AppResources.WipeDataSuccessMessage, AppResources.OkBtn);
            await Shell.Current.GoToAsync("//HomePage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie udało się usunąć wszystkich danych aplikacji.");

            await Shell.Current.DisplayAlertAsync(AppResources.ErrorTitle, AppResources.WipeDataFailedMessage, AppResources.OkBtn);
        }
    }
}
