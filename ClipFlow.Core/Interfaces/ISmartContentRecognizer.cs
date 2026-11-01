using System.Collections.Generic;
using ClipFlow.Core.Models;

namespace ClipFlow.Core.Interfaces;

public interface ISmartContentRecognizer
{
    SmartDetectionResult Recognize(string? text, ContentType baseType, List<string>? filePaths = null);
}
