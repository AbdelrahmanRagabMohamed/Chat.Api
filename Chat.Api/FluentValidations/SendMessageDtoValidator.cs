using Chat.Api.DTOs;
using FluentValidation;

public class SendMessageDtoValidator : AbstractValidator<SendMessageDto>
{
    public SendMessageDtoValidator()
    {
        RuleFor(x => x.ReceiverId).GreaterThan(0);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(1000);
    }
}
