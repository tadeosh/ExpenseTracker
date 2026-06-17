namespace ExpenseTracker.Helpers
{
    public static class CurrencyHelper
    {
        // Pełen słownik z flagami i nazwami (możesz dopisać dowolne inne z całego świata)
        public static readonly Dictionary<string, (string Flag, string Name)> CurrencyData = new()
        {
            {"PLN", ("🇵🇱", "Polski Złoty")},
            {"EUR", ("🇪🇺", "Euro")},
            {"USD", ("🇺🇸", "Dolar Amerykański")},
            {"GBP", ("🇬🇧", "Funt Brytyjski")},
            {"CHF", ("🇨🇭", "Frank Szwajcarski")},
            {"CZK", ("🇨🇿", "Korona Czeska")},
            {"NOK", ("🇳🇴", "Korona Norweska")},
            {"SEK", ("🇸🇪", "Korona Szwedzka")},
            {"DKK", ("🇩🇰", "Korona Duńska")},
            {"UAH", ("🇺🇦", "Hrywna Ukraińska")},
            {"JPY", ("🇯🇵", "Jen Japoński")},
            {"AUD", ("🇦🇺", "Dolar Australijski")},
            {"CAD", ("🇨🇦", "Dolar Kanadyjski")},
            {"HUF", ("🇭🇺", "Forint Węgierski")},
            {"RON", ("🇷🇴", "Lej Rumuński")},
            {"BGN", ("🇧🇬", "Lew Bułgarski")},
            {"TRY", ("🇹🇷", "Lira Turecka")},
            {"ILS", ("🇮🇱", "Nowy Szekel Izraelski")},
            {"AED", ("🇦🇪", "Dirham ZEA")},
            {"CNY", ("🇨🇳", "Yuan Chiński")}
        };

        // Zwraca same kody (np. "PLN", "EUR", "USD") potrzebne do logiki
        public static List<string> AllCurrencyCodes => CurrencyData.Keys.ToList();

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


        // Formatuje wyświetlanie w zależności od systemu operacyjnego
        public static string FormatDisplay(string code)
        {
            if (!CurrencyData.ContainsKey(code)) return code;
            var data = CurrencyData[code];

#if WINDOWS
            // Windows nie obsługuje flag, więc zwracamy np. "PLN - Polski Złoty"
            return $"{code} - {data.Name}";
#else
            // Android / iOS / Mac obsługują, więc: "🇵🇱 PLN - Polski Złoty"
            return $"{data.Flag} {code} - {data.Name}";
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