using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class FavoriteCurrenciesPage : ContentPage
{
    private readonly FavoriteCurrenciesViewModel _viewModel;

    public FavoriteCurrenciesPage(FavoriteCurrenciesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadData();
    }

    // Nowość: Automatyczny zapis w momencie zamykania ekranu (strzałka "Wstecz")
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.SaveFavorites();
    }
}