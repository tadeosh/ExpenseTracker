namespace ExpenseTracker.Views.Controls;

public partial class AppCurrencyPicker : ContentView
{
    public AppCurrencyPicker()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(AppCurrencyPicker), defaultBindingMode: BindingMode.TwoWay);

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(AppCurrencyPicker));

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
