using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Albums;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Albums.AlbumListQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class AlbumListQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Albums.AlbumListQuery>
{
    public AlbumListQueryValidator()
    {
        RuleFor(x => x.FaceId).OptionalPositiveFaceId();
    }
}
