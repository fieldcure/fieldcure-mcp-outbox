using System.Text.Json;
using FieldCure.Mcp.Outbox.Credentials;
using FieldCure.Mcp.Outbox.Interaction;
using ModelContextProtocol.Protocol;

namespace FieldCure.Mcp.Outbox.Tests;

[TestClass]
public class OutboxSecretResolverTests
{
    [TestMethod]
    public async Task ResolveFieldsAsync_UsesEnvironmentValue_WithoutCallingGate()
    {
        const string envVar = "OUTBOX_TEST_RESOLVER_ENV";
        Environment.SetEnvironmentVariable(envVar, "env-secret");

        try
        {
            var resolver = new OutboxSecretResolver();
            var gate = new FakeElicitGate();
            var result = await resolver.ResolveFieldsAsync(gate,
            [
                new SecretFieldRequest(
                    "bot_token",
                    envVar,
                    "Bot Token",
                    "Slack bot token",
                    "Enter the bot token."),
            ], CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual("env-secret", result["bot_token"]);
            Assert.AreEqual(0, gate.Calls.Count);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [TestMethod]
    public async Task ResolveFieldsAsync_UsesElicitation_WhenValueIsMissing()
    {
        const string envVar = "OUTBOX_TEST_RESOLVER_ELICIT";
        Environment.SetEnvironmentVariable(envVar, null);

        var resolver = new OutboxSecretResolver();
        var gate = new FakeElicitGate();
        gate.Results.Enqueue(new ElicitGateResult(
            IsAccepted: true,
            Content: new Dictionary<string, JsonElement>
            {
                ["client_secret"] = JsonDocument.Parse("{\"v\":\"elicited-secret\"}").RootElement.GetProperty("v"),
            }));

        var result = await resolver.ResolveFieldsAsync(gate,
        [
            new SecretFieldRequest(
                "client_secret",
                envVar,
                "Client Secret",
                "OAuth client secret",
                "Enter the client secret."),
        ], CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual("elicited-secret", result["client_secret"]);
        Assert.AreEqual(1, gate.Calls.Count);

        var second = await resolver.ResolveFieldsAsync(gate,
        [
            new SecretFieldRequest(
                "client_secret",
                envVar,
                "Client Secret",
                "OAuth client secret",
                "Enter the client secret."),
        ], CancellationToken.None);

        Assert.IsNotNull(second);
        Assert.AreEqual("elicited-secret", second["client_secret"]);
        Assert.AreEqual(1, gate.Calls.Count);
    }

    [TestMethod]
    public async Task ResolveFieldsAsync_ReturnsNull_WhenGateIsUnsupported_AndValueIsMissing()
    {
        const string envVar = "OUTBOX_TEST_RESOLVER_MISSING";
        Environment.SetEnvironmentVariable(envVar, null);

        var resolver = new OutboxSecretResolver();
        var gate = new FakeElicitGate { IsSupported = false };

        var result = await resolver.ResolveFieldsAsync(gate,
        [
            new SecretFieldRequest(
                "api_key",
                envVar,
                "API Key",
                "Provider API key",
                "Enter the API key."),
        ], CancellationToken.None);

        Assert.IsNull(result);
        Assert.AreEqual(0, gate.Calls.Count);
    }

    sealed class FakeElicitGate : IElicitGate
    {
        public bool IsSupported { get; init; } = true;

        public Queue<ElicitGateResult> Results { get; } = new();

        public List<ElicitRequestParams> Calls { get; } = new();

        public Task<ElicitGateResult> ElicitAsync(ElicitRequestParams request, CancellationToken ct)
        {
            Calls.Add(request);
            return Task.FromResult(Results.Dequeue());
        }
    }
}
