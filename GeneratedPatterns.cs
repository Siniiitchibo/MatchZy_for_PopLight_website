using System.Text.RegularExpressions;

namespace MatchZy;

public partial class MatchZy
{
    [GeneratedRegex(@"ServerVersion=(\d+)")]
    private static partial Regex ServerVersionRegex();

    [GeneratedRegex(@"[^\p{L}0-9 _-]")]
    private static partial Regex PlayerNameRegex();

    [GeneratedRegex(
        @"\Apoplight_matches/match_[0-9]{9}_[0-9a-f]{8}\.json\z")]
    private static partial Regex PoplightPracticeFileRegex();

    [GeneratedRegex(@"\A7656[0-9]{13}\z")]
    private static partial Regex PoplightPracticeSteamIdRegex();

    [GeneratedRegex(@"\A[0-9]{6,20}\z")]
    private static partial Regex PoplightPracticeWorkshopRegex();

    [GeneratedRegex(@"\A[a-zA-Z0-9_]+\z")]
    private static partial Regex PoplightPracticeMapRegex();
}