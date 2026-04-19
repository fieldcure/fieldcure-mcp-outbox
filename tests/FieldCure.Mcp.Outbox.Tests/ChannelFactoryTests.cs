using FieldCure.Mcp.Outbox.Channels;
using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Tests;

[TestClass]
public class ChannelFactoryTests
{
    [TestMethod]
    public void Create_Slack_ReturnsSlackChannel()
    {
        var metadata = new ChannelMetadata
        {
            Id = "test_slack",
            Type = "slack",
            Name = "test",
            Token = "xoxb-test-token",
            DefaultChannel = "general",
        };

        var channel = ChannelFactory.Create(metadata, null, new TestHttpClientFactory());

        Assert.IsInstanceOfType(channel, typeof(SlackChannel));
        Assert.AreEqual("test_slack", channel.Id);
    }

    [TestMethod]
    public void Create_Microsoft_ReturnsMicrosoftChannel()
    {
        var metadata = new ChannelMetadata
        {
            Id = "test_microsoft",
            Type = "microsoft",
            Name = "test",
            From = "test@outlook.com",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
        };

        var channel = ChannelFactory.Create(metadata, null, new TestHttpClientFactory());

        Assert.IsInstanceOfType(channel, typeof(MicrosoftChannel));
        Assert.AreEqual("test_microsoft", channel.Id);
    }

    [TestMethod]
    public void Create_Discord_ReturnsDiscordChannel()
    {
        var metadata = new ChannelMetadata
        {
            Id = "test_discord",
            Type = "discord",
            Name = "test",
            WebhookUrl = "https://discord.com/api/webhooks/123/abc",
        };

        var channel = ChannelFactory.Create(metadata, null, new TestHttpClientFactory());

        Assert.IsInstanceOfType(channel, typeof(DiscordChannel));
        Assert.AreEqual("test_discord", channel.Id);
    }

    [TestMethod]
    public void Create_UnknownType_ThrowsArgumentException()
    {
        var metadata = new ChannelMetadata
        {
            Id = "test_unknown",
            Type = "unknown",
            Name = "test",
        };

        Assert.ThrowsExactly<ArgumentException>(() =>
            ChannelFactory.Create(metadata, null, new TestHttpClientFactory()));
    }

    class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
