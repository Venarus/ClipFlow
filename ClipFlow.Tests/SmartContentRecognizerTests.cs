using System.Collections.Generic;
using ClipFlow.Core.Models;
using ClipFlow.Infrastructure.Services;
using Xunit;

namespace ClipFlow.Tests;

public class SmartContentRecognizerTests
{
    private readonly SmartContentRecognizer _recognizer = new();

    [Theory]
    [InlineData("https://github.com/dotnet/runtime", "github.com")]
    [InlineData("http://microsoft.com/windows", "microsoft.com")]
    [InlineData("https://antigravity.google.com/docs/api?v=2#intro", "antigravity.google.com")]
    public void Recognize_ShouldDetectUrlsAndDomain(string input, string expectedDomain)
    {
        var result = _recognizer.Recognize(input, ContentType.Text);

        Assert.Equal(SmartType.Url, result.DetectedType);
        Assert.Equal(expectedDomain, result.Domain);
    }

    [Theory]
    [InlineData("developer@clipflow.local")]
    [InlineData("contact.user+filter@sub.domain.co.uk")]
    public void Recognize_ShouldDetectEmails(string input)
    {
        var result = _recognizer.Recognize(input, ContentType.Text);

        Assert.Equal(SmartType.Email, result.DetectedType);
        Assert.Equal(input, result.DisplayText);
    }

    [Theory]
    [InlineData("#FFF", "#FFFFFF")]
    [InlineData("#3b82f6", "#3B82F6")]
    [InlineData("#107C41FF", "#107C41FF")]
    public void Recognize_ShouldDetectHexColorsAndNormalize(string input, string expectedHex)
    {
        var result = _recognizer.Recognize(input, ContentType.Text);

        Assert.Equal(SmartType.HexColor, result.DetectedType);
        Assert.Equal(expectedHex, result.ColorHex);
        Assert.NotNull(result.ColorRgb);
    }

    [Theory]
    [InlineData("rgb(59, 130, 246)", "#3B82F6")]
    [InlineData("rgba(255, 0, 0, 0.5)", "#FF0000")]
    public void Recognize_ShouldDetectRgbColors(string input, string expectedHexPrefix)
    {
        var result = _recognizer.Recognize(input, ContentType.Text);

        Assert.Equal(SmartType.RgbColor, result.DetectedType);
        Assert.Equal(expectedHexPrefix, result.ColorHex);
    }

    [Theory]
    [InlineData("+1 555-123-4567")]
    [InlineData("(555) 867-5309")]
    public void Recognize_ShouldDetectPhoneNumbers(string input)
    {
        var result = _recognizer.Recognize(input, ContentType.Text);

        Assert.Equal(SmartType.Phone, result.DetectedType);
    }

    [Fact]
    public void Recognize_ShouldDetectJsonCode()
    {
        var json = "{\n  \"appName\": \"ClipFlow\",\n  \"enabled\": true,\n  \"version\": 1.0\n}";
        var result = _recognizer.Recognize(json, ContentType.Text);

        Assert.Equal(SmartType.Code, result.DetectedType);
        Assert.Equal("JSON", result.CodeLanguage);
        Assert.Equal(5, result.LineCount);
    }

    [Fact]
    public void Recognize_ShouldDetectSqlCode()
    {
        var sql = "SELECT Id, ContentType, CreatedAt FROM ClipboardItems WHERE IsFavorite = 1 ORDER BY CreatedAt DESC;";
        var result = _recognizer.Recognize(sql, ContentType.Text);

        Assert.Equal(SmartType.Code, result.DetectedType);
        Assert.Equal("SQL", result.CodeLanguage);
    }

    [Fact]
    public void Recognize_ShouldDetectCSharpCode()
    {
        var csharp = "namespace ClipFlow.Core;\npublic class Sample {\n    public void Test() => Console.WriteLine(\"Hello\");\n}";
        var result = _recognizer.Recognize(csharp, ContentType.Text);

        Assert.Equal(SmartType.Code, result.DetectedType);
        Assert.Equal("C#", result.CodeLanguage);
    }

    [Fact]
    public void Recognize_ShouldCalculateWordAndCharCounts()
    {
        var text = "The quick brown fox jumps over the lazy dog";
        var result = _recognizer.Recognize(text, ContentType.Text);

        Assert.Equal(9, result.WordCount);
        Assert.Equal(text.Length, result.CharCount);
        Assert.Equal(1, result.LineCount);
    }
}
