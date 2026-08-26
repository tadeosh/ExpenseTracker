using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class AddTransactionPage : ContentPage
{
    private readonly AddTransactionViewModel _viewModel;

    public AddTransactionPage(AddTransactionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    //protected override async void OnAppearing()
    //{
    //    base.OnAppearing();
    //    await _viewModel.LoadDataAsync();
    //    if (string.IsNullOrEmpty(AmountEntry.Text))
    //    {
    //        await Task.Delay(100);
    //        AmountEntry.Focus();
    //    }
    //}

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // Odpalamy ładowanie bezpiecznie na wątku UI, po zakończeniu przejścia
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _viewModel.LoadDataAsync();
        });
    }

    // NOWOŚĆ: Przechwytuje sprzętowy przycisk "Wstecz" na Androidzie
    protected override bool OnBackButtonPressed()
    {
        // Zamiast minimalizować aplikację, wracamy na stronę główną
        Shell.Current.GoToAsync("//HomePage");

        // Zwrócenie 'true' oznacza: "Systemie, już się tym zająłem, nic więcej nie rób!"
        return true;
    }
}