using Enemy.Service.DTOs;
using FluentValidation;
using System.Text.RegularExpressions;

namespace Enemy.Service.Validators;

internal static partial class GameDataRules
{
    public static readonly string[] Categories = { "Equipment", "Accessory", "Skill", "Material" };
    public static readonly string[] Elements = { "Fire", "Wood", "Earth", "Thunder", "Thrust" };
    public static readonly string[] DamageTypes = { "Physical", "Magic", "True" };
    public static readonly string[] Deliveries = { "AreaInstant", "Projectile" };
    public static readonly string[] HitShapes = { "Cone", "Circle", "Rectangle" };

    [GeneratedRegex("^[a-z0-9]+(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex StableIdRegex();

    public static bool IsStableId(string? id) => id is { Length: <= 64 } && StableIdRegex().IsMatch(id);
    public static bool IsOwnedImage(string? path) => path == null || path.StartsWith("/api/assets/media/", StringComparison.Ordinal);
    public static bool Finite(float value) => float.IsFinite(value);
}

public class SkillConfigDtoValidator : AbstractValidator<SkillConfigDto>
{
    public SkillConfigDtoValidator()
    {
        RuleFor(x => x.SkillId).NotEmpty().Must(GameDataRules.IsStableId).WithMessage("SkillId must be a canonical lower_snake_case ID up to 64 characters.");
        RuleFor(x => x.Element).Must(GameDataRules.Elements.Contains);
        RuleFor(x => x.DamageType).Must(GameDataRules.DamageTypes.Contains);
        RuleFor(x => x.Delivery).Must(GameDataRules.Deliveries.Contains);
        RuleFor(x => x.HitShape).Must(GameDataRules.HitShapes.Contains);
        RuleFor(x => x.ManaCost).InclusiveBetween(0, 100000);
        RuleFor(x => x.BaseDamage).InclusiveBetween(0, 1000000);
        RuleFor(x => x.ProjectileCount).InclusiveBetween(1, 100);
        FiniteNonNegative(x => x.CastTime);
        FiniteNonNegative(x => x.Cooldown);
        FiniteNonNegative(x => x.ApScaling);
        FiniteNonNegative(x => x.KnockbackForce);
        FiniteNonNegative(x => x.TickInterval);
        FiniteNonNegative(x => x.SweetSpotRadius);
        FiniteNonNegative(x => x.SweetSpotMultiplier);
        FiniteNonNegative(x => x.Range);
        FiniteNonNegative(x => x.RectWidth);
        FiniteNonNegative(x => x.RectHeight);
        FiniteNonNegative(x => x.ProjectileSpeed);
        FiniteNonNegative(x => x.SpreadAngle);
        FiniteNonNegative(x => x.VfxLifetime);
        RuleFor(x => x.Angle).Must(GameDataRules.Finite).InclusiveBetween(0, 360);
        RuleFor(x => x.OffsetX).Must(GameDataRules.Finite);
        RuleFor(x => x.OffsetY).Must(GameDataRules.Finite);
        RuleFor(x => x.ActiveStartFrac).Must(GameDataRules.Finite).InclusiveBetween(0, 1);
        RuleFor(x => x.ActiveEndFrac).Must(GameDataRules.Finite).InclusiveBetween(0, 1)
            .GreaterThanOrEqualTo(x => x.ActiveStartFrac);
        RuleFor(x => x.ImageUrl).Must(GameDataRules.IsOwnedImage).WithMessage("ImageUrl must be an Assets service media path.");
    }

    private void FiniteNonNegative(System.Linq.Expressions.Expression<Func<SkillConfigDto, float>> field) =>
        RuleFor(field).Must(x => GameDataRules.Finite(x) && x >= 0).WithMessage("Value must be finite and non-negative.");
}

public class UnityItemImportValidator : AbstractValidator<UnityItemImport>
{
    public UnityItemImportValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().Must(GameDataRules.IsStableId);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Category).Must(GameDataRules.Categories.Contains);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.MaxStack).InclusiveBetween(1, 9999);
        RuleFor(x => x.Modifiers).Must(x => x == null || x.Count <= 50);
        RuleForEach(x => x.Modifiers).SetValidator(new ItemModifierDtoValidator());
        RuleFor(x => x.ImageUrl).Must(GameDataRules.IsOwnedImage);
    }
}

