using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FieldCure.Mcp.Outbox.Channels;

public class SmtpChannel : IChannel
{
    readonly string _from;
    readonly string _host;
    readonly int _port;
    readonly bool _useSsl;
    readonly string _username;
    readonly string _password;

    public string Id { get; }
    public string Type => "smtp";
    public string Name { get; }

    public SmtpChannel(string id, string name, string from, string host, int port, bool useSsl,
        string username, string password)
    {
        Id = id;
        Name = name;
        _from = from;
        _host = host;
        _port = port;
        _useSsl = useSsl;
        _username = username;
        _password = password;
    }

    public async Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.To))
            return new SendResult { Success = false, Error = "'to' is required for SMTP channels.", ErrorCode = "missing_to" };

        if (string.IsNullOrWhiteSpace(request.Subject))
            return new SendResult { Success = false, Error = "'subject' is required for SMTP channels.", ErrorCode = "missing_subject" };

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_from));
            message.To.Add(MailboxAddress.Parse(request.To));
            message.Subject = request.Subject;
            message.Body = new TextPart("plain") { Text = request.Message };

            using var client = new SmtpClient();

            var secureOption = _port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_host, _port, secureOption, cancellationToken);
            await client.AuthenticateAsync(_username, _password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            return new SendResult { Success = true };
        }
        catch (Exception ex)
        {
            return new SendResult { Success = false, Error = ex.Message };
        }
    }
}
