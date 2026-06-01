using FluentValidation;
using Wiki.Service.DTOs;
using Wiki.Service.Models;

namespace Wiki.Service.Validators;

public class CreateArticleRequestValidator : AbstractValidator<CreateArticleRequest>
{
    public CreateArticleRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(3, 100);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Content).NotEmpty().MinimumLength(20);
        RuleFor(x => x.Status).Must(s => s is ArticleStatus.Published or ArticleStatus.Draft)
            .WithMessage("Status must be 'Published' or 'Draft'.");
    }
}

public class UpdateArticleRequestValidator : AbstractValidator<UpdateArticleRequest>
{
    public UpdateArticleRequestValidator()
    {
        RuleFor(x => x.Title).Length(3, 100).When(x => x.Title != null);
        RuleFor(x => x.Content).MinimumLength(20).When(x => x.Content != null);
        RuleFor(x => x.Status).Must(s => s is ArticleStatus.Published or ArticleStatus.Draft)
            .WithMessage("Status must be 'Published' or 'Draft'.")
            .When(x => x.Status != null);
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

public class ReviewContributionRequestValidator : AbstractValidator<ReviewContributionRequest>
{
    public ReviewContributionRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is ContributionStatus.Approved or ContributionStatus.Rejected)
            .WithMessage("Status must be 'Approved' or 'Rejected'.");
    }
}
