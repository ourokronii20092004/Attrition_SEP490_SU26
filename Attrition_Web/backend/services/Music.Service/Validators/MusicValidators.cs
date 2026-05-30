using FluentValidation;
using Music.Service.DTOs;

namespace Music.Service.Validators;

public class CreateAlbumRequestValidator : AbstractValidator<CreateAlbumRequest>
{
    public CreateAlbumRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AlbumType).MaximumLength(50).When(x => x.AlbumType != null);
        RuleFor(x => x.Genre).MaximumLength(100).When(x => x.Genre != null);
    }
}

public class UploadTrackRequestValidator : AbstractValidator<UploadTrackRequest>
{
    public UploadTrackRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200).When(x => x.Title != null);
        RuleFor(x => x.TrackNumber).GreaterThan(0).When(x => x.TrackNumber.HasValue);
        RuleFor(x => x.Duration).GreaterThanOrEqualTo(0).When(x => x.Duration.HasValue);
    }
}

public class UpdateTrackRequestValidator : AbstractValidator<UpdateTrackRequest>
{
    public UpdateTrackRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200).When(x => x.Title != null);
        RuleFor(x => x.TrackNumber).GreaterThan(0).When(x => x.TrackNumber.HasValue);
        RuleFor(x => x.Duration).GreaterThanOrEqualTo(0).When(x => x.Duration.HasValue);
    }
}

public class CreatePlaylistReqValidator : AbstractValidator<CreatePlaylistReq>
{
    public CreatePlaylistReqValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description != null);
    }
}
