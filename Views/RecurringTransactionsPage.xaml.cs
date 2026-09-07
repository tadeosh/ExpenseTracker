using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class RecurringTransactionsPage : ContentPage
{
    // Wstrzykujemy ViewModel przez konstruktor
    public RecurringTransactionsPage(RecurringTransactionsViewModel viewModel)
    {
        InitializeComponent();

        // KRYTYCZNE: Bez tego strona będzie pusta, bo XAML nie ma skąd czerpać danych
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // Rzutowanie jest bezpieczne, bo wyżej zdefiniowaliśmy BindingContext
        if (BindingContext is RecurringTransactionsViewModel vm)
        {
            await vm.LoadDataAsync();
        }
    }
}