using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using ClipFlow.Core.Interfaces;
using ClipFlow.Core.Models;
using ClipFlow.Infrastructure.Services;
using ClipFlow.Infrastructure.Storage;
using Xunit;

namespace ClipFlow.Tests;

public class LiveClipboardIntegrationTests
{
    [Fact]
    public async Task FullPipeline_CaptureText_RecognizesAndPersistsToStorage()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"pipeline_{Guid.NewGuid():N}.db");
        var tempSettings = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}.json");

        try
        {
            var settingsService = new SettingsService(tempSettings);
            await settingsService.LoadSettingsAsync();

            var recognizer = new SmartContentRecognizer();
            var security = new SecurityService(settingsService);
            var repo = new SqliteClipboardRepository(tempDb);
            await repo.InitializeAsync();

            // Simulate copied text: GitHub URL
            var copiedUrl = "https://github.com/dotnet/wpf";
            var detected = recognizer.Recognize(copiedUrl, ContentType.Text);

            Assert.Equal(SmartType.Url, detected.DetectedType);
            Assert.Equal("github.com", detected.Domain);

            var item = new ClipboardItem
            {
                ContentType = ContentType.Url,
                TextContent = copiedUrl,
                ContentHash = Core.Utilities.HashUtilities.ComputeSha256(copiedUrl),
                SourceApplication = "chrome.exe",
                SmartDetection = detected
            };

            var added = await repo.AddOrUpdateAsync(item);
            Assert.True(added);

            // Query back
            var results = await repo.GetItemsAsync(new FilterCriteria { ContentType = ContentType.Url });
            Assert.Single(results);
            Assert.Equal("github.com", results[0].SmartDetection.Domain);
            Assert.Equal("chrome.exe", results[0].SourceApplication);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { if (File.Exists(tempDb)) File.Delete(tempDb); } catch { }
            try { if (File.Exists(tempSettings)) File.Delete(tempSettings); } catch { }
        }
    }

    [Fact]
    public void FullPipeline_ColorRecognition_SwatchesStored()
    {
        var recognizer = new SmartContentRecognizer();
        var result = recognizer.Recognize("#10B981", ContentType.Text);

        Assert.Equal(SmartType.HexColor, result.DetectedType);
        Assert.Equal("#10B981", result.ColorHex);
        Assert.Equal("rgb(16, 185, 129)", result.ColorRgb);
    }
}
