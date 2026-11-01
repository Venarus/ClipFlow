using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClipFlow.Core.Interfaces;
using ClipFlow.Core.Models;

namespace ClipFlow.Infrastructure.Services;

public partial class SecurityService : ISecurityService
{
    private readonly ISettingsService _settingsService;

    // Password manager custom clipboard format names
    private static readonly HashSet<string> IgnoredFormatNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Clipboard Viewer Ignore",
        "ExcludeClipboardContentFromMonitor",
        "CanIncludeInClipboardHistory",
        "CanUploadToCloudClipboard"
    };

    // Sensitive patterns
    private static readonly Regex PrivateKeyRegex = new(
        @"-----BEGIN [A-Z0-9\s_-]+PRIVATE KEY-----", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex JwtRegex = new(
        @"\beyJ[a-zA-Z0-9_-]{10,}\.eyJ[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}\b", 
        RegexOptions.Compiled);

    private static readonly Regex ApiKeyRegex = new(
        @"\b(?:sk_live_[0-9a-zA-Z]{24,}|ghp_[0-9a-zA-Z]{36}|gho_[0-9a-zA-Z]{36}|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z\-_]{35}|xox[baprs]-[0-9a-zA-Z]{10,})\b", 
        RegexOptions.Compiled);

    private static readonly Regex CredentialLineRegex = new(
        @"(?:password|passwd|pwd|secret|api_key|apikey|access_token|bearer)\s*[:=]\s*[""']?([^\s""']{4,})[""']?", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CreditCardCandidateRegex = new(
        @"\b(?:\d{4}[- ]?){3}\d{4}\b|\b\d{15,16}\b", 
        RegexOptions.Compiled);

    public SecurityService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool IsApplicationExcluded(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        var clean = processName.Trim();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(0, clean.Length - 4);
        }

        var excludedList = _settingsService.Settings.ExcludedApplications;
        foreach (var excluded in excludedList)
        {
            var target = excluded.Trim();
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                target = target.Substring(0, target.Length - 4);
            }

            if (string.Equals(clean, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public bool ShouldIgnoreClipboardFormats(IEnumerable<string> availableFormats)
    {
        if (!_settingsService.Settings.IgnorePasswordManagerFormats) return false;

        foreach (var format in availableFormats)
        {
            if (IgnoredFormatNames.Contains(format))
            {
                return true;
            }
        }
        return false;
    }

    public bool ContainsSensitiveData(string? text, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!_settingsService.Settings.FilterSensitiveContent) return false;

        // Check for Private Key
        if (PrivateKeyRegex.IsMatch(text))
        {
            reason = "Private Key detected";
            return true;
        }

        // Check for JWT Token
        if (JwtRegex.IsMatch(text))
        {
            reason = "JWT Authentication Token detected";
            return true;
        }

        // Check for known API Keys
        if (ApiKeyRegex.IsMatch(text))
        {
            reason = "API Secret / Access Token detected";
            return true;
        }

        // Check for explicit credential patterns
        if (CredentialLineRegex.IsMatch(text))
        {
            reason = "Password or secret credential assignment detected";
            return true;
        }

        // Check for Credit Card Number with Luhn validation
        var cardMatches = CreditCardCandidateRegex.Matches(text);
        foreach (Match m in cardMatches)
        {
            var digits = new string(m.Value.Where(char.IsDigit).ToArray());
            if (digits.Length >= 13 && digits.Length <= 19 && IsValidLuhn(digits))
            {
                reason = "Credit Card Number detected";
                return true;
            }
        }

        return false;
    }

    public string SanitizeForDisplay(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Mask credit cards
        text = CreditCardCandidateRegex.Replace(text, m =>
        {
            var digits = new string(m.Value.Where(char.IsDigit).ToArray());
            if (digits.Length >= 13 && digits.Length <= 19 && IsValidLuhn(digits))
            {
                return $"{digits.Substring(0, 4)} **** **** {digits.Substring(digits.Length - 4)}";
            }
            return m.Value;
        });

        // Mask API keys
        text = ApiKeyRegex.Replace(text, m => $"{m.Value.Substring(0, 7)}...[REDACTED]");

        return text;
    }

    private static bool IsValidLuhn(string digits)
    {
        int sum = 0;
        bool alternate = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }
}
