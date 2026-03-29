using TL;

namespace FieldCure.Mcp.Outbox.Channels;

public class TelegramChannel : IChannel
{
    readonly string _apiId;
    readonly string _apiHash;
    readonly string _phone;
    readonly string _sessionPath;

    public string Id { get; }
    public string Type => "telegram";
    public string Name { get; }

    public TelegramChannel(string id, string name, string apiId, string apiHash, string phone, string sessionPath)
    {
        Id = id;
        Name = name;
        _apiId = apiId;
        _apiHash = apiHash;
        _phone = phone;
        _sessionPath = sessionPath;
    }

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
