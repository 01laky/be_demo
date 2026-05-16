using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.UpdateFaceChatRoomDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateFaceChatRoomRequestValidator : AbstractValidator<BeDemo.Api.Controllers.UpdateFaceChatRoomDto>
{
    public UpdateFaceChatRoomRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Title != null);
    }
}
