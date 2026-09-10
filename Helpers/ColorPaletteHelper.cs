namespace ExpenseTracker.Helpers
{
    public static class ColorPaletteHelper
    {
        // Predefiniowana, wysoko kontrastowa paleta barw dla wykresów
        private static readonly string[] PredefinedColors =
        {
            "#2196F3", // Niebieski
            "#F44336", // Czerwony
            "#4CAF50", // Zielony
            "#FF9800", // Pomarańczowy
            "#9C27B0", // Fioletowy
            "#00BCD4", // Turkusowy
            "#FFC107", // Bursztynowy
            "#E91E63", // Różowy
            "#3F51B5", // Indygo
            "#8BC34A", // Jasnozielony
            "#795548", // Brązowy
            "#607D8B"  // Niebieskoszary
        };

        // Zwraca kolor na podstawie indeksu, zapętlając się, jeśli projektów jest więcej niż kolorów
        public static string GetColor(int index)
        {
            if (index < 0) return "#9E9E9E"; // Szary dla przypadków brzegowych
            return PredefinedColors[index % PredefinedColors.Length];
        }
    }
}
