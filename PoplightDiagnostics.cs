using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MatchZy
{
    public partial class MatchZy
    {
        private readonly string poplightBootId =
            Guid.NewGuid().ToString("N");

        private long poplightLastEndedMatchId = 0;

        private readonly HashSet<string> poplightReportedProblems = new();
        private readonly object poplightProblemLock = new();

        private void ReportPoplightProblem(
            string code,
            long matchId,
            string message,
            RemoteLogTarget target)
        {
            if (matchId <= 0 || string.IsNullOrEmpty(target.Url))
                return;

            string key = matchId + ":" + code;

            lock (poplightProblemLock)
            {
                if (poplightReportedProblems.Count > 2000)
                    poplightReportedProblems.Clear();

                if (!poplightReportedProblems.Add(key))
                    return;
            }

            _ = Task.Run(async () =>
            {
                await SendEventAsync(
                    new PoplightErrorEvent(message, code, matchId),
                    target
                );
            });
        }
    }
}