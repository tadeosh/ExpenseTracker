using FluentValidation;
using ExpenseTracker.ViewModels;
using ExpenseTracker.Resources.Strings;

namespace ExpenseTracker.Validators
{
    public class ProjectsValidator : AbstractValidator<ProjectsViewModel>
    {
        public ProjectsValidator()
        {
            RuleFor(x => x.ProjectName)
                .NotEmpty()
                .WithMessage(AppResources.ProjectValidationMissingData ?? "Wprowadź nazwę projektu.");
        }
    }
}