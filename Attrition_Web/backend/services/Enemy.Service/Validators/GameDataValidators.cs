using Enemy.Service.DTOs;
using FluentValidation;
using System.Text.RegularExpressions;

namespace Enemy.Service.Validators;

internal static partial class GameDataRules
{
    public static readonly string[] Categories = ["Equipment", "Accessory", "Material"];

    [GeneratedRegex("^[a-z0-9]+(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant)] private static partial Regex StableIdRegex();

    public static bool IsStableId(string? id) => id is { Length: <= 64 } && StableIdRegex().IsMatch(id);

    public static bool IsOwnedImage(string? path) => path == null || path.StartsWith("/api/assets/media/", StringComparison.Ordinal);

    public static bool Finite(float value) => float.IsFinite(value);
}

public class UnityItemImportValidator : AbstractValidator<UnityItemImport>
{
    public UnityItemImportValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().Must(GameDataRules.IsStableId); RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Category).Must(GameDataRules.Categories.Contains); RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.MaxStack).InclusiveBetween(1, 9999); RuleFor(x => x.Modifiers).Must(x => x == null || x.Count <= 50);
        RuleForEach(x => x.Modifiers).SetValidator(new ItemModifierDtoValidator()); RuleFor(x => x.ImageUrl).Must(GameDataRules.IsOwnedImage);
    }
}

public class UnityLootImportValidator : AbstractValidator<UnityLootImport>
{
    public UnityLootImportValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().Must(GameDataRules.IsStableId);
        RuleFor(x => x.DropChance).Must(x => GameDataRules.Finite(x) && x is > 0 and <= 1);
        RuleFor(x => x.MinQty).InclusiveBetween((short)1, short.MaxValue);
        RuleFor(x => x.MaxQty).GreaterThanOrEqualTo(x => x.MinQty);
    }
}

public class UnityEnemyImportValidator : AbstractValidator<UnityEnemyImport>
{
    public UnityEnemyImportValidator()
    {
        RuleFor(x => x.EnemyId).NotEmpty().Must(GameDataRules.IsStableId); RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tier).Must(EnemyTiers.All.Contains); RuleFor(x => x.Hp).GreaterThan(0).LessThanOrEqualTo(10000000);
        RuleFor(x => x.Ad).InclusiveBetween(0, 1000000); RuleFor(x => x.Ap).InclusiveBetween(0, 1000000); RuleFor(x => x.Def).InclusiveBetween(0, 1000000); RuleFor(x => x.Res).InclusiveBetween(0, 1000000); RuleFor(x => x.Poise).InclusiveBetween(0, 1000000); RuleFor(x => x.ExpReward).InclusiveBetween(0, 10000000);
        RuleFor(x => x.PoiseRecoveryTime).Must(x => GameDataRules.Finite(x) && x >= 0); RuleFor(x => x.PatrolSpeed).Must(x => GameDataRules.Finite(x) && x >= 0); RuleFor(x => x.ChaseSpeed).Must(x => GameDataRules.Finite(x) && x >= 0); RuleFor(x => x.AttackSpeed).Must(x => GameDataRules.Finite(x) && x > 0); RuleFor(x => x.ImageUrl).Must(GameDataRules.IsOwnedImage);
        RuleFor(x => x.LootTable).Must(x => x == null || x.Count <= 100);
        RuleForEach(x => x.LootTable).SetValidator(new UnityLootImportValidator());
        RuleFor(x => x.LootTable).Must(x => x == null || x.Select(y => y.ItemId).Distinct(StringComparer.Ordinal).Count() == x.Count)
            .WithMessage("Loot item IDs must be unique per enemy.");
    }
}

public class GameDataImportRequestValidator : AbstractValidator<GameDataImportRequest>
{
    public GameDataImportRequestValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(x => x != null && x.Count <= 1000); RuleFor(x => x.Enemies).NotNull().Must(x => x != null && x.Count <= 500);
        RuleForEach(x => x.Items).SetValidator(new UnityItemImportValidator()); RuleForEach(x => x.Enemies).SetValidator(new UnityEnemyImportValidator());
        RuleFor(x => x).Custom((r, c) => { if (r.Items == null || r.Enemies == null) return; AddDuplicates(r.Items.Select(x => x.ItemId), "Items", c); AddDuplicates(r.Enemies.Select(x => x.EnemyId), "Enemies", c); });
    }

    private static void AddDuplicates(IEnumerable<string> ids, string property, ValidationContext<GameDataImportRequest> context)
    { foreach (var id in ids.GroupBy(x => x, StringComparer.Ordinal).Where(x => x.Count() > 1).Select(x => x.Key)) context.AddFailure(property, $"Duplicate stable ID '{id}'."); }
}