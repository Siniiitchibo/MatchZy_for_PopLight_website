using System.Diagnostics;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace MatchZy
{
    public partial class MatchZy
    {
        public FakeConVar<bool> warmupWelcomeMessageEnabled = new(
            "matchzy_warmup_welcome_message_enabled",
            "Enable the repeating warmup welcome message. Default: false",
            false);

        public FakeConVar<int> warmupWelcomeMessageInterval = new(
            "matchzy_warmup_welcome_message_interval",
            "Warmup welcome message interval in seconds. Minimum: 1. Default: 60",
            60);

        public FakeConVar<string> warmupWelcomeMessage = new(
            "matchzy_warmup_welcome_message",
            "Custom warmup welcome message. Supports colors, variables and $$$ for multiple lines.",
            "");

        private readonly Dictionary<int, long> warmupWelcomeMessageLastSent = [];

        private bool CanSendWarmupWelcomeMessage()
        {
            return warmupWelcomeMessageEnabled.Value &&
                isWarmup &&
                readyAvailable &&
                !matchStarted &&
                !isPractice &&
                !isSleep &&
                !isKnifeRound &&
                !isSideSelectionPhase &&
                !isMatchLive &&
                !string.IsNullOrWhiteSpace(warmupWelcomeMessage.Value) &&
                warmupWelcomeMessage.Value.Trim() != "\"\"";
        }

        private void SendWarmupWelcomeMessageToPlayer(
            CCSPlayerController player)
        {
            if (!CanSendWarmupWelcomeMessage() ||
                !player.IsValid ||
                player.IsBot ||
                player.IsHLTV ||
                !player.UserId.HasValue)
            {
                return;
            }

            foreach (string line in warmupWelcomeMessage.Value.Split("$$$"))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string formattedMessage =
                    GetColorTreatedString(FormatCvarValue(line.Trim()));

                string personalMessage = formattedMessage.Replace(
                    "{PLAYER_NAME}",
                    player.PlayerName);

                PrintToPlayerChat(player, personalMessage);
            }

            warmupWelcomeMessageLastSent[player.UserId.Value] =
                Stopwatch.GetTimestamp();
        }

        private void CheckWarmupWelcomeMessage()
        {
            if (!CanSendWarmupWelcomeMessage())
            {
                warmupWelcomeMessageLastSent.Clear();
                return;
            }

            // Remove records of disconnected players.
            foreach (int userId in warmupWelcomeMessageLastSent.Keys
                .Where(id => !playerData.ContainsKey(id))
                .ToArray())
            {
                warmupWelcomeMessageLastSent.Remove(userId);
            }

            int interval = Math.Max(1, warmupWelcomeMessageInterval.Value);

            foreach (var player in playerData.Values.ToArray())
            {
                if (!player.IsValid ||
                    player.IsBot ||
                    player.IsHLTV ||
                    !player.UserId.HasValue ||
                    (player.TeamNum != 2 && player.TeamNum != 3))
                {
                    continue;
                }

                int userId = player.UserId.Value;

                if (warmupWelcomeMessageLastSent.TryGetValue(
                    userId, out long lastSent))
                {
                    double elapsedSeconds =
                        Stopwatch.GetElapsedTime(lastSent).TotalSeconds;

                    if (elapsedSeconds < interval)
                        continue;
                }

                SendWarmupWelcomeMessageToPlayer(player);
            }
        }
    }
}