using Attrition.API.DTOs;
using FluentValidation;

namespace Attrition.API.Validators;

public class CreateItemRequestValidator : AbstractValidator<CreateItemRequest>
{
    public CreateItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ItemType).NotEmpty();
        RuleFor(x => x.Rarity).NotEmpty();
        RuleFor(x => x.StackLimit).GreaterThan((short)0);
    }
}

public class CreateSkillRequestValidator : AbstractValidator<CreateSkillRequest>
{
    public CreateSkillRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SkillType).NotEmpty();
        RuleFor(x => x.CooldownSec).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RequiredLevel).GreaterThanOrEqualTo(1);
    }
}

public class CreateLevelRequestValidator : AbstractValidator<CreateLevelRequest>
{
    public CreateLevelRequestValidator()
    {
        RuleFor(x => x.LevelNumber).GreaterThan(0);
        RuleFor(x => x.ExpRequired).GreaterThanOrEqualTo(0);
    }
}

public class CreateSpawnPointRequestValidator : AbstractValidator<CreateSpawnPointRequest>
{
    public CreateSpawnPointRequestValidator()
    {
        RuleFor(x => x.EnemyId).NotEmpty();
        RuleFor(x => x.SceneName).NotEmpty();
        RuleFor(x => x.SpawnIntervalSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxActiveCount).GreaterThan(0);
    }
}

public class UpsertGameConfigRequestValidator : AbstractValidator<UpsertGameConfigRequest>
{
    public UpsertGameConfigRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty();
        RuleFor(x => x.Value).NotEmpty();
    }
}
