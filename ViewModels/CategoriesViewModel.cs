using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using System.Collections.ObjectModel;
using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.ViewModels
{
    // NOWOŚĆ: IQueryAttributable pozwala łapać parametry nawigacji z poprzedniej strony
    public partial class CategoriesViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IDatabaseService _databaseService;

        public ObservableCollection<CategoryDisplayItem> Categories { get; } = new();

        public ObservableCollection<string> AvailableColors { get; } = new(new[]
        {
            "#F44336", "#E91E63", "#9C27B0", "#673AB7", "#3F51B5",
            "#2196F3", "#03A9F4", "#00BCD4", "#009688", "#4CAF50",
            "#8BC34A", "#CDDC39", "#FFEB3B", "#FFC107", "#FF9800", "#FF5722"
        });

        [ObservableProperty]
        public partial string SelectedColor { get; set; } = "#2196F3";

        [ObservableProperty]
        public partial string CategoryName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsEditing { get; set; } = false;

        [ObservableProperty]
        public partial bool IsFormVisible { get; set; } = false;

        // NOWOŚĆ: Tytuł strony (zmienia się w zależności czy to kategoria główna czy podkategoria)
        [ObservableProperty]
        public partial string PageTitle { get; set; } = AppResources.CategoriesTitle ?? "Kategorie";

        // NOWOŚĆ: Przechowuje informację o tym, do jakiej kategorii weszliśmy
        [ObservableProperty]
        public partial Category? CurrentParentCategory { get; set; }

        private Category? _categoryBeingEdited;
        private CategoryDisplayItem? _draggedItem;

        public CategoriesViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        // NOWOŚĆ: Ta metoda odpala się automatycznie, gdy wchodzimy na stronę z parametrami
        // 1. ZMIANA: Odbieramy tylko ID i ładujemy obiekt z bazy
        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("ParentCategoryId", out var idObj) && idObj is int parentId)
            {
                // Ponieważ metoda z interfejsu IQueryAttributable nie jest async Task,
                // ładujemy dane wywołując prywatną metodę asynchroniczną.
                await LoadParentCategoryAsync(parentId);
            }
        }

        private async Task LoadParentCategoryAsync(int parentId)
        {
            // Pobieramy z bazy danych na podstawie ID
            var categories = await _databaseService.GetCategoriesAsync();
            CurrentParentCategory = categories.FirstOrDefault(c => c.Id == parentId);

            if (CurrentParentCategory != null)
            {
                PageTitle = CurrentParentCategory.Name;
                await LoadCategoriesAsync(); // Odświeżamy listę podkategorii
            }
        }

        // 2. ZMIANA: Wysyłamy TYLKO numer ID zamiast całego obiektu
        [RelayCommand]
        private async Task OpenSubcategoriesAsync(CategoryDisplayItem item)
        {
            if (item == null) return;

            var navigationParameter = new Dictionary<string, object>
            {
                { "ParentCategoryId", item.Category.Id } // Przekazujemy tylko int
            };

            await Shell.Current.GoToAsync(nameof(Views.CategoriesPage), navigationParameter);
        }

        [RelayCommand]
        private void OpenAddForm()
        {
            if (IsFormVisible && !IsEditing)
            {
                IsFormVisible = false;
            }
            else
            {
                IsEditing = false;
                _categoryBeingEdited = null;
                CategoryName = string.Empty;
                SelectedColor = AvailableColors.FirstOrDefault() ?? "#2196F3";
                IsFormVisible = true;
            }
        }

        [RelayCommand]
        private async Task SaveCategoryAsync()
        {
            if (string.IsNullOrWhiteSpace(CategoryName)) return;

            if (IsEditing && _categoryBeingEdited != null)
            {
                _categoryBeingEdited.Name = CategoryName;
                _categoryBeingEdited.ColorHex = SelectedColor;
                // Przy edycji nie ruszamy ParentId
                await _databaseService.SaveCategoryAsync(_categoryBeingEdited);
            }
            else
            {
                var newCategory = new Category
                {
                    Name = CategoryName,
                    // Magia: Jeśli jesteśmy na podstronie, automatycznie przypinamy ParentId!
                    ParentId = CurrentParentCategory?.Id,
                    ColorHex = SelectedColor,
                    DisplayOrder = Categories.Count
                };
                await _databaseService.SaveCategoryAsync(newCategory);
            }

            CancelEdit();
            await LoadCategoriesAsync();
        }

        [RelayCommand]
        private void EditCategory(Category categoryToEdit)
        {
            IsFormVisible = true;
            IsEditing = true;
            _categoryBeingEdited = categoryToEdit;

            CategoryName = categoryToEdit.Name;
            SelectedColor = categoryToEdit.ColorHex;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            _categoryBeingEdited = null;
            CategoryName = string.Empty;
            SelectedColor = AvailableColors.FirstOrDefault() ?? "#2196F3";
            IsFormVisible = false;
        }

        [RelayCommand]
        private void ToggleDeleteMode(CategoryDisplayItem item)
        {
            foreach (var cat in Categories.Where(c => c != item)) cat.IsDeleteMode = false;
            item.IsDeleteMode = !item.IsDeleteMode;
        }

        [RelayCommand]
        private async Task DeleteCategoryAsync(Category categoryToDelete)
        {
            await _databaseService.DeleteCategoryAsync(categoryToDelete);
            await LoadCategoriesAsync();
        }

        // --- KOMENDY DRAG & DROP (Z wibracjami) ---
        [RelayCommand]
        private void DragStarted(CategoryDisplayItem item)
        {
            _draggedItem = item;
            item.IsBeingDragged = true;
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
        }

        [RelayCommand]
        private void DragCompleted()
        {
            if (_draggedItem != null)
            {
                _draggedItem.IsBeingDragged = false;
                _draggedItem = null;
            }
            foreach (var cat in Categories) cat.IsDragTarget = false;
        }

        [RelayCommand]
        private void DragOver(CategoryDisplayItem targetItem)
        {
            if (_draggedItem == null || _draggedItem == targetItem) return;
            foreach (var cat in Categories) cat.IsDragTarget = (cat == targetItem);
        }

        [RelayCommand]
        private void DragLeave(CategoryDisplayItem targetItem)
        {
            targetItem.IsDragTarget = false;
        }

        [RelayCommand]
        private async Task Drop(CategoryDisplayItem targetItem)
        {
            if (_draggedItem == null || _draggedItem == targetItem) return;

            int oldIndex = Categories.IndexOf(_draggedItem);
            int newIndex = Categories.IndexOf(targetItem);

            if (oldIndex < 0 || newIndex < 0) return;

            Categories.Move(oldIndex, newIndex);

            for (int i = 0; i < Categories.Count; i++)
            {
                var categoryToUpdate = Categories[i].Category;
                if (categoryToUpdate.DisplayOrder != i)
                {
                    categoryToUpdate.DisplayOrder = i;
                    await _databaseService.SaveCategoryAsync(categoryToUpdate);
                }
            }
            targetItem.IsDragTarget = false;
        }

        public async Task LoadCategoriesAsync()
        {
            var rawCategories = await _databaseService.GetCategoriesAsync();
            Categories.Clear();

            // Odfiltrowujemy liste na podstawie tego, gdzie jesteśmy
            List<Category> filteredCats;
            if (CurrentParentCategory == null)
            {
                filteredCats = rawCategories.Where(c => c.ParentId == null).OrderBy(c => c.DisplayOrder).ToList();
            }
            else
            {
                filteredCats = rawCategories.Where(c => c.ParentId == CurrentParentCategory.Id).OrderBy(c => c.DisplayOrder).ToList();
            }

            foreach (var cat in filteredCats)
            {
                // Szukamy w surowych danych wszystkich podkategorii przypiętych pod to ID
                var subCats = rawCategories.Where(c => c.ParentId == cat.Id).OrderBy(c => c.DisplayOrder).ToList();
                int count = subCats.Count;

                // Generujemy piękny tekst po przecinku: "Paliwo, Myjnia, Serwis"
                string text = count > 0 ? string.Join(", ", subCats.Select(c => c.Name)) : string.Empty;

                Categories.Add(new CategoryDisplayItem
                {
                    Category = cat,
                    SubcategoriesCount = count,
                    SubcategoriesText = text
                });
            }

            CancelEdit();
        }

       
    }

    public partial class CategoryDisplayItem : ObservableObject
    {
        public Category Category { get; set; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MinusRotation))]
        public partial bool IsDeleteMode { get; set; }

        [ObservableProperty]
        public partial bool IsBeingDragged { get; set; }

        [ObservableProperty]
        public partial bool IsDragTarget { get; set; }

        // NOWOŚĆ: Trzyma ilość podkategorii
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(HasSubcategories))]
        public partial int SubcategoriesCount { get; set; }

        // NOWOŚĆ: Trzyma nazwy podkategorii po przecinku
        [ObservableProperty]
        public partial string SubcategoriesText { get; set; } = string.Empty;

        // Magia: Zwraca Prawdę, jeśli kategoria ma dzieci (użyjemy do ukrywania napisu)
        public bool HasSubcategories => SubcategoriesCount > 0;

        // Magia: Jeśli są dzieci, dokleja " (ilość)" do nazwy!
        public string DisplayName => SubcategoriesCount > 0 ? $"{Category.Name} ({SubcategoriesCount})" : Category.Name;

        public double MinusRotation => IsDeleteMode ? 90 : 0;
        public double FontSize => 18;

        public Microsoft.Maui.Graphics.Color CategoryColor
        {
            get
            {
                if (Microsoft.Maui.Graphics.Color.TryParse(Category.ColorHex, out var color)) return color;
                return Microsoft.Maui.Graphics.Colors.Gray;
            }
        }
    }
}