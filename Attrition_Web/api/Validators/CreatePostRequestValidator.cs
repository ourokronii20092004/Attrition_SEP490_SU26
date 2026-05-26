using Attrition.API.DTOs;
using FluentValidation;

namespace Attrition.API.Validators;

public class CreatePostRequestValidator : AbstractValidator<CreatePostRequest>
{
    public CreatePostRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Post content is required.")
            .MinimumLength(2).WithMessage("Post content must be at least 2 characters.");
    }
}
