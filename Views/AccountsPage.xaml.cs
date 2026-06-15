using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class AccountsPage : ContentPage
{
    // Tworzymy prywatną zmienną, żeby pamiętać nasz ViewModel
    private readonly AccountsViewModel _viewModel;

    public AccountsPage(AccountsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    // Ta metoda uruchamia się automatycznie za każdym razem, gdy użytkownik wchodzi na tę stronę
    protected override async void OnAppearing()
    {
        // Najpierw pozwalamy systemowi narysować standardowe elementy strony
        base.OnAppearing();

        // Dopiero teraz bezpiecznie, w pełni asynchronicznie, pobieramy dane z bazy!
        await _viewModel.LoadAccountsAsync();
    }
}