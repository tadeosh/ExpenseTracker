using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using System.Collections.ObjectModel;
using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.ViewModels
{
    public partial class ProjectsViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        public ObservableCollection<Project> Projects { get; } = new();

        [ObservableProperty]
        public partial string ProjectName { get; set; } = string.Empty;

        public ProjectsViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        private async Task AddProjectAsync()
        {
            if (string.IsNullOrWhiteSpace(ProjectName)) return;

            var newProject = new Project { Name = ProjectName };
            await _databaseService.SaveProjectAsync(newProject);
            Projects.Add(newProject);

            ProjectName = string.Empty;
        }

        public async Task LoadProjectsAsync()
        {
            var items = await _databaseService.GetProjectsAsync();
            Projects.Clear();
            foreach (var item in items) Projects.Add(item);
        }
    }
}