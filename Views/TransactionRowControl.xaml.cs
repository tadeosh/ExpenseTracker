using System.Windows.Input;

namespace ExpenseTracker.Views;

public partial class TransactionRowControl : ContentView
{
    // Statyczne pole przechowujące referencję do otwartego elementu GOLBALNIE w całej aplikacji
    private static SwipeView? _currentlyOpenSwipeView;

    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(
        nameof(EditCommand), typeof(ICommand), typeof(TransactionRowControl));

    public static readonly BindableProperty DeleteCommandProperty = BindableProperty.Create(
        nameof(DeleteCommand), typeof(ICommand), typeof(TransactionRowControl));

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public ICommand DeleteCommand
    {
        get => (ICommand)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public TransactionRowControl()
    {
        InitializeComponent();
    }

    private void OnSwipeStarted(object? sender, SwipeStartedEventArgs e)
    {
        // Jeśli inny wiersz był już przesunięty - zamknij go natychmiast! (Gwarantuje to Clean UX)
        if (_currentlyOpenSwipeView != null && _currentlyOpenSwipeView != sender)
        {
            _currentlyOpenSwipeView.Close();
        }

        _currentlyOpenSwipeView = sender as SwipeView;
    }
}