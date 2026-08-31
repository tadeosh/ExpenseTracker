using SQLite;

namespace ExpenseTracker.Models
{
    public class Category
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // Jeśli to pole jest puste (null), to jest to główna kategoria. 
        // Jeśli ma numer, jest to podkategoria należąca do kategorii o tym ID.
        public int? ParentId { get; set; }

        // NOWOŚĆ: Pole na zapisanie koloru w formacie HEX (np. "#FF0000")
        public string ColorHex { get; set; } = "#1E88E5"; // Domyślny niebieski

        // NOWOŚĆ: Przechowuje pozycję elementu na liście
        public int DisplayOrder { get; set; }

        public bool IsArchived { get; set; } = false; // NOWOŚĆ: Pole do archiwizacji kategorii

    }
}