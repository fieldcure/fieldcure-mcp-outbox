using TL;

namespace FieldCure.Mcp.Outbox.Channels;

/// <summary>
/// Sends messages to Telegram using the WTelegramClient library.
/// </summary>
public class TelegramChannel : IChannel
{
    readonly string _apiId;
    readonly string _apiHash;
    readonly string _phone;
    readonly string _sessionPath;

    /// <inheritdoc />
    public string Id { get; }
    /// <inheritdoc />
    public string Type => "telegram";
    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Initializes a new Telegram channel.
    /// </summary>
    /// <param name="id">Unique channel identifier.</param>
    /// <param name="name">Display name.</param>
    /// <param name="apiId">Telegram API ID.</param>
    /// <param name="apiHash">Telegram API hash.</param>
    /// <param name="phone">Phone number associated with the Telegram account.</param>
    /// <param name="sessionPath">File path for the WTelegram session file.</param>
    public TelegramChannel(string id, string name, string apiId, string apiHash, string phone, string sessionPath)
    {
        Id = id;
        Name = name;
        _apiId = apiId;
        _apiHash = apiHash;
        _phone = phone;
        _sessionPath = sessionPath;
    }

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new WTelegram.Client(what => what switch
            {
                "api_id" => _apiId,
                "api_hash" => _apiHash,
                "phone_number" => _phone,
                "session_pathname" => _sessionPath,
                _ => null,
            });

            await client.LoginUserIfNeeded();
            await client.SendMessageAsync(InputPeer.Self, request.Message);

            return new SendResult { Success = true };
        }
        catch (Exception ex)
        {
            return new SendResult { Success = false, Error = ex.Message };
        }
    }
}
