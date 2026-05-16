using BeDemo.Api.Services;
using FluentValidation;

namespace BeDemo.Api.Validation.Rules;

/// <summary>Reusable FluentValidation rule extensions (endpoint-schema-validation §5).</summary>
public static class BeDemoValidationExtensions
{
    /// <summary>Rejects ASCII NUL in user-controlled strings (OAuth2 / registration hardening).</summary>
    public static IRuleBuilderOptions<T, string?> NoNullBytes<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.Must(v => v == null || !v.Contains('\0'))
            .WithMessage("Value must not contain null bytes.")
            .WithErrorCode("val_null_byte");

    /// <summary>Absolute http/https URL only — wraps <see cref="ContentModerationHelpers.IsSafeHttpUrl"/>.</summary>
    public static IRuleBuilderOptions<T, string?> SafeHttpUrl<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.Must(v => string.IsNullOrEmpty(v) || ContentModerationHelpers.IsSafeHttpUrl(v))
            .WithMessage("URL must be absolute http or https.")
            .WithErrorCode("val_url_unsafe");

    /// <summary>page ≥ 1, pageSize in 1..100.</summary>
    public static void ApplyPaginationRules<T>(this AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, int>> page,
        System.Linq.Expressions.Expression<Func<T, int>> pageSize)
    {
        validator.RuleFor(page).GreaterThanOrEqualTo(1).WithErrorCode("val_page_min");
        validator.RuleFor(pageSize).InclusiveBetween(1, ValidationConstants.PageSizeDefaultMax)
            .WithErrorCode("val_page_size_range");
    }

    /// <summary>Registration mail platform: only <c>mobile</c> or empty.</summary>
    public static IRuleBuilderOptions<T, string?> RegistrationPlatform<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.Must(v => string.IsNullOrWhiteSpace(v) ||
                             string.Equals(v, "mobile", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Platform must be 'mobile' when provided.")
            .WithErrorCode("val_platform_invalid");

    /// <summary>Push device platform: ios or android.</summary>
    public static IRuleBuilderOptions<T, string> PushPlatform<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder.Must(v => v.Equals("ios", StringComparison.OrdinalIgnoreCase) ||
                              v.Equals("android", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Platform must be 'ios' or 'android'.")
            .WithErrorCode("val_push_platform_invalid");

    /// <summary>faceId query parameter when present must be &gt; 0.</summary>
    public static IRuleBuilderOptions<T, int?> OptionalPositiveFaceId<T>(this IRuleBuilder<T, int?> ruleBuilder) =>
        ruleBuilder.Must(v => v == null || v > 0)
            .WithMessage("faceId must be greater than zero when provided.")
            .WithErrorCode("val_face_id_invalid");

    public static IRuleBuilderOptions<T, int> PositiveFaceId<T>(this IRuleBuilder<T, int> ruleBuilder) =>
        ruleBuilder.GreaterThan(0).WithErrorCode("val_face_id_invalid");
}
