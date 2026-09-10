using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class ReportsPage : ContentPage
{
    public ReportsPage(ReportsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (BindingContext is ReportsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
