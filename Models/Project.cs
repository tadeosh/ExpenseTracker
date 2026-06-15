using SQLite;

namespace ExpenseTracker.Models
{
    public class Project
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // Analogicznie do Kategorii - jeśli ma wartość, jest to sub-projekt
        public int? ParentId { get; set; }
    }
}