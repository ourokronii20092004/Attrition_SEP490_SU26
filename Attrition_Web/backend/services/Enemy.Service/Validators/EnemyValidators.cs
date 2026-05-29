using Enemy.Service.DTOs;
using FluentValidation;

namespace Enemy.Service.Validators;

public class EnemyCreateRequestValidator : AbstractValidator<EnemyCreateRequest>
{
    public EnemyCreateRequestValidator()
    {
        RuleFor(x => x.EnemyId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tier).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Hp).GreaterThan(0);
        RuleFor(x => x.Ad).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Ap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Def).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Res).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AttackSpeed).GreaterThan(0);
        RuleFor(x => x.ExpReward).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GoldReward).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.LootTable).SetValidator(new LootEntryDtoValidator());
    }
}

public class EnemyUpdateRequestValidator : AbstractValidator<EnemyUpdateRequest>
{
    public EnemyUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tier).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Hp).GreaterThan(0);
        RuleFor(x => x.Ad).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Ap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Def).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Res).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AttackSpeed).GreaterThan(0);
        RuleFor(x => x.ExpReward).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GoldReward).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.LootTable).SetValidator(new LootEntryDtoValidator());
    }
}

public class LootEntryDtoValidator : AbstractValidator<LootEntryDto>
{
    public LootEntryDtoValidator()
    {
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rarity).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DropChance).InclusiveBetween(0f, 1f);
        RuleFor(x => x.MinQty).GreaterThan((short)0);
        RuleFor(x => x.MaxQty).GreaterThanOrEqualTo(x => x.MinQty);
    }
}
