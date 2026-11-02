using System.Text.Json;

namespace MatchZy;

internal static class MatchZyJsonOptions
{
    internal static readonly JsonSerializerOptions AllowTrailingCommas = new() { AllowTrailingCommas = true };
    internal static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
}
