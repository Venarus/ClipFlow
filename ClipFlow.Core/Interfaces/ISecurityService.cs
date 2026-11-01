using System;
using System.Collections.Generic;

namespace ClipFlow.Core.Interfaces;

public interface ISecurityService
{
    bool IsApplicationExcluded(string? processName);
    bool ContainsSensitiveData(string? text, out string? reason);
    bool ShouldIgnoreClipboardFormats(IEnumerable<string> availableFormats);
    string SanitizeForDisplay(string text);
}
