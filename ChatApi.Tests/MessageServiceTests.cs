using Chat.Api.DTOs;
using ChatApi.Hubs;
using ChatApi.Interfaces;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Moq;

public class MessageServiceTests
{
    private readonly Mock<IMessageRepository> _messageRepoMock = new();
    private readonly Mock<IConversationRepository> _conversationRepoMock = new();
    private readonly Mock<IHubContext<ChatHub>> _hubContextMock = new();
    private readonly Mock<IMemoryCache> _cacheMock = new();

    [Fact]
    public async Task SendMessageAsync_ShouldReturnResponse_WhenMessageIsSent()
    {
        // Arrange
        int senderId = 1;
        var dto = new SendMessageDto { ReceiverId = 2, Content = "Hello!" };
        var fakeConversation = new Conversation { Id = 1 };

        _conversationRepoMock.Setup(r => r.GetOrCreateConversationAsync(senderId, dto.ReceiverId))
            .ReturnsAsync(fakeConversation);

        _messageRepoMock.Setup(r => r.CreateMessageAsync(It.IsAny<Message>()))
            .Returns(Task.CompletedTask);

        _conversationRepoMock.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        var service = new MessageService(
            _messageRepoMock.Object,
            _conversationRepoMock.Object,
            _hubContextMock.Object,
            _cacheMock.Object
        );

        // Act
        var result = await service.SendMessageAsync(senderId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(senderId, result.SenderId);
        Assert.Equal(dto.Content, result.Message.Content);
    }
}
