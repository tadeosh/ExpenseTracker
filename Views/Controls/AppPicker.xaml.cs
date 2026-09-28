using System.Collections;

namespace ExpenseTracker.Views.Controls;

public partial class AppPicker : ContentView
{
    public AppPicker()
    {
        InitializeComponent();

        SetDynamicResource(SelectedTextColorProperty, "AppTextColor");
    }

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(AppPicker));



    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(AppPicker), defaultBindingMode: BindingMode.TwoWay);

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(AppPicker));

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly BindableProperty ItemDisplayPathProperty = BindableProperty.Create(nameof(ItemDisplayPath), typeof(string), typeof(AppPicker), propertyChanged: OnItemDisplayPathChanged);

    public string? ItemDisplayPath
    {
        get => (string?)GetValue(ItemDisplayPathProperty);
        set => SetValue(ItemDisplayPathProperty, value);
    }

    private static void OnItemDisplayPathChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (AppPicker)bindable;
        var path = newValue as string;

        control.InnerPicker.ItemDisplayBinding = string.IsNullOrWhiteSpace(path) ? null : new Binding(path);
    }

    public static readonly BindableProperty SelectedTextColorProperty = BindableProperty.Create(nameof(SelectedTextColor), typeof(Color), typeof(AppPicker));

    public Color? SelectedTextColor
    {
        get => (Color?)GetValue(SelectedTextColorProperty);
        set => SetValue(SelectedTextColorProperty, value);
    }

    public static readonly BindableProperty SelectedFontAttributesProperty = BindableProperty.Create(nameof(SelectedFontAttributes), typeof(FontAttributes), typeof(AppPicker), FontAttributes.None);

    public FontAttributes SelectedFontAttributes
    {
        get => (FontAttributes)GetValue(SelectedFontAttributesProperty);
        set => SetValue(SelectedFontAttributesProperty, value);
    }
}
