using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using FluentValidation;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace ExpenseTracker.ViewModels
{
    // NOWOŚĆ: IQueryAttributable pozwala łapać parametry nawigacji z poprzedniej strony
    public partial class CategoriesViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IDatabaseService _databaseService;
        private readonly IValidator<CategoriesViewModel> _validator;
        private readonly ISettingsService _settingsService;

        public ObservableCollection<CategoryDisplayItem> Categories { get; } = new();

        public ObservableCollection<string> RecentColors { get; } = new();

        [ObservableProperty]
        public partial string SelectedColor { get; set; } = "#2196F3";

        // NOWOŚĆ: Właściwości dla suwaków (wartości 0-255)
        [ObservableProperty] public partial double ColorRed { get; set; }
        [ObservableProperty] public partial double ColorGreen { get; set; }
        [ObservableProperty] public partial double ColorBlue { get; set; }

        private bool _isUpdatingColor = false;

        // Metody wywoływane automatycznie przez CommunityToolkit przy przesunięciu suwaka
        partial void OnColorRedChanged(double value) => UpdateHexFromRgb();
        partial void OnColorGreenChanged(double value) => UpdateHexFromRgb();
        partial void OnColorBlueChanged(double value) => UpdateHexFromRgb();

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

        [ObservableProperty]
        public partial string? CategoryNameError { get; set; } // NOWOŚĆ: Pole błędu

        // ZMIANA: Zaktualizowany konstruktor
        public CategoriesViewModel(IDatabaseService databaseService, IValidator<CategoriesViewModel> validator, ISettingsService settingsService)
        {
            _databaseService = databaseService;
            _validator = validator;
            _settingsService = settingsService;
        }

        private void ClearErrors()
        {
            CategoryNameError = null;
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        //=============== Color Picker =====================
        private void UpdateHexFromRgb()
        {
            if (_isUpdatingColor) return;
            _isUpdatingColor = true;

            // Generuje nowy kod HEX na podstawie pozycji 3 suwaków
            SelectedColor = Microsoft.Maui.Graphics.Color.FromRgb((int)ColorRed, (int)ColorGreen, (int)ColorBlue).ToHex();

            _isUpdatingColor = false;
        }

        // Metoda wywoływana, gdy z kodu przypiszemy SelectedColor (np. przy edycji lub wpisaniu HEX ręcznie)
        partial void OnSelectedColorChanged(string value)
        {
            if (_isUpdatingColor || string.IsNullOrWhiteSpace(value)) return;

            if (Microsoft.Maui.Graphics.Color.TryParse(value, out var color))
            {
                _isUpdatingColor = true;
                // color.Red zwraca wartość 0.0 - 1.0, więc mnożymy przez 255 dla suwaka
                ColorRed = color.Red * 255;
                ColorGreen = color.Green * 255;
                ColorBlue = color.Blue * 255;
                _isUpdatingColor = false;
            }
        }

        // 1. Ładowanie ostatnich kolorów przy starcie (np. wywoływane w konstruktorze lub LoadCategoriesAsync)
        private void LoadRecentColors()
        {
            var savedColorsJson = _settingsService.RecentCategoryColors ?? "[]"; // Wymaga dodania pola w ISettingsService

            // Fallback (awaryjna lista, jeśli nic nie ma)
            if (savedColorsJson == "[]")
            {
                var defaultColors = new[] { "#2196F3", "#4CAF50", "#F44336", "#FF9800", "#9C27B0" };
                foreach (var c in defaultColors) RecentColors.Add(c);
                return;
            }

            try
            {
                var colors = JsonSerializer.Deserialize<List<string>>(savedColorsJson);
                if (colors != null)
                {
                    RecentColors.Clear();
                    foreach (var c in colors) RecentColors.Add(c);
                }
            }
            catch { /* Ignorujemy błędy parsowania */ }
        }

        // 2. Logika zapisywania nowego koloru do palety
        private void SaveColorToRecents(string hexColor)
        {
            if (RecentColors.Contains(hexColor))
            {
                // Przesuwamy na początek listy (MRU - Most Recently Used)
                RecentColors.Remove(hexColor);
            }

            RecentColors.Insert(0, hexColor);

            // Ograniczamy paletę do max 10 ostatnich kolorów
            if (RecentColors.Count > 10)
            {
                RecentColors.RemoveAt(10);
            }

            _settingsService.RecentCategoryColors = JsonSerializer.Serialize(RecentColors.ToList());
        }

        //=============== END Color Picker =====================

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
                ClearErrors();

                IsEditing = false;
                _categoryBeingEdited = null;
                CategoryName = string.Empty;
                //SelectedColor = "#HEX"; //AvailableColors.FirstOrDefault() ?? "#2196F3";
                // MAGIA: Jeśli jesteśmy w podkategoriach, dziedziczymy kolor z CurrentParentCategory!
                SelectedColor = CurrentParentCategory?.ColorHex ?? RecentColors.FirstOrDefault() ?? "#2196F3";
                IsFormVisible = true;
            }
        }

        [RelayCommand]
        private async Task SaveCategoryAsync()
        {
            ClearErrors();

            // 1. Walidacja
            var validationResult = await _validator.ValidateAsync(this);

            if (!validationResult.IsValid)
            {
                // 2. Mapowanie błędu do UI
                var error = validationResult.Errors.FirstOrDefault(e => e.PropertyName == nameof(CategoryName));
                if (error != null)
                {
                    CategoryNameError = error.ErrorMessage;
                }
                return; // Zatrzymujemy zapis
            }

            SaveColorToRecents(SelectedColor); // Zapisujemy kolor na paletę

            // 3. Zapis (Twój dotychczasowy kod)
            if (IsEditing && _categoryBeingEdited != null)
            {
                _categoryBeingEdited.Name = CategoryName;
                _categoryBeingEdited.ColorHex = SelectedColor;
                await _databaseService.SaveCategoryAsync(_categoryBeingEdited);
            }
            else
            {
                var newCategory = new Category
                {
                    Name = CategoryName,
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
            ClearErrors();

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
            SelectedColor = "#HEX"; //AvailableColors.FirstOrDefault() ?? "#2196F3";
            IsFormVisible = false;
            ClearErrors();
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
            // 1. Sprawdzamy w bazie, czy kategoria ma podkategorie
            var allCategories = await _databaseService.GetCategoriesAsync(includeArchived: false);
            bool hasSubcategories = allCategories.Any(c => c.ParentId == categoryToDelete.Id);

            if (hasSubcategories)
            {
                // 2. Twarde potwierdzenie (Zabezpieczenie UX przed misclickiem)
                bool confirm = await Shell.Current.DisplayAlertAsync(
                    AppResources.WarningTitle ?? "Uwaga",
                    AppResources.DeleteCatCascadeWarningMsg ?? "Ta kategoria posiada podkategorie...",
                    AppResources.YesBtn ?? "Tak",
                    AppResources.CancelBtn ?? "Anuluj");

                if (!confirm)
                {
                    // Użytkownik zrezygnował - wychodzimy z trybu usuwania dla tego elementu
                    var item = Categories.FirstOrDefault(c => c.Category.Id == categoryToDelete.Id);
                    if (item != null) item.IsDeleteMode = false;
                    return;
                }
            }

            // 3. Użytkownik potwierdził lub kategoria nie ma dzieci - odpalamy Soft Delete z kaskadą
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
        [NotifyPropertyChangedFor(nameof(ShowDeleteWarning))] // NOWOŚĆ: Odświeża ostrzeżenie przy zmianie trybu
        public partial bool IsDeleteMode { get; set; }

        [ObservableProperty]
        public partial bool IsBeingDragged { get; set; }

        [ObservableProperty]
        public partial bool IsDragTarget { get; set; }

        // NOWOŚĆ: Trzyma ilość podkategorii
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(HasSubcategories))]
        [NotifyPropertyChangedFor(nameof(ShowDeleteWarning))] // NOWOŚĆ: Odświeża ostrzeżenie przy zmianie trybu
        public partial int SubcategoriesCount { get; set; }

        // NOWOŚĆ: Trzyma nazwy podkategorii po przecinku
        [ObservableProperty]
        public partial string SubcategoriesText { get; set; } = string.Empty;

        // Magia: Zwraca Prawdę, jeśli kategoria ma dzieci (użyjemy do ukrywania napisu)
        public bool HasSubcategories => SubcategoriesCount > 0;

        public bool ShowDeleteWarning => IsDeleteMode && HasSubcategories;

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