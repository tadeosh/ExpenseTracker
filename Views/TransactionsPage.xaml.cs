namespace ExpenseTracker.Views;

public partial class TransactionsPage : ContentPage
{
    // Wstrzykujemy nasz ViewModel przez konstruktor
    public TransactionsPage(ViewModels.TransactionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // NOWOŚĆ: Ta metoda uruchamia się za każdym razem, gdy strona pojawia się na ekranie
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Upewniamy się, że strona ma podpięty ViewModel
        if (BindingContext is ViewModels.TransactionsViewModel vm)
        {
            // Wymuszamy ponowne pobranie danych z bazy i nałożenie filtrów
            await vm.LoadDataAsync();
        }
    }

    private SwipeView? _currentlyOpenSwipeView;

    private void OnSwipeStarted(object? sender, SwipeStartedEventArgs e)
    {
        // Jeśli otwieramy nowy wiersz, a stary jest otwarty - zamknij stary
        if (_currentlyOpenSwipeView != null && _currentlyOpenSwipeView != sender)
        {
            _currentlyOpenSwipeView.Close();
        }

        // Zapisz referencję do nowo otwieranego wiersza
        _currentlyOpenSwipeView = sender as SwipeView;
    }
}