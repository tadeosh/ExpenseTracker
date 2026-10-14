using System.Globalization;
using System.Windows.Input;

namespace ExpenseTracker.Views.Controls;

public partial class AppDatePicker : ContentView
{
    public AppDatePicker()
    {
        OpenPickerCommand = new Command(OpenPicker);

        InitializeComponent();

        SetDynamicResource(TextColorProperty, "AppTextColor");
        SetDynamicResource(IconColorProperty, "AppTextColor");
    }

    public ICommand OpenPickerCommand { get; }

    public static readonly BindableProperty DateProperty = BindableProperty.Create(nameof(Date), typeof(DateTime?), typeof(AppDatePicker), defaultValue: null, defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnDisplayPropertyChanged);

    public DateTime? Date
    {
        get => (DateTime?)GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    public static readonly BindableProperty MinimumDateProperty = BindableProperty.Create(nameof(MinimumDate), typeof(DateTime?), typeof(AppDatePicker), new DateTime(1900, 1, 1));

    public DateTime? MinimumDate
    {
        get => (DateTime?)GetValue(MinimumDateProperty);
        set => SetValue(MinimumDateProperty, value);
    }

    public static readonly BindableProperty MaximumDateProperty = BindableProperty.Create(nameof(MaximumDate), typeof(DateTime?), typeof(AppDatePicker), new DateTime(2100, 12, 31));

    public DateTime? MaximumDate
    {
        get => (DateTime?)GetValue(MaximumDateProperty);
        set => SetValue(MaximumDateProperty, value);
    }

    public static readonly BindableProperty FormatProperty = BindableProperty.Create(nameof(Format), typeof(string), typeof(AppDatePicker), "d", propertyChanged: OnDisplayPropertyChanged);

    public string Format
    {
        get => (string)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(AppDatePicker), string.Empty, propertyChanged: OnDisplayPropertyChanged);

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public new static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(AppDatePicker));

    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(AppDatePicker));

    public Color? IconColor
    {
        get => (Color?)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public string DisplayText
    {
        get
        {
            if (!Date.HasValue)
                return Placeholder;

            var culture = ExpenseTracker.Resources.Strings.AppResources.Culture ?? CultureInfo.CurrentCulture;

            return Date.Value.ToString(Format, culture);
        }
    }

    private void OpenPicker()
    {
        if (!IsEnabled)
            return;

        InnerDatePicker.IsOpen = true;
    }

    private static void OnDisplayPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (AppDatePicker)bindable;
        control.OnPropertyChanged(nameof(DisplayText));
    }
}
