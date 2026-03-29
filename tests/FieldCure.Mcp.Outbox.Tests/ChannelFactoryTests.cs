using FieldCure.Mcp.Outbox.Channels;
using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Tests;

/// <summary>
/// Tests for <see cref="ChannelFactory"/> channel creation logic.
/// </summary>
[TestClass]
public class ChannelFactoryTests
{
    [TestMethod]
    public void Create_Slack_ReturnsSlackChannel()
    {
        var credentials = new CredentialManager();
        var testId = $"test_factory_slack_{Guid.NewGuid():N}";

        try
        {
            credentials.Store($"FieldCure.Outbox:{testId}", "xoxb-test-token");

            var metadata = new ChannelMetadata
            {
                Id = testId,
                Type = "slack",
                Name = "test",
                DefaultChannel = "general",
            };

            var httpFactory = new TestHttpClientFactory();
            var channel = ChannelFactory.Create(metadata, credentials, httpFactory);

            Assert.IsInstanceOfType(channel, typeof(SlackChannel));
            Assert.AreEqual(testId, channel.Id);
        }
        finally
        {
            credentials.Delete($"FieldCure.Outbox:{testId}");
        }
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
            ChannelFactory.Create(metadata, new CredentialManager(), new TestHttpClientFactory()));
    }

    class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
