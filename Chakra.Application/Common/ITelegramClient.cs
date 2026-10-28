namespace Chakra.Application.Common;

public interface ITelegramClient
{
    Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default);
}
