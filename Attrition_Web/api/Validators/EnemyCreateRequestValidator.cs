using Attrition.API.DTOs;
using FluentValidation;

namespace Attrition.API.Validators;

public class EnemyCreateRequestValidator : AbstractValidator<EnemyCreateRequest>
{
    public EnemyCreateRequestValidator()
    {
        RuleFor(x => x.EnemyId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tier).NotEmpty();
        RuleFor(x => x.Hp).GreaterThan(0);
        RuleFor(x => x.Ad).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Def).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AttackSpeed).GreaterThan(0);
        RuleFor(x => x.ExpReward).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GoldReward).GreaterThanOrEqualTo(0);
    }
}
