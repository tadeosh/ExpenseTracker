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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataAsync();
        if (string.IsNullOrEmpty(AmountEntry.Text))
        {
            await Task.Delay(100);
            AmountEntry.Focus();
        }
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