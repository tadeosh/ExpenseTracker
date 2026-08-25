namespace ExpenseTracker.Services.Interfaces
{
    public interface ISettingsService
    {
        string DefaultCurrency { get; set; }
        string AppLanguage { get; set; }
        int AppTheme { get; set; }

        // Metoda do czyszczenia preferencji (przyda się przy Factory Reset)
        void ClearBusinessSettings();
    }
}
