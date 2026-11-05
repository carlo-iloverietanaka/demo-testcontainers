using FluentValidation;

namespace SMS.Application.Services.Sms;

public class SmsValidator : AbstractValidator<SmsInput>
{
    public SmsValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty();
        RuleFor(x => x.ContentType)
            .NotEmpty();
        RuleFor(x => x.Sender)
            .NotEmpty();
        RuleFor(x => x.Recipient)
            .NotEmpty();
        RuleFor(x => x.Medium)
            .NotEmpty();
    }
}