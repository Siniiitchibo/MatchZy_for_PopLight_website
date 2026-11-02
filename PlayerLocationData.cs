using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

public class PlayerLocationData(Vector position, QAngle angle)
{
    public Vector Position { get; set; } = position;
    public QAngle Angle { get; set; } = angle;

    public void LoadPosition(CCSPlayerController player)
    {
        PlayerTeleport.TeleportUpright(player, Position, Angle);
    }
}