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

/// <summary>Shared stat-field validation rules applied to both Create and Update requests.</summary>
internal static class EnemyStatRules
{
    public static void ApplyTo<T>(AbstractValidator<T> v,
        System.Linq.Expressions.Expression<Func<T, string>> name,
        System.Linq.Expressions.Expression<Func<T, string>> tier,
        System.Linq.Expressions.Expression<Func<T, int>> hp,
        System.Linq.Expressions.Expression<Func<T, int>> ad,
        System.Linq.Expressions.Expression<Func<T, int>> ap,
        System.Linq.Expressions.Expression<Func<T, int>> def,
        System.Linq.Expressions.Expression<Func<T, int>> res,
        System.Linq.Expressions.Expression<Func<T, float>> attackSpeed,
        System.Linq.Expressions.Expression<Func<T, int>> expReward,
        System.Linq.Expressions.Expression<Func<T, int>> goldReward)
    {
        v.RuleFor(name).NotEmpty().MaximumLength(100);
        v.RuleFor(tier).NotEmpty().Must(t => EnemyTiers.All.Contains(t))
            .WithMessage("Tier must be one of: Normal, Elite, Boss.");
        v.RuleFor(hp).GreaterThan(0);
        v.RuleFor(ad).GreaterThanOrEqualTo(0);
        v.RuleFor(ap).GreaterThanOrEqualTo(0);
        v.RuleFor(def).GreaterThanOrEqualTo(0);
        v.RuleFor(res).GreaterThanOrEqualTo(0);
        v.RuleFor(attackSpeed).GreaterThan(0);
        v.RuleFor(expReward).GreaterThanOrEqualTo(0);
        v.RuleFor(goldReward).GreaterThanOrEqualTo(0);
    }

    public static void ApplyUnityOverrideTo<T>(AbstractValidator<T> v,
        System.Linq.Expressions.Expression<Func<T, int>> poise,
        System.Linq.Expressions.Expression<Func<T, float>> recovery,
        System.Linq.Expressions.Expression<Func<T, float>> patrol,
        System.Linq.Expressions.Expression<Func<T, float>> chase)
    {
        v.RuleFor(poise).GreaterThanOrEqualTo(0);
        v.RuleFor(recovery).Must(x => float.IsFinite(x) && x >= 0);
        v.RuleFor(patrol).Must(x => float.IsFinite(x) && x >= 0);
        v.RuleFor(chase).Must(x => float.IsFinite(x) && x >= 0);
    }

}

public class EnemyCreateRequestValidator : AbstractValidator<EnemyCreateRequest>
{
    public EnemyCreateRequestValidator()
    {
        RuleFor(x => x.EnemyId).NotEmpty().Must(GameDataRules.IsStableId);
        RuleFor(x => x.ImageUrl).Must(GameDataRules.IsOwnedImage);
        EnemyStatRules.ApplyTo(this,
            x => x.Name, x => x.Tier, x => x.Hp, x => x.Ad, x => x.Ap,
            x => x.Def, x => x.Res, x => x.AttackSpeed, x => x.ExpReward,
            x => x.GoldReward);
        EnemyStatRules.ApplyUnityOverrideTo(this, x => x.Poise, x => x.PoiseRecoveryTime, x => x.PatrolSpeed, x => x.ChaseSpeed);
        RuleFor(x => x.LootTable).Must(l => l == null || l.Count <= 100)
            .WithMessage("Loot table cannot exceed 100 entries.");
        RuleForEach(x => x.LootTable).SetValidator(new LootEntryDtoValidator());
    }
}

public class EnemyUpdateRequestValidator : AbstractValidator<EnemyUpdateRequest>
{
    public EnemyUpdateRequestValidator()
    {
        RuleFor(x => x.ImageUrl).Must(GameDataRules.IsOwnedImage);
        EnemyStatRules.ApplyTo(this,
            x => x.Name, x => x.Tier, x => x.Hp, x => x.Ad, x => x.Ap,
            x => x.Def, x => x.Res, x => x.AttackSpeed, x => x.ExpReward,
            x => x.GoldReward);
        EnemyStatRules.ApplyUnityOverrideTo(this, x => x.Poise, x => x.PoiseRecoveryTime, x => x.PatrolSpeed, x => x.ChaseSpeed);
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
