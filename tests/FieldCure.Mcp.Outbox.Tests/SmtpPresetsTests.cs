using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Tests;

/// <summary>
/// Tests for <see cref="SmtpPresets"/> provider lookup behavior.
/// </summary>
[TestClass]
public class SmtpPresetsTests
{
    [TestMethod]
    public void Get_Gmail_ReturnsCorrectSettings()
    {
        var preset = SmtpPresets.Get("gmail");
        Assert.IsNotNull(preset);
        Assert.AreEqual("smtp.gmail.com", preset.Host);
        Assert.AreEqual(587, preset.Port);
    }

    [TestMethod]
    public void Get_Outlook_ReturnsCorrectSettings()
    {
        var preset = SmtpPresets.Get("outlook");
        Assert.IsNotNull(preset);
        Assert.AreEqual("smtp-mail.outlook.com", preset.Host);
        Assert.AreEqual(587, preset.Port);
    }

    [TestMethod]
    public void Get_Microsoft365_ReturnsCorrectSettings()
    {
        var preset = SmtpPresets.Get("microsoft365");
        Assert.IsNotNull(preset);
        Assert.AreEqual("smtp.office365.com", preset.Host);
        Assert.AreEqual(587, preset.Port);
    }

    [TestMethod]
    public void Get_Naver_ReturnsCorrectSettings()
    {
        var preset = SmtpPresets.Get("naver");
        Assert.IsNotNull(preset);
        Assert.AreEqual("smtp.naver.com", preset.Host);
        Assert.AreEqual(587, preset.Port);
    }

    [TestMethod]
    public void Get_Unknown_ReturnsNull()
    {
        var preset = SmtpPresets.Get("unknown");
        Assert.IsNull(preset);
    }

    [TestMethod]
    public void Get_IsCaseInsensitive()
    {
        var preset = SmtpPresets.Get("Gmail");
        Assert.IsNotNull(preset);
        Assert.AreEqual("smtp.gmail.com", preset.Host);
    }

    [TestMethod]
    public void Names_ContainsAllPresets()
    {
        var names = SmtpPresets.Names;
        Assert.AreEqual(4, names.Count);
        Assert.IsTrue(names.Contains("gmail"));
        Assert.IsTrue(names.Contains("outlook"));
        Assert.IsTrue(names.Contains("microsoft365"));
        Assert.IsTrue(names.Contains("naver"));
    }
}
