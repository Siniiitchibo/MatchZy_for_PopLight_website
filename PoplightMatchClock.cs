using System;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace MatchZy
{
    public class PoplightClockEvent : MatchZyEvent
    {
        public PoplightClockEvent(long matchId, string phase, long at) : base("poplight_match_clock")
        {
            MatchId = matchId; Phase = phase; At = at;
        }
        [JsonPropertyName("matchid")] public long MatchId { get; }
        [JsonPropertyName("phase")] public string Phase { get; }
        [JsonPropertyName("at")] public long At { get; }
    }

    public partial class MatchZy
    {
        private long poplightClockMatchId;
        private long poplightClockStartedAt;
        private long poplightClockEndedAt;

        private void StartPoplightMatchClock()
        {
            if (!isMatchSetup || !isMatchLive || liveMatchId <= 0 || seriesEnded) return;
            if (poplightClockMatchId == liveMatchId && poplightClockStartedAt > 0) return;
            poplightClockMatchId = liveMatchId;
            poplightClockStartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            poplightClockEndedAt = 0;
            var report = new PoplightClockEvent(liveMatchId, "start", poplightClockStartedAt);
            RemoteLogTarget target = CurrentRemoteLogTarget();
            _ = Task.Run(async () => await SendEventAsync(report, target));
        }

        private void StopPoplightMatchClock()
        {
            if (poplightClockMatchId != liveMatchId || poplightClockStartedAt <= 0 || poplightClockEndedAt > 0) return;
            poplightClockEndedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var report = new PoplightClockEvent(liveMatchId, "end", poplightClockEndedAt);
            RemoteLogTarget target = CurrentRemoteLogTarget();
            _ = Task.Run(async () => await SendEventAsync(report, target));
        }
    }
}
