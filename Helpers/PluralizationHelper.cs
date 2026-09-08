using System.Globalization;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings; // Upewnij się, że to Twoja ścieżka do AppResources

namespace ExpenseTracker.Helpers
{
    public static class PluralizationHelper
    {
        public static string GetUnitDisplayName(RecurrenceUnit unit, int count)
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
            string suffix = GetPluralSuffix(count, culture);

            string baseKey = unit switch
            {
                RecurrenceUnit.Days => "UnitDays",
                RecurrenceUnit.Weeks => "UnitWeeks",
                RecurrenceUnit.Months => "UnitMonths",
                RecurrenceUnit.Years => "UnitYears",
                _ => "UnitMonths"
            };

            // Pobieramy z ResourceManager (wygenerowany przez plik .resx)
            string? localizedString = AppResources.ResourceManager.GetString($"{baseKey}{suffix}");

            // Fallback: jeśli język (np. EN) nie ma formy _5, bierzemy _2 (l. mnoga)
            if (string.IsNullOrEmpty(localizedString) && suffix == "_5")
            {
                localizedString = AppResources.ResourceManager.GetString($"{baseKey}_2");
            }

            // Ostateczny fallback bezpieczeństwa
            return localizedString ?? baseKey;
        }

        private static string GetPluralSuffix(int count, string lang)
        {
            // 1. Zawsze liczba pojedyncza dla 1
            if (count == 1) return "_1";

            // 2. Reguły dla języka polskiego
            if (lang == "pl")
            {
                int mod10 = count % 10;
                int mod100 = count % 100;

                // Przypadki dla 2, 3, 4, 22, 23, 24, 32... ale NIE 12, 13, 14
                if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
                {
                    return "_2";
                }

                // Cała reszta to dopełniacz (5-11, 12-14, 25-31 itd.)
                return "_5";
            }

            // 3. Reguła ogólna (angielski, niemiecki itp.) - po prostu liczba mnoga
            return "_2";
        }
    }
}
