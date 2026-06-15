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
    }
}