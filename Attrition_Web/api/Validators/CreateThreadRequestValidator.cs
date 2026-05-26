using Attrition.API.DTOs;
using FluentValidation;

namespace Attrition.API.Validators;

public class CreateThreadRequestValidator : AbstractValidator<CreateThreadRequest>
{
    public CreateThreadRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Valid category is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Thread title is required.")
            .MinimumLength(5).WithMessage("Title must be at least 5 characters.")
            .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Post content is required.")
            .MinimumLength(10).WithMessage("Post content must be at least 10 characters.");
    }
}
