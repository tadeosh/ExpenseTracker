using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using FluentValidation;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    public partial class ProjectsViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IDatabaseService _databaseService;
        private readonly IValidator<ProjectsViewModel> _validator;

        public ObservableCollection<ProjectDisplayItem> Projects { get; } = new();

        [ObservableProperty] public partial string ProjectName { get; set; } = string.Empty;
        [ObservableProperty] public partial string? ProjectNameError { get; set; }

        [ObservableProperty] public partial bool IsEditing { get; set; } = false;
        [ObservableProperty] public partial bool IsFormVisible { get; set; } = false;

        // NOWOŚĆ: Hierarchia
        [ObservableProperty] public partial string PageTitle { get; set; } = AppResources.ProjectsTitle ?? "Projekty";
        [ObservableProperty] public partial Project? CurrentParentProject { get; set; }

        private Project? _projectBeingEdited;
        private ProjectDisplayItem? _draggedItem;

        public ProjectsViewModel(IDatabaseService databaseService, IValidator<ProjectsViewModel> validator)
        {
            _databaseService = databaseService;
            _validator = validator;
        }

        // --- OBSŁUGA HIERARCHII (Nawigacja) ---
        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("ParentProjectId", out var idObj) && idObj is int parentId)
            {
                await LoadParentProjectAsync(parentId);
            }
        }

        private async Task LoadParentProjectAsync(int parentId)
        {
            var projects = await _databaseService.GetProjectsAsync(includeArchived: false);
            CurrentParentProject = projects.FirstOrDefault(p => p.Id == parentId);

            if (CurrentParentProject != null)
            {
                PageTitle = CurrentParentProject.Name;
                await LoadProjectsAsync();
            }
        }

        [RelayCommand]
        private async Task OpenSubprojectsAsync(ProjectDisplayItem? item)
        {
            // NOWOŚĆ: Guard Clause - Ograniczenie do 1 poziomu zagnieżdżenia.
            if (item == null || item.Project.ParentId != null)
            {
                try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
                return;
            }

            var navParams = new Dictionary<string, object>
            {
                { "ParentProjectId", item.Project.Id }
            };

            await Shell.Current.GoToAsync(nameof(Views.ProjectsPage), navParams);
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.Navigation.PopAsync();
            else
                await Shell.Current.GoToAsync("..");
        }

        // --- FORMULARZ ---
        private void ClearErrors() => ProjectNameError = null;

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
                _projectBeingEdited = null;
                ProjectName = string.Empty;
                IsFormVisible = true;
            }
        }

        [RelayCommand]
        private async Task SaveProjectAsync()
        {
            ClearErrors();

            var validationResult = await _validator.ValidateAsync(this);
            if (!validationResult.IsValid)
            {
                var error = validationResult.Errors.FirstOrDefault(e => e.PropertyName == nameof(ProjectName));
                if (error != null) ProjectNameError = error.ErrorMessage;
                return;
            }

            // NOWOŚĆ: Hard Limit na zapis
            if (!IsEditing && CurrentParentProject != null && CurrentParentProject.ParentId != null)
            {
                return;
            }

            if (IsEditing && _projectBeingEdited != null)
            {
                _projectBeingEdited.Name = ProjectName;
                await _databaseService.SaveProjectAsync(_projectBeingEdited);
            }
            else
            {
                var newProject = new Project
                {
                    Name = ProjectName,
                    ParentId = CurrentParentProject?.Id, // MAGIA: Przypisanie do rodzica!
                    DisplayOrder = Projects.Count
                };
                await _databaseService.SaveProjectAsync(newProject);
            }

            CancelEdit();
            await LoadProjectsAsync();
        }

        [RelayCommand]
        private void EditProject(Project projectToEdit)
        {
            ClearErrors();
            IsFormVisible = true;
            IsEditing = true;
            _projectBeingEdited = projectToEdit;
            ProjectName = projectToEdit.Name;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            _projectBeingEdited = null;
            ProjectName = string.Empty;
            IsFormVisible = false;
            ClearErrors();
        }

        // --- USUWANIE (Z Kaskadą) ---
        [RelayCommand]
        private void ToggleDeleteMode(ProjectDisplayItem item)
        {
            foreach (var proj in Projects.Where(p => p != item)) proj.IsDeleteMode = false;
            item.IsDeleteMode = !item.IsDeleteMode;
        }

        [RelayCommand]
        private async Task DeleteProjectAsync(Project projectToDelete)
        {
            // Sprawdzamy czy projekt ma podprojekty
            var allProjects = await _databaseService.GetProjectsAsync(includeArchived: false);
            bool hasSubprojects = allProjects.Any(p => p.ParentId == projectToDelete.Id);

            if (hasSubprojects)
            {
                bool confirm = await Shell.Current.DisplayAlertAsync(
                    AppResources.WarningTitle ?? "Uwaga",
                    AppResources.DeleteProjCascadeWarningMsg ?? "Ten projekt posiada podprojekty. Czy chcesz usunąć je wszystkie?",
                    AppResources.YesBtn ?? "Tak",
                    AppResources.CancelBtn ?? "Anuluj");

                if (!confirm)
                {
                    var item = Projects.FirstOrDefault(p => p.Project.Id == projectToDelete.Id);
                    if (item != null) item.IsDeleteMode = false;
                    return;
                }
            }

            await _databaseService.DeleteProjectAsync(projectToDelete);
            await LoadProjectsAsync();
        }

        // --- ŁADOWANIE ---
        public async Task LoadProjectsAsync()
        {
            var rawProjects = await _databaseService.GetProjectsAsync(includeArchived: false);
            Projects.Clear();

            List<Project> filteredProjects = CurrentParentProject == null
                 ? rawProjects.Where(p => p.ParentId == null).OrderBy(p => p.DisplayOrder).ToList()
                 : rawProjects.Where(p => p.ParentId == CurrentParentProject.Id).OrderBy(p => p.DisplayOrder).ToList();

            foreach (var proj in filteredProjects)
            {
                var subProjs = rawProjects.Where(p => p.ParentId == proj.Id).ToList();
                int count = subProjs.Count;
                string text = count > 0 ? string.Join(", ", subProjs.Select(p => p.Name)) : string.Empty;

                Projects.Add(new ProjectDisplayItem
                {
                    Project = proj,
                    SubprojectsCount = count,
                    SubprojectsText = text
                });
            }
            CancelEdit();
        }

        //============= przeciąganie i upuszczanie projektów w hierarchii =============
        [RelayCommand]
        private void DragStarted(ProjectDisplayItem item)
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
            foreach (var proj in Projects) proj.IsDragTarget = false;
        }

        [RelayCommand]
        private void DragOver(ProjectDisplayItem targetItem)
        {
            if (_draggedItem == null || _draggedItem == targetItem) return;
            foreach (var proj in Projects) proj.IsDragTarget = (proj == targetItem);
        }

        [RelayCommand]
        private void DragLeave(ProjectDisplayItem targetItem)
        {
            targetItem.IsDragTarget = false;
        }

        [RelayCommand]
        private async Task Drop(ProjectDisplayItem targetItem)
        {
            if (_draggedItem == null || _draggedItem == targetItem) return;

            int oldIndex = Projects.IndexOf(_draggedItem);
            int newIndex = Projects.IndexOf(targetItem);

            if (oldIndex < 0 || newIndex < 0) return;

            Projects.Move(oldIndex, newIndex);

            // Zapis nowej kolejności do bazy
            for (int i = 0; i < Projects.Count; i++)
            {
                var projectToUpdate = Projects[i].Project;
                if (projectToUpdate.DisplayOrder != i)
                {
                    projectToUpdate.DisplayOrder = i;
                    await _databaseService.SaveProjectAsync(projectToUpdate);
                }
            }
            targetItem.IsDragTarget = false;
        }
        //============= koniec przeciąganie i upuszczanie projektów w hierarchii ======
    }

    // --- KLASA POMOCNICZA DLA WIDOKU ---
    public partial class ProjectDisplayItem : ObservableObject
    {
        public Project Project { get; set; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MinusRotation))]
        [NotifyPropertyChangedFor(nameof(ShowDeleteWarning))]
        public partial bool IsDeleteMode { get; set; }

        [ObservableProperty] public partial bool IsBeingDragged { get; set; }
        [ObservableProperty] public partial bool IsDragTarget { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(HasSubprojects))]
        [NotifyPropertyChangedFor(nameof(ShowDeleteWarning))]
        public partial int SubprojectsCount { get; set; }

        [ObservableProperty]
        public partial string SubprojectsText { get; set; } = string.Empty;

        public bool HasSubprojects => SubprojectsCount > 0;
        // Zwraca Prawdę tylko dla głównych projektów (poziom 1)
        public bool IsMainProject => Project.ParentId == null;
        public bool ShowDeleteWarning => IsDeleteMode && HasSubprojects;
        public string DisplayName => SubprojectsCount > 0 ? $"{Project.Name} ({SubprojectsCount})" : Project.Name;
        public double MinusRotation => IsDeleteMode ? 90 : 0;
    }


}