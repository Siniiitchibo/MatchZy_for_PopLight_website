using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

public partial class MatchZy
{
    private sealed class PoplightPracticeTeam
    {
        public PoplightPracticeTeam() { }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("players")]
        public Dictionary<string, string> Players { get; set; } = [];
    }

    private sealed class PoplightPracticeConfig
    {
        public PoplightPracticeConfig() { }
        [JsonPropertyName("matchid")]
        public long Id { get; set; }

        [JsonPropertyName("map")]
        public string Map { get; set; } = "";

        [JsonPropertyName("team1")]
        public PoplightPracticeTeam Team1 { get; set; } = new();

        [JsonPropertyName("team2")]
        public PoplightPracticeTeam Team2 { get; set; } = new();
    }

    private PoplightPracticeConfig? poplightPracticeSession;
    private bool poplightPracticeReady;
    private int poplightPracticePreviousAutoStartMode;
    private string? poplightPracticeError;
    private string? poplightPracticeConfigPath;

    private readonly Dictionary<int, (long SessionId, CsTeam Team)>
    poplightPracticeInitialTeams = [];

    private void QueuePoplightPracticeInitialTeam(
        CCSPlayerController player)
    {
        var session = poplightPracticeSession;

        if (session == null ||
            !player.IsValid ||
            player.IsBot ||
            player.IsHLTV ||
            !player.UserId.HasValue)
        {
            return;
        }

        string steamId = player.SteamID.ToString();
        CsTeam team;

        if (session.Team1.Players.ContainsKey(steamId))
            team = CsTeam.CounterTerrorist;
        else if (session.Team2.Players.ContainsKey(steamId))
            team = CsTeam.Terrorist;
        else
            return;

        poplightPracticeInitialTeams[player.UserId.Value] =
            (session.Id, team);

        // If the engine already assigned a side, apply the web side now.
        // Otherwise wait for the player's first team assignment.
        if (player.TeamNum is 2 or 3)
            Server.NextFrame(() =>
                ApplyPoplightPracticeInitialTeam(player));
    }

    private HookResult OnPoplightPracticeInitialTeam(
        EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (@event.Team is 2 or 3 &&
            player != null &&
            player.IsValid &&
            player.UserId.HasValue &&
            poplightPracticeInitialTeams.ContainsKey(player.UserId.Value))
        {
            Server.NextFrame(() =>
                ApplyPoplightPracticeInitialTeam(player));
        }

        return HookResult.Continue;
    }

    private void ApplyPoplightPracticeInitialTeam(
        CCSPlayerController player)
    {
        if (!player.IsValid || !player.UserId.HasValue)
            return;

        int userId = player.UserId.Value;

        if (!poplightPracticeInitialTeams.Remove(userId, out var initial))
            return;

        if (poplightPracticeSession?.Id != initial.SessionId)
            return;

        // The pending assignment is already removed, so later manual
        // changes will not move the player back.
        if (player.TeamNum != (int)initial.Team)
            player.ChangeTeam(initial.Team);
    }

    private void DeletePoplightPracticeConfig()
    {
        string? path = poplightPracticeConfigPath;

        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            File.Delete(path);
            poplightPracticeConfigPath = null;

            Log(
                $"[Poplight practice] Konfigurácia bola odstránená: " +
                Path.GetFileName(path));
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            Log(
                $"[Poplight practice] Konfiguráciu sa nepodarilo vymazať: " +
                $"{Path.GetFileName(path)}. {ex.Message}");
        }
    }

    private void InitializePoplightPractice()
    {
        RegisterEventHandler<EventPlayerTeam>(
            OnPoplightPracticeInitialTeam);

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;

            if (player != null && player.IsValid && player.UserId.HasValue)
                poplightPracticeInitialTeams.Remove(player.UserId.Value);

            return HookResult.Continue;
        });

        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            poplightPracticeInitialTeams.Clear();
            if (poplightPracticeSession == null) return;
            long expected = poplightPracticeSession.Id;
            poplightPracticeReady = false;
            AddTimer(2.0f, () =>
            {
                if (poplightPracticeSession?.Id != expected) return;
                if (isMatchSetup)
                {
                    DeletePoplightPracticeConfig();
                    poplightPracticeSession = null;
                    poplightPracticeReady = false;
                    autoStartMode = poplightPracticePreviousAutoStartMode;
                    return;
                }
                try
                {
                    isSleep = false;
                    if (!isPractice) StartPracticeMode();
                    // Never inherit a password from the previous match.
                    Server.ExecuteCommand("sv_password \"\"");
                    poplightPracticeReady = isPractice;
                }
                catch (Exception ex)
                {
                    poplightPracticeError = ex.Message;
                    Log($"[Poplight practice] Failed to start: {ex.Message}");
                }
            });
        });
    }

    // Called before the normal whitelist. Practice owns its separate roster;
    // the server's existing whitelist.cfg is never overwritten.
    private bool RejectPoplightPracticePlayer(
        CCSPlayerController player, string steamId)
    {
        if (poplightPracticeSession == null || player.IsBot || player.IsHLTV)
            return false;

        if (poplightPracticeSession.Team1.Players.ContainsKey(steamId) ||
            poplightPracticeSession.Team2.Players.ContainsKey(steamId))
            return false;

        KickPlayer(player);
        return true;
    }

    private void ReplyPoplightPracticeStatus(CommandInfo command)
    {
        if (poplightPracticeSession != null &&
            (isMatchSetup || (poplightPracticeReady && !isPractice)))
        {
            DeletePoplightPracticeConfig();
            poplightPracticeSession = null;
            poplightPracticeReady = false;
            autoStartMode = poplightPracticePreviousAutoStartMode;
        }
        var session = poplightPracticeSession;
        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV)
            .ToArray();

        command.ReplyToCommand(JsonSerializer.Serialize(new
        {
            poplight_practice_api = 1,
            active = session != null,
            ready = session != null && poplightPracticeReady && isPractice,
            matchid = session?.Id,
            map = Server.MapName,
            team1_connected = session == null ? 0 : players.Count(p =>
                session.Team1.Players.ContainsKey(p.SteamID.ToString())),
            team2_connected = session == null ? 0 : players.Count(p =>
                session.Team2.Players.ContainsKey(p.SteamID.ToString())),
            error = poplightPracticeError
        }));
    }

    [ConsoleCommand("poplight_practice_status", "Returns managed practice status")]
    public void PoplightPracticeStatus(
        CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return;
        ReplyPoplightPracticeStatus(command);
    }

    [ConsoleCommand("poplight_loadpractice", "Loads a PopLight practice session")]
    public void PoplightLoadPractice(
        CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return;
        if (poplightPracticeSession != null)
        {
            ReplyPoplightPracticeStatus(command);
            return;
        }

        poplightPracticeError = null;
        try
        {
            if (isMatchSetup || matchStarted || isMatchLive)
                throw new InvalidOperationException("A match is already loaded.");

            if (command.ArgCount != 2)
                throw new InvalidOperationException("Expected a practice configuration file.");

            string relativePath = command.ArgByIndex(1);
            if (!PoplightPracticeFileRegex().IsMatch(relativePath))
                throw new InvalidOperationException("Invalid practice configuration path.");

            string path = Path.Combine(
                Server.GameDirectory, "csgo", relativePath);
            var session = JsonSerializer.Deserialize<PoplightPracticeConfig>(
                File.ReadAllText(path))
                ?? throw new InvalidOperationException("Empty practice configuration.");

            if (session.Id < 100000000 || session.Id > 999999999 ||
                session.Team1 == null || session.Team2 == null ||
                session.Team1.Players == null || session.Team2.Players == null)
                throw new InvalidOperationException("Invalid practice configuration.");

            string[] ids =
            [
                .. session.Team1.Players.Keys
                    .Concat(session.Team2.Players.Keys)
            ];
            if (ids.Length == 0 || ids.Distinct().Count() != ids.Length ||
                ids.Any(id => !PoplightPracticeSteamIdRegex().IsMatch(id)))
                throw new InvalidOperationException("Invalid practice roster.");

            bool workshop =
                PoplightPracticeWorkshopRegex().IsMatch(session.Map);
            if (!workshop &&
                (!PoplightPracticeMapRegex().IsMatch(session.Map) ||
                 !Server.IsMapValid(session.Map)))
                throw new InvalidOperationException("Invalid practice map.");

            if (isPractice) ExecUnpracCommands();
            poplightPracticePreviousAutoStartMode = autoStartMode;
            poplightPracticeSession = session;
            poplightPracticeConfigPath = path;
            poplightPracticeReady = false;
            autoStartMode = 2;
            isPractice = false;
            isSleep = false;
            isWarmup = false;
            readyAvailable = false;

            foreach (var connected in Utilities.GetPlayers())
            {
                if (connected.IsValid)
                    RejectPoplightPracticePlayer(
                        connected, connected.SteamID.ToString());
            }

            // The existing map-start handler uses autoStartMode 2. The listener
            // above confirms practice after the selected map has loaded.
            Server.ExecuteCommand("game_type 0;game_mode 1;sv_password \"\"");
            Server.ExecuteCommand(workshop
                ? $"host_workshop_map {session.Map}"
                : $"changelevel {session.Map}");
        }
        catch (Exception ex)
        {
            if (poplightPracticeSession != null)
            {
                autoStartMode = poplightPracticePreviousAutoStartMode;
                DeletePoplightPracticeConfig();
                poplightPracticeSession = null;
                poplightPracticeReady = false;
                ResetMatch();
            }
            poplightPracticeError = ex.Message;
            Log($"[Poplight practice] Request rejected: {ex.Message}");
        }
        ReplyPoplightPracticeStatus(command);
    }

    [ConsoleCommand("poplight_endpractice", "Ends a PopLight practice session")]
    public void PoplightEndPractice(
        CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return;
        if (command.ArgCount != 2 ||
            !long.TryParse(command.ArgByIndex(1), out long expected) ||
            (poplightPracticeSession != null &&
             poplightPracticeSession.Id != expected))
        {
            poplightPracticeError = "The practice session has changed.";
            ReplyPoplightPracticeStatus(command);
            return;
        }
        if (poplightPracticeSession != null)
        {
            DeletePoplightPracticeConfig();
            poplightPracticeSession = null;
            poplightPracticeReady = false;
            poplightPracticeError = null;
            autoStartMode = poplightPracticePreviousAutoStartMode;
            foreach (var connected in Utilities.GetPlayers())
            {
                if (connected.IsValid && !connected.IsBot && !connected.IsHLTV)
                    KickPlayer(connected);
            }
            ExecUnpracCommands();
            ResetMatch();
            RemoveSpawnBeams();
        }
        ReplyPoplightPracticeStatus(command);
    }
}