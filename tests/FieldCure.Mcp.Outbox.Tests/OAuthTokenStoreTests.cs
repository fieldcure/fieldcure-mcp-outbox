using FieldCure.Mcp.Outbox.Channels;
using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Tests;

[TestClass]
public class OAuthTokenStoreTests
{
    string _tempDir = null!;
    OAuthTokenStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "outbox_tokens_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _store = new OAuthTokenStore(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task SaveAndGet_RoundTrips_ByChannelId()
    {
        var token = new MicrosoftTokenData
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
        };

        await _store.SaveAsync("microsoft_1", token);

        var loaded = await _store.GetAsync<MicrosoftTokenData>("microsoft_1");
        Assert.IsNotNull(loaded);
        Assert.AreEqual("access", loaded.AccessToken);
        Assert.AreEqual("refresh", loaded.RefreshToken);
    }

    [TestMethod]
    public async Task RemoveAsync_DeletesOnlyRequestedEntry()
    {
        await _store.SaveAsync("microsoft_1", new MicrosoftTokenData
        {
            AccessToken = "m1",
            RefreshToken = "r1",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        });

        await _store.SaveAsync("kakaotalk_1", new KakaoTokenData
        {
            AccessToken = "k1",
            RefreshToken = "kr1",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        });

        await _store.RemoveAsync("microsoft_1");

        var ms = await _store.GetAsync<MicrosoftTokenData>("microsoft_1");
        var kakao = await _store.GetAsync<KakaoTokenData>("kakaotalk_1");

        Assert.IsNull(ms);
        Assert.IsNotNull(kakao);
        Assert.AreEqual("k1", kakao.AccessToken);
    }

    [TestMethod]
    public async Task SaveAsync_CreatesTokensFile()
    {
        await _store.SaveAsync("microsoft_1", new MicrosoftTokenData
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        });

        Assert.IsTrue(File.Exists(_store.TokensFilePath));
    }
}
