namespace ExpenseTracker.Services.Interfaces;

public interface IDialogService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel);

    Task ShowAlertAsync(
        string title,
        string message,
        string cancel);
}
