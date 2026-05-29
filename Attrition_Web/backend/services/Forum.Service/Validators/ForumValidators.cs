using FluentValidation;
using Forum.Service.DTOs;

namespace Forum.Service.Validators;

public class CreateThreadRequestValidator : AbstractValidator<CreateThreadRequest>
{
    public CreateThreadRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().Length(5, 100);
        RuleFor(x => x.Content).NotEmpty().MinimumLength(10);
    }
}

public class CreatePostRequestValidator : AbstractValidator<CreatePostRequest>
{
    public CreatePostRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MinimumLength(2);
    }
}

public class UpdatePostRequestValidator : AbstractValidator<UpdatePostRequest>
{
    public UpdatePostRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MinimumLength(2);
    }
}

public class ForumCategoryRequestValidator : AbstractValidator<ForumCategoryRequest>
{
    public ForumCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 50);
    }
}
