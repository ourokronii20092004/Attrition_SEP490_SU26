using Enemy.Service.DTOs;
using FluentValidation;

namespace Enemy.Service.Validators;

/// <summary>The three enemy classifications. Tier is stored as a string but constrained to these.</summary>
public static class EnemyTiers
{
    public const string Normal = "Normal";
    public const string Elite = "Elite";
    public const string Boss = "Boss";
    public static readonly string[] All = { Normal, Elite, Boss };
}

public class EnemyCreateRequestValidator : AbstractValidator<EnemyCreateRequest>
{
    public EnemyCreateRequestValidator()
    {
        RuleFor(x => x.EnemyId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tier).NotEmpty().Must(t => EnemyTiers.All.Contains(t))
            .WithMessage("Tier must be one of: Normal, Elite, Boss.");
        RuleFor(x => x.Hp).GreaterThan(0);
        RuleFor(x => x.Ad).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Ap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Def).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Res).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AttackSpeed).GreaterThan(0);
        RuleFor(x => x.ExpReward).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GoldReward).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LootTable).Must(l => l == null || l.Count <= 100)
            .WithMessage("Loot table cannot exceed 100 entries.");
        RuleForEach(x => x.LootTable).SetValidator(new LootEntryDtoValidator());
    }
}

public class EnemyUpdateRequestValidator : AbstractValidator<EnemyUpdateRequest>
{
    public EnemyUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tier).NotEmpty().Must(t => EnemyTiers.All.Contains(t))
            .WithMessage("Tier must be one of: Normal, Elite, Boss.");
        RuleFor(x => x.Hp).GreaterThan(0);
        RuleFor(x => x.Ad).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Ap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Def).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Res).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AttackSpeed).GreaterThan(0);
        RuleFor(x => x.ExpReward).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GoldReward).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LootTable).Must(l => l == null || l.Count <= 100)
            .WithMessage("Loot table cannot exceed 100 entries.");
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
