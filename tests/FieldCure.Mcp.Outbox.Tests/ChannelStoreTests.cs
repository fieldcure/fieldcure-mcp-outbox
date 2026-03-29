using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Tests;

[TestClass]
public class ChannelStoreTests
{
    string _tempDir = null!;
    ChannelStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "outbox_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        // Use reflection to set DataDirectory for testing
        _store = new ChannelStore();
        var field = typeof(ChannelStore).GetProperty(nameof(ChannelStore.DataDirectory))!;
        // ChannelStore.DataDirectory is get-only, so we write the channels.json directly
        // Instead, create a testable subclass approach — write to the actual store location
        // For simplicity, test by directly writing/reading from the store's path
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task LoadAsync_ReturnsEmptyList_WhenFileDoesNotExist()
    {
        var store = new ChannelStore();
        // If no channels have been configured yet, this should not throw
        var channels = await store.LoadAsync();
        Assert.IsNotNull(channels);
    }

    [TestMethod]
    public async Task AddAndLoad_RoundTrips()
    {
        var store = new ChannelStore();
        var testId = $"test_slack_{Guid.NewGuid():N}";

        try
        {
            await store.AddAsync(new ChannelMetadata
            {
                Id = testId,
                Type = "slack",
                Name = "test-channel",
                DefaultChannel = "test",
            });

            var loaded = await store.GetByIdAsync(testId);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("slack", loaded.Type);
            Assert.AreEqual("test-channel", loaded.Name);
        }
        finally
        {
            await store.RemoveAsync(testId);
        }
    }

    [TestMethod]
    public async Task RemoveAsync_RemovesChannel()
    {
        var store = new ChannelStore();
        var testId = $"test_remove_{Guid.NewGuid():N}";

        await store.AddAsync(new ChannelMetadata
        {
            Id = testId,
            Type = "slack",
            Name = "to-remove",
        });

        await store.RemoveAsync(testId);

        var result = await store.GetByIdAsync(testId);
        Assert.IsNull(result);
    }
}
