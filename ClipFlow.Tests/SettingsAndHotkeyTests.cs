using System.IO;
using System.Threading.Tasks;
using ClipFlow.Infrastructure.Services;
using Xunit;

namespace ClipFlow.Tests;

public class SettingsAndHotkeyTests
{
    [Fact]
    public async Task SettingsService_SaveAndReload_ShouldPreserveValues()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_{System.Guid.NewGuid():N}.json");
        try
        {
            var service = new SettingsService(tempFile);
            await service.LoadSettingsAsync();

            service.Settings.Theme = "Light";
            service.Settings.BackdropMaterial = "Mica";
            service.Settings.BackgroundOpacity = 0.75;
            service.Settings.MaxHistoryItems = 2500;
            service.Settings.StartWithWindows = true;

            await service.SaveSettingsAsync();

            // Load anew from disk
            var service2 = new SettingsService(tempFile);
            await service2.LoadSettingsAsync();

            Assert.Equal("Light", service2.Settings.Theme);
            Assert.Equal("Mica", service2.Settings.BackdropMaterial);
            Assert.Equal(0.75, service2.Settings.BackgroundOpacity);
            Assert.Equal(2500, service2.Settings.MaxHistoryItems);
            Assert.True(service2.Settings.StartWithWindows);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("Win+Alt+V", 0x0001 | 0x0008, (uint)'V')]
    [InlineData("Ctrl+Shift+V", 0x0002 | 0x0004, (uint)'V')]
    [InlineData("Win+Alt+F", 0x0001 | 0x0008, (uint)'F')]
    [InlineData("Ctrl+Alt+C", 0x0001 | 0x0002, (uint)'C')]
    [InlineData("F9", 0, 0x78)]
    public void HotkeyService_TryParseHotkey_ValidStrings(string input, uint expectedMods, uint expectedVk)
    {
        var success = HotkeyService.TryParseHotkey(input, out var mods, out var vk);

        Assert.True(success);
        Assert.Equal(expectedMods, mods);
        Assert.Equal(expectedVk, vk);
    }
}
