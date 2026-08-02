using FluentValidation;
using Skill.Service.DTOs;

namespace Skill.Service.Validators;

internal static class SkillRules
{
    internal static bool StableId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z0-9]+(?:_[a-z0-9]+)*$");
    internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    internal static bool OwnedImage(string? value) => value == null || value.StartsWith("/api/assets/media/", StringComparison.Ordinal);
    internal static readonly string[] Elements = ["Fire", "Wood", "Earth", "Thunder", "Thrust"];
    internal static readonly string[] DamageTypes = ["Physical", "Magic", "True"];
    internal static readonly string[] Deliveries = ["AreaInstant", "Projectile", "SpawnAoE"];
    internal static readonly string[] Shapes = ["Cone", "Circle", "Rectangle"];
}

public class SkillImportDtoValidator : AbstractValidator<SkillImportDto>
{
    public SkillImportDtoValidator()
    {
        RuleFor(x => x.SkillId).Must(SkillRules.StableId);
        AddCommon(this);
    }
    internal static void AddCommon<T>(AbstractValidator<T> v) where T : SkillImportDto
    {
        v.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        v.RuleFor(x => x.Description).MaximumLength(2000);
        v.RuleFor(x => x.IconKey).MaximumLength(100);
        v.RuleFor(x => x.Rarity).NotEmpty().MaximumLength(50);
        v.RuleFor(x => x.Element).Must(SkillRules.Elements.Contains);
        v.RuleFor(x => x.DamageType).Must(SkillRules.DamageTypes.Contains);
        v.RuleFor(x => x.Delivery).Must(SkillRules.Deliveries.Contains);
        v.RuleFor(x => x.HitShape).Must(SkillRules.Shapes.Contains);
        v.RuleFor(x => x.ManaCost).InclusiveBetween(0, 1000000);
        v.RuleFor(x => x.BaseDamage).InclusiveBetween(0, 1000000);
        v.RuleFor(x => x.ProjectileCount).InclusiveBetween(1, 100);
        v.RuleFor(x => x.ImageUrl).Must(SkillRules.OwnedImage);
        v.RuleFor(x => x).Must(x => x.ActiveEndFrac >= x.ActiveStartFrac && x.ActiveStartFrac >= 0 && x.ActiveEndFrac <= 1 &&
            x.CastTime >= 0 && x.Cooldown >= 0 && x.ApScaling >= 0 && x.KnockbackForce >= 0 && x.TickInterval >= 0 &&
            x.SweetSpotRadius >= 0 && x.SweetSpotMultiplier >= 0 && x.Range >= 0 && x.Angle is >= 0 and <= 360 &&
            x.RectWidth >= 0 && x.RectHeight >= 0 && x.ProjectileSpeed >= 0 && x.SpreadAngle >= 0 && x.VfxLifetime >= 0 &&
            new[] { x.CastTime, x.Cooldown, x.ActiveStartFrac, x.ActiveEndFrac, x.ApScaling, x.KnockbackForce,
                x.TickInterval, x.SweetSpotRadius, x.SweetSpotMultiplier, x.Range, x.Angle, x.RectWidth, x.RectHeight,
                x.OffsetX, x.OffsetY, x.ProjectileSpeed, x.SpreadAngle, x.VfxLifetime }.All(SkillRules.Finite))
            .WithMessage("Skill contains invalid numeric values.");
    }
}

public class SkillUpdateRequestValidator : AbstractValidator<SkillUpdateRequest>
{
    public SkillUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.IconKey).MaximumLength(100);
        RuleFor(x => x.Rarity).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Element).Must(SkillRules.Elements.Contains);
        RuleFor(x => x.DamageType).Must(SkillRules.DamageTypes.Contains);
        RuleFor(x => x.Delivery).Must(SkillRules.Deliveries.Contains);
        RuleFor(x => x.HitShape).Must(SkillRules.Shapes.Contains);
        RuleFor(x => x.ManaCost).InclusiveBetween(0, 1000000);
        RuleFor(x => x.BaseDamage).InclusiveBetween(0, 1000000);
        RuleFor(x => x.ProjectileCount).InclusiveBetween(1, 100);
        RuleFor(x => x.ImageUrl).Must(SkillRules.OwnedImage);
        RuleFor(x => x).Must(x => x.ActiveEndFrac >= x.ActiveStartFrac && x.ActiveStartFrac >= 0 && x.ActiveEndFrac <= 1 &&
            x.CastTime >= 0 && x.Cooldown >= 0 && x.ApScaling >= 0 && x.KnockbackForce >= 0 && x.TickInterval >= 0 &&
            x.SweetSpotRadius >= 0 && x.SweetSpotMultiplier >= 0 && x.Range >= 0 && x.Angle is >= 0 and <= 360 &&
            x.RectWidth >= 0 && x.RectHeight >= 0 && x.ProjectileSpeed >= 0 && x.SpreadAngle >= 0 && x.VfxLifetime >= 0 &&
            new[] { x.CastTime, x.Cooldown, x.ActiveStartFrac, x.ActiveEndFrac, x.ApScaling, x.KnockbackForce,
                x.TickInterval, x.SweetSpotRadius, x.SweetSpotMultiplier, x.Range, x.Angle, x.RectWidth, x.RectHeight,
                x.OffsetX, x.OffsetY, x.ProjectileSpeed, x.SpreadAngle, x.VfxLifetime }.All(SkillRules.Finite))
            .WithMessage("Skill contains invalid numeric values.");
    }
}

public class SkillImportRequestValidator : AbstractValidator<SkillImportRequest>
{
    public SkillImportRequestValidator()
    {
        RuleFor(x => x.Skills).NotNull().Must(x => x != null && x.Count <= 200);
        RuleForEach(x => x.Skills).SetValidator(new SkillImportDtoValidator());
        RuleFor(x => x.Skills).Must(x => x == null || x.Select(s => s.SkillId).Distinct(StringComparer.Ordinal).Count() == x.Count)
            .WithMessage("Skill IDs must be unique.");
    }
}
