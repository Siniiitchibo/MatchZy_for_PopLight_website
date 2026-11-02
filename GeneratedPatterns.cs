using System.Text.RegularExpressions;

namespace MatchZy;

public partial class MatchZy
{
    [GeneratedRegex(@"ServerVersion=(\d+)")]
    private static partial Regex ServerVersionRegex();

    [GeneratedRegex(@"[^\p{L}0-9 _-]")]
    private static partial Regex PlayerNameRegex();
}
