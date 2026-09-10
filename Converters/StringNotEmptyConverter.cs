using System.Globalization;

namespace ExpenseTracker.Converters
{
    public class StringNotEmptyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue)
                return !string.IsNullOrWhiteSpace(stringValue) && stringValue != "-"; // Wykluczamy też Twój znak pustego projektu

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
