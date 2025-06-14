using Chat.Api.DTOs;
using FluentValidation;

public class EditMessageDtoValidator : AbstractValidator<EditMessageDto>
{
    public EditMessageDtoValidator()
    {
        RuleFor(x => x.MessageId).GreaterThan(0);
        RuleFor(x => x.NewContent).NotEmpty().MaximumLength(1000);
    }
}
