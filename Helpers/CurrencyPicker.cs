using ExpenseTracker.Helpers;

namespace ExpenseTracker.Controls
{
    // Dziedziczymy po standardowym Pickerze, dzięki czemu zachowujemy jego wygląd i zachowanie
    public class CurrencyPicker : Picker
    {
        private bool _isInitializing = true;
        private string? _previousValidCode;

        public CurrencyPicker()
        {
            // 1. Automatycznie ładujemy naszą profesjonalną listę walut (Ulubione + Separator + Reszta)
            foreach (var currencyDisplay in CurrencyHelper.GetSortedCurrencyDisplayList())
            {
                Items.Add(currencyDisplay);
            }

            // 2. Podpinamy się pod zdarzenie zmiany wyboru
            SelectedIndexChanged += OnCurrencySelectedIndexChanged;

            _isInitializing = false;
        }

        private void OnCurrencySelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isInitializing || SelectedIndex == -1) return;

            string? selectedDisplay = SelectedItem?.ToString();

            // 3. AUTOMATYCZNA BLOKADA SEPARATORA "──────"
            if (selectedDisplay != null && selectedDisplay.Contains("──"))
            {
                // Jeśli użytkownik wybrał kreskę, szukamy na liście tekstu dla poprzednio wybranego (prawidłowego) kodu
                if (!string.IsNullOrEmpty(_previousValidCode))
                {
                    string displayToRestore = CurrencyHelper.FormatDisplay(_previousValidCode);
                    SelectedIndex = Items.IndexOf(displayToRestore);
                }
                else
                {
                    SelectedIndex = -1; // Jeśli nie było nic wcześniej, po prostu resetujemy wybór
                }
                return;
            }

            // 4. Zapamiętujemy ostatni poprawny wybór na wypadek, gdyby użytkownik następnym razem kliknął kreskę
            _previousValidCode = SelectedCurrencyCode;
        }

        // POTĘŻNA WŁAŚCIWOŚĆ: Zwraca surowy 3-literowy kod (np. "PLN") niezależnie od tego, co wyświetla się na ekranie!
        public string? SelectedCurrencyCode
        {
            get => CurrencyHelper.ExtractCode(SelectedItem?.ToString());
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    SelectedIndex = -1;
                    return;
                }

                // Pozwala ustawić walutę z poziomu kodu (np. SelectedCurrencyCode = "EUR")
                string displayToFind = CurrencyHelper.FormatDisplay(value);
                int index = Items.IndexOf(displayToFind);
                if (index >= 0)
                {
                    SelectedIndex = index;
                }
            }
        }
    }
}