public class UnityEnemyImportValidator : AbstractValidator<UnityEnemyImport>
{
    public UnityEnemyImportValidator()
    {
        RuleFor(x => x.EnemyId).NotEmpty().Must(GameDataRules.IsStableId);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tier).Must(EnemyTiers.All.Contains);
        RuleFor(x => x.Hp).GreaterThan(0).LessThanOrEqualTo(10000000);
        RuleFor(x => x.Ad).InclusiveBetween(0, 1000000);
        RuleFor(x => x.Ap).InclusiveBetween(0, 1000000);
        RuleFor(x => x.Def).InclusiveBetween(0, 1000000);
        RuleFor(x => x.Res).InclusiveBetween(0, 1000000);
        RuleFor(x => x.Poise).InclusiveBetween(0, 1000000);
        RuleFor(x => x.ExpReward).InclusiveBetween(0, 10000000);
        RuleFor(x => x.PoiseRecoveryTime).Must(x => GameDataRules.Finite(x) && x >= 0);
        RuleFor(x => x.PatrolSpeed).Must(x => GameDataRules.Finite(x) && x >= 0);
        RuleFor(x => x.ChaseSpeed).Must(x => GameDataRules.Finite(x) && x >= 0);
        RuleFor(x => x.AttackSpeed).Must(x => GameDataRules.Finite(x) && x > 0);
        RuleFor(x => x.ImageUrl).Must(GameDataRules.IsOwnedImage);
    }
}

public class GameDataImportRequestValidator : AbstractValidator<GameDataImportRequest>
{
    public GameDataImportRequestValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(x => x != null && x.Count <= 1000);
        RuleFor(x => x.Enemies).NotNull().Must(x => x != null && x.Count <= 500);
        RuleFor(x => x.Skills).NotNull().Must(x => x != null && x.Count <= 200);
        RuleForEach(x => x.Items).SetValidator(new UnityItemImportValidator());
        RuleForEach(x => x.Enemies).SetValidator(new UnityEnemyImportValidator());
        RuleForEach(x => x.Skills).SetValidator(new SkillConfigDtoValidator());
        RuleFor(x => x).Custom((request, context) =>
        {
            if (request.Items == null || request.Enemies == null || request.Skills == null) return;
            AddDuplicates(request.Items.Select(x => x.ItemId), "Items", context);
            AddDuplicates(request.Enemies.Select(x => x.EnemyId), "Enemies", context);
            AddDuplicates(request.Skills.Select(x => x.SkillId), "Skills", context);
            var itemIds = request.Items.Select(x => x.ItemId).ToHashSet(StringComparer.Ordinal);
            foreach (var skill in request.Skills)
                if (!itemIds.Contains(skill.SkillId))
                    context.AddFailure("Skills", $"Skill '{skill.SkillId}' must also exist in Items.");
            foreach (var item in request.Items.Where(x => x.Category == "Skill"))
                if (!request.Skills.Any(x => x.SkillId == item.ItemId))
                    context.AddFailure("Items", $"Skill item '{item.ItemId}' is missing its skill config.");
        });
    }

    private static void AddDuplicates(IEnumerable<string> ids, string property, ValidationContext<GameDataImportRequest> context)
    {
        foreach (var id in ids.GroupBy(x => x, StringComparer.Ordinal).Where(x => x.Count() > 1).Select(x => x.Key))
            context.AddFailure(property, $"Duplicate stable ID '{id}'.");
    }
}
