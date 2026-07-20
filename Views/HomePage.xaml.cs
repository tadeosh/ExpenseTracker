using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Pobiera na żywo najświeższe dane za każdym razem, gdy użytkownik wraca na stronę główną
        await _viewModel.LoadDataAsync();
    }

    
}