using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.ChatMessagesQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class ChatMessagesQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.ChatMessagesQuery>
{
    public ChatMessagesQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, ValidationConstants.PageSizeDefaultMax);
    }
}
