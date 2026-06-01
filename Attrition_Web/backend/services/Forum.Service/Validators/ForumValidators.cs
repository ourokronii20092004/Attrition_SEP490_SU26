using FluentValidation;
using Forum.Service.DTOs;
using Forum.Service.Models;

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

public class ReactRequestValidator : AbstractValidator<ReactRequest>
{
    public ReactRequestValidator()
    {
        RuleFor(x => x.ReactionType)
            .NotEmpty()
            .Must(t => t is ReactionType.Like or ReactionType.Dislike)
            .WithMessage("Reaction type must be 'like' or 'dislike'.");
    }
}

public class ReportPostReqValidator : AbstractValidator<ReportPostReq>
{
    public ReportPostReqValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class RemovePostRequestValidator : AbstractValidator<RemovePostRequest>
{
    public RemovePostRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500)
            .WithMessage("A removal reason (max 500 chars) is required.");
    }
}
