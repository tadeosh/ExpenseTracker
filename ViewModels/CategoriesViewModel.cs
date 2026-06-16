using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    public partial class CategoriesViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        public ObservableCollection<Category> Categories { get; } = new();

        [ObservableProperty]
        public partial string CategoryName { get; set; } = string.Empty;

        public CategoriesViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        private async Task AddCategoryAsync()
        {
            if (string.IsNullOrWhiteSpace(CategoryName)) return;

            var newCategory = new Category { Name = CategoryName };
            await _databaseService.SaveCategoryAsync(newCategory);
            Categories.Add(newCategory);

            CategoryName = string.Empty;
        }

        public async Task LoadCategoriesAsync()
        {
            var items = await _databaseService.GetCategoriesAsync();
            Categories.Clear();
            foreach (var item in items) Categories.Add(item);
        }
    }
}