using FluentValidation;
using Wiki.Service.DTOs;

namespace Wiki.Service.Validators;

public class CreateArticleRequestValidator : AbstractValidator<CreateArticleRequest>
{
    public CreateArticleRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(3, 100);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Content).NotEmpty().MinimumLength(20);
        RuleFor(x => x.Status).Must(s => s is "Published" or "Draft")
            .WithMessage("Status must be 'Published' or 'Draft'.");
    }
}

public class SuggestEditRequestValidator : AbstractValidator<SuggestEditRequest>
{
    public SuggestEditRequestValidator()
    {
        RuleFor(x => x.SuggestedContent).NotEmpty().MinimumLength(20);
    }
}

public class WikiCategoryRequestValidator : AbstractValidator<WikiCategoryRequest>
{
    public WikiCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 50);
    }
}
