using System.Collections.Generic;
using System.IO;
using ClipFlow.Core.Interfaces;
using ClipFlow.Core.Models;
using ClipFlow.Infrastructure.Services;
using Xunit;

namespace ClipFlow.Tests;

public class SecurityAndPrivacyTests
{
    private readonly ISettingsService _settingsService;
    private readonly SecurityService _securityService;

    public SecurityAndPrivacyTests()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_{System.Guid.NewGuid():N}.json");
        _settingsService = new SettingsService(tempFile);
        _securityService = new SecurityService(_settingsService);
    }

    [Theory]
    [InlineData("keepass.exe", true)]
    [InlineData("KeePassXC.exe", true)]
    [InlineData("1password", true)]
    [InlineData("Bitwarden.EXE", true)]
    [InlineData("notepad.exe", false)]
    [InlineData("chrome.exe", false)]
    public void IsApplicationExcluded_ShouldMatchConfiguredApps(string processName, bool expectedExcluded)
    {
        var isExcluded = _securityService.IsApplicationExcluded(processName);
        Assert.Equal(expectedExcluded, isExcluded);
    }

    [Theory]
    [InlineData("Clipboard Viewer Ignore", true)]
    [InlineData("ExcludeClipboardContentFromMonitor", true)]
    [InlineData("CF_TEXT", false)]
    [InlineData("UnicodeText", false)]
    public void ShouldIgnoreClipboardFormats_DetectsPasswordManagerMarkers(string format, bool expectedIgnore)
    {
        var result = _securityService.ShouldIgnoreClipboardFormats(new[] { format });
        Assert.Equal(expectedIgnore, result);
    }

    [Fact]
    public void ContainsSensitiveData_ShouldDetectPrivateKey()
    {
        var key = "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA0m...\n-----END RSA PRIVATE KEY-----";
        var isSensitive = _securityService.ContainsSensitiveData(key, out var reason);

        Assert.True(isSensitive);
        Assert.Contains("Private Key", reason);
    }

    [Fact]
    public void ContainsSensitiveData_ShouldDetectJwtToken()
    {
        var jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        var isSensitive = _securityService.ContainsSensitiveData(jwt, out var reason);

        Assert.True(isSensitive);
        Assert.Contains("JWT", reason);
    }

    [Fact]
    public void ContainsSensitiveData_ShouldDetectApiKey()
    {
        var apiKey = "My secret key is " + "sk_" + "live_51A2B3C4D5E6F7G8H9I0J1K2L3M4N5O";
        var isSensitive = _securityService.ContainsSensitiveData(apiKey, out var reason);

        Assert.True(isSensitive);
        Assert.Contains("API Secret", reason);
    }

    [Fact]
    public void ContainsSensitiveData_ShouldDetectPasswordAssignment()
    {
        var cred = "db_password = SuperSecretPass123!";
        var isSensitive = _securityService.ContainsSensitiveData(cred, out var reason);

        Assert.True(isSensitive);
        Assert.Contains("Password", reason);
    }

    [Fact]
    public void ContainsSensitiveData_ShouldAllowNormalText()
    {
        var text = "Hello world! This is a standard piece of documentation for ClipFlow on Windows 11.";
        var isSensitive = _securityService.ContainsSensitiveData(text, out var reason);

        Assert.False(isSensitive);
        Assert.Null(reason);
    }

    [Fact]
    public void SanitizeForDisplay_ShouldMaskSensitiveTokens()
    {
        var text = "Using token " + "sk_" + "live_123456789012345678901234 for authentication";
        var sanitized = _securityService.SanitizeForDisplay(text);

        Assert.DoesNotContain("123456789012345678901234", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
    }
}
