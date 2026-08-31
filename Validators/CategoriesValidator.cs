using FluentValidation;
using ExpenseTracker.ViewModels;
using ExpenseTracker.Resources.Strings;

namespace ExpenseTracker.Validators
{
    public class CategoriesValidator : AbstractValidator<CategoriesViewModel>
    {
        public CategoriesValidator()
        {
            // Walidacja nazwy kategorii
            RuleFor(x => x.CategoryName)
                .NotEmpty()
                .WithMessage(AppResources.AccountValidationMissingData ?? "Wprowadź nazwę kategorii.");
        }
    }
}