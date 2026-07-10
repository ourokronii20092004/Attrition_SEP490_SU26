using Enemy.Service.DTOs;
using FluentValidation;

namespace Enemy.Service.Validators;

/// <summary>Shared item-field rules applied to both Create and Update requests (admin CRUD was
/// previously unvalidated — empty names and out-of-range stacks slipped through).</summary>
internal static class ItemFieldRules
{
    public static void ApplyTo<T>(AbstractValidator<T> v,
        System.Linq.Expressions.Expression<Func<T, string>> name,
        System.Linq.Expressions.Expression<Func<T, string>> category,
        System.Linq.Expressions.Expression<Func<T, string>> rarity,
        System.Linq.Expressions.Expression<Func<T, string?>> description,
        System.Linq.Expressions.Expression<Func<T, int>> maxStack,
        System.Linq.Expressions.Expression<Func<T, List<ItemModifierDto>?>> modifiers)
    {
        v.RuleFor(name).NotEmpty().MaximumLength(100);
        v.RuleFor(category).NotEmpty().MaximumLength(50);
        v.RuleFor(rarity).NotEmpty().MaximumLength(50);
        v.RuleFor(description).MaximumLength(2000);
        v.RuleFor(maxStack).InclusiveBetween(1, 9999)
            .WithMessage("Max stack must be between 1 and 9999.");
        v.RuleFor(modifiers).Must(m => m == null || m.Count <= 50)
            .WithMessage("An item cannot have more than 50 modifiers.");
        // Per-element modifier validation is added by each concrete validator below — RuleForEach
        // can't infer the element type through this generic helper.
    }
}

public class ItemCreateRequestValidator : AbstractValidator<ItemCreateRequest>
{
    public ItemCreateRequestValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().MaximumLength(64);
        ItemFieldRules.ApplyTo(this,
            x => x.Name, x => x.Category, x => x.Rarity, x => x.Description, x => x.MaxStack, x => x.Modifiers);
        RuleForEach(x => x.Modifiers).SetValidator(new ItemModifierDtoValidator());
    }
}

public class ItemUpdateRequestValidator : AbstractValidator<ItemUpdateRequest>
{
    public ItemUpdateRequestValidator()
    {
        ItemFieldRules.ApplyTo(this,
            x => x.Name, x => x.Category, x => x.Rarity, x => x.Description, x => x.MaxStack, x => x.Modifiers);
        RuleForEach(x => x.Modifiers).SetValidator(new ItemModifierDtoValidator());
    }
}

public class ItemModifierDtoValidator : AbstractValidator<ItemModifierDto>
{
    public ItemModifierDtoValidator()
    {
        RuleFor(x => x.Stat).NotEmpty().MaximumLength(50);
        // Modifiers may be negative (e.g. a cursed item), but keep them in a sane range.
        RuleFor(x => x.Amount).InclusiveBetween(-100000, 100000);
    }
}
