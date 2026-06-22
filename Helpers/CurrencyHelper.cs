using System.Globalization;

namespace ExpenseTracker.Helpers
{
    public static class CurrencyHelper
    {
        // Słownik trzyma teraz tylko kody i flagi (nazwy pobieramy dynamicznie z systemu)
        public static readonly Dictionary<string, string> CurrencyData = new()
        {
            {"PLN", "🇵🇱"}, {"EUR", "🇪🇺"}, {"USD", "🇺🇸"}, {"GBP", "🇬🇧"}, {"CHF", "🇨🇭"},
            {"CZK", "🇨🇿"}, {"NOK", "🇳🇴"}, {"SEK", "🇸🇪"}, {"DKK", "🇩🇰"}, {"UAH", "🇺🇦"},
            {"JPY", "🇯🇵"}, {"AUD", "🇦🇺"}, {"CAD", "🇨🇦"}, {"HUF", "🇭🇺"}, {"RON", "🇷🇴"},
            {"BGN", "🇧🇬"}, {"TRY", "🇹🇷"}, {"ILS", "🇮🇱"}, {"AED", "🇦🇪"}, {"CNY", "🇨🇳"}
        };

        // Zwraca same kody (np. "PLN", "EUR", "USD") potrzebne do logiki
        public static List<string> AllCurrencyCodes => CurrencyData.Keys.ToList();

        private static string GetCurrencyNativeName(string currencyCode)
        {
            try
            {
                // Budujemy dynamicznie nazwę klucza, np. "Currency_PLN"
                string resourceKey = $"Currency_{currencyCode}";

                // Pyta menedżera zasobów AppResources o tekst dla tego klucza
                string? localizedName = Resources.Strings.AppResources.ResourceManager.GetString(resourceKey, Resources.Strings.AppResources.Culture);

                if (!string.IsNullOrEmpty(localizedName))
                {
                    return localizedName;
                }
            }
            catch
            {
                // Bezpiecznik w razie problemów z zasobami
            }

            // Jeśli nie dodałeś tłumaczenia w pliku resx (np. dla nowej waluty), 
            // aplikacja bezpiecznie pokaże po prostu "PLN"
            return currencyCode;
        }

        // Zwraca same kody np. ["PLN", "EUR"]
        public static List<string> GetFavoriteCurrencies()
        {
            string saved = Preferences.Default.Get("FavoriteCurrencies", "PLN,EUR,USD");
            return saved.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public static void SaveFavoriteCurrencies(IEnumerable<string> favorites)
        {
            Preferences.Default.Set("FavoriteCurrencies", string.Join(",", favorites));
        }

        // TWORZENIE PIĘKNEJ LISTY DO PICKERA (Z LINIA ODDZIELAJĄCĄ)
        public static List<string> GetSortedCurrencyDisplayList()
        {
            var favs = GetFavoriteCurrencies();
            var result = new List<string>();

            // 1. Ulubione
            foreach (var code in favs)
            {
                if (CurrencyData.ContainsKey(code)) result.Add(FormatDisplay(code));
            }

            // 2. Separator
            if (favs.Any())
            {
                result.Add(" ────────── ");
            }

            // 3. Reszta alfabetycznie
            var others = CurrencyData.Keys.Except(favs).OrderBy(c => c);
            foreach (var code in others)
            {
                result.Add(FormatDisplay(code));
            }

            return result;
        }


        // Formatuje wyświetlanie w zależności od systemu operacyjnego i JĘZYKA
        public static string FormatDisplay(string code)
        {
            if (!CurrencyData.ContainsKey(code)) return code;

            // POBIERAMY TŁUMACZENIE Z SYSTEMU NA ŻYWO:
            string localizedName = GetCurrencyNativeName(code);
            string flag = CurrencyData[code];

#if WINDOWS
            // Windows nie obsługuje flag, więc zwracamy np. "PLN - Polski złoty" (po angielsku: "PLN - Polish zloty")
            return $"{code} - {localizedName}";
#else
            // Android / iOS / Mac obsługują, więc: "🇵🇱 PLN - Polski złoty"
            return $"{flag} {code} - {localizedName}";
#endif
        }

        // Bezpieczne wyciąganie kodu na obu systemach
        public static string? ExtractCode(string? displayString)
        {
            if (string.IsNullOrWhiteSpace(displayString) || displayString.Contains("──"))
                return null;

            var parts = displayString.Split(' ', StringSplitOptions.RemoveEmptyEntries);

#if WINDOWS
            // "PLN - Polski Złoty" -> element 0 to "PLN"
            if (parts.Length >= 1) return parts[0];
#else
            // "🇵🇱 PLN - Polski Złoty" -> element 1 to "PLN" (bo 0 to flaga)
            if (parts.Length >= 2) return parts[1];
#endif
            return displayString;
        }
    }
}