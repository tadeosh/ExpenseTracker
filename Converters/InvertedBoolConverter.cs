using System.Globalization;

namespace ExpenseTracker.Converters
{
    public class InvertedBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
                return !booleanValue;

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
                return !booleanValue;

            return false;
        }
    }
}
