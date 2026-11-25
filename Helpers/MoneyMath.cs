namespace ExpenseTracker.Helpers;

public static class MoneyMath
{
    public const int AmountDecimalPlaces = 2;
    public const int ExchangeRateDecimalPlaces = 8;

    public static decimal ConvertTransferAmount(decimal sourceAmount, decimal exchangeRate)
    {
        if (exchangeRate <= 0m)
            throw new ArgumentOutOfRangeException(nameof(exchangeRate), "Kurs wymiany musi być większy od zera.");

        var convertedAmount = sourceAmount / exchangeRate;

        return decimal.Round(convertedAmount, AmountDecimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static decimal RoundAmount(decimal amount)
    {
        return decimal.Round(amount, AmountDecimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static decimal RoundExchangeRate(decimal exchangeRate)
    {
        if (exchangeRate <= 0m)
            throw new ArgumentOutOfRangeException(nameof(exchangeRate), "Kurs wymiany musi być większy od zera.");

        return decimal.Round(exchangeRate, ExchangeRateDecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
