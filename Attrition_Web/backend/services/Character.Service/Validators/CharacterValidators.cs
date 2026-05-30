using Character.Service.DTOs;
using FluentValidation;

namespace Character.Service.Validators;

public class SnapshotIngestRequestValidator : AbstractValidator<SnapshotIngestRequest>
{
    public SnapshotIngestRequestValidator()
    {
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Archetype).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Level).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Hp).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxHp).GreaterThan(0);
        RuleFor(x => x.Gold).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RoomCode).MaximumLength(32);
        RuleFor(x => x.EventType).NotEmpty().MaximumLength(20);
        RuleFor(x => x.PlaytimeSeconds).GreaterThanOrEqualTo(0);
    }
}
