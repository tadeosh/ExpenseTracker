using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class ExchangeRatesPage : ContentPage
{
    private readonly ExchangeRatesViewModel _viewModel;

    public ExchangeRatesPage(ExchangeRatesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataAsync();
    }
}