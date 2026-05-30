using Assets.Service.DTOs;
using FluentValidation;

namespace Assets.Service.Validators;

public class UpdateAssetReqValidator : AbstractValidator<UpdateAssetReq>
{
    public UpdateAssetReqValidator()
    {
        RuleFor(x => x.AssetType).MaximumLength(50)
            .When(x => x.AssetType != null);
        RuleFor(x => x.Title).MaximumLength(200)
            .When(x => x.Title != null);
        RuleFor(x => x.Description).MaximumLength(2000)
            .When(x => x.Description != null);
        RuleFor(x => x.Tags).MaximumLength(500)
            .When(x => x.Tags != null);
    }
}
