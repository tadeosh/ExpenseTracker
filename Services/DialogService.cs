using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.Services;

public sealed class DialogService : IDialogService
{
    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel)
    {
        var page = Shell.Current?.CurrentPage;

        if (page is null)
            return false;

        return await page.DisplayAlertAsync(
            title,
            message,
            accept,
            cancel);
    }

    public async Task ShowAlertAsync(
        string title,
        string message,
        string cancel)
    {
        var page = Shell.Current?.CurrentPage;

        if (page is null)
            return;

        await page.DisplayAlertAsync(
            title,
            message,
            cancel);
    }
}
