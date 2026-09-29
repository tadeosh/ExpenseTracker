using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Widget;
using ExpenseTracker.Resources.Strings;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using System.Globalization;

namespace ExpenseTracker.Platforms.Android.Handlers;

public class AppDatePickerHandler : DatePickerHandler
{
    private DatePickerDialog? _currentDialog;

    protected override DatePickerDialog CreateDatePickerDialog(int year, int month, int day)
    {
        System.Diagnostics.Debug.WriteLine("[AppDatePickerHandler] CreateDatePickerDialog");

        var activity = Platform.CurrentActivity;

        if (activity is null || activity.IsFinishing || activity.IsDestroyed)
            return base.CreateDatePickerDialog(year, month, day);

        ApplyApplicationLocale(activity);

        var dialog = new DatePickerDialog(activity, OnDateSelected, year, month, day);
        _currentDialog = dialog;

        dialog.ShowEvent += OnDialogShown;

        dialog.DatePicker.DateChanged += (_, args) =>
        {
            UpdateLocalizedHeader(
                dialog,
                args.Year,
                args.MonthOfYear,
                args.DayOfMonth);
        };

        return dialog;
    }

    private void OnDateSelected(object? sender, DatePickerDialog.DateSetEventArgs args)
    {
        if (VirtualView is not null)
            VirtualView.Date = args.Date;
    }

    private void OnDialogShown(object? sender, EventArgs args)
    {
        System.Diagnostics.Debug.WriteLine("[AppDatePickerHandler] OnDialogShown");

        if (sender is not DatePickerDialog dialog)
            return;

        var positiveButton = dialog.GetButton((int)DialogButtonType.Positive);
        var negativeButton = dialog.GetButton((int)DialogButtonType.Negative);
        var buttonColor = GetDialogButtonColor();

        System.Diagnostics.Debug.WriteLine(
            $"[AppDatePickerHandler] Positive: {positiveButton is not null}, Negative: {negativeButton is not null}");

        if (positiveButton is not null)
        {
            positiveButton.Text = AppResources.OkBtn;
            positiveButton.SetTextColor(buttonColor);
        }

        if (negativeButton is not null)
        {
            negativeButton.Text = AppResources.CancelBtn;
            negativeButton.SetTextColor(buttonColor);
        }

        UpdateLocalizedHeader(
            dialog,
            dialog.DatePicker.Year,
            dialog.DatePicker.Month,
            dialog.DatePicker.DayOfMonth);
    }

    private static void UpdateLocalizedHeader(DatePickerDialog dialog, int year, int month, int day)
    {
        var culture = AppResources.Culture ?? CultureInfo.CurrentUICulture;
        var selectedDate = new DateTime(year, month + 1, day);

        var headerId = dialog.Context?.Resources?.GetIdentifier("date_picker_header_date", "id", "android") ?? 0;

        if (headerId == 0)
        {
            System.Diagnostics.Debug.WriteLine("[AppDatePickerHandler] Nie znaleziono date_picker_header_date.");
            return;
        }

        var headerText = dialog.FindViewById<TextView>(headerId);

        if (headerText is null)
        {
            System.Diagnostics.Debug.WriteLine("[AppDatePickerHandler] Nie znaleziono TextView nagłówka.");
            return;
        }

        var localizedText = FormatHeaderDate(selectedDate, culture);

        headerText.Post(() =>
        {
            headerText.Text = localizedText;
            System.Diagnostics.Debug.WriteLine($"[AppDatePickerHandler] Header applied: {headerText.Text}");
        });
    }

    private static string FormatHeaderDate(DateTime date, CultureInfo culture)
    {
        var pattern = culture.TwoLetterISOLanguageName switch
        {
            "en" => "ddd, MMM d",
            "de" => "ddd, d. MMM",
            "pl" => "ddd, d MMM",
            _ => "ddd, d MMM"
        };

        return date.ToString(pattern, culture);
    }

    private static void ApplyApplicationLocale(Activity activity)
    {
        var culture = AppResources.Culture ?? CultureInfo.CurrentUICulture;
        var languageTag = culture.Name.Replace('_', '-');
        var locale = Java.Util.Locale.ForLanguageTag(languageTag);

        Java.Util.Locale.SetDefault(Java.Util.Locale.Category.Format, locale);
        Java.Util.Locale.SetDefault(Java.Util.Locale.Category.Display, locale);

        var resources = activity.Resources;

        if (resources is null)
            return;

        var configuration = new Configuration(resources.Configuration);

        configuration.SetLocale(locale);
        configuration.SetLayoutDirection(locale);

#pragma warning disable CA1422
        resources.UpdateConfiguration(configuration, resources.DisplayMetrics);
#pragma warning restore CA1422

        System.Diagnostics.Debug.WriteLine(
            $"[AppDatePickerHandler] Android locale: {languageTag}");
    }

    private static global::Android.Graphics.Color GetDialogButtonColor()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(
                "PrimaryColor",
                out var resource) == true &&
            resource is Microsoft.Maui.Graphics.Color color)
        {
            return color.ToPlatform();
        }

        return global::Android.Graphics.Color.Rgb(204, 0, 153);
    }
}
