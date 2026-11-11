using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThrashFighters.Sim;

public enum MoveId
{
    Jab, Tilt, UpTilt, DownTilt, Dash, Smash,
    NeutralAir, ForwardAir, BackAir, UpAir, DownAir,
    Grab, Special, Recovery
}

public sealed record MoveSpec(
    string Name, int Startup, int Active, int Recovery, int Landing,
    int OffsetX, int OffsetY, int Radius, int Damage, int Angle, int Base, int Growth)
{
    public int Duration => Startup + Active + Recovery;
}

// Velocity and acceleration are in subpixels per simulation tick.
public sealed record FighterSpec(
    string Name, int Weight, int Height, int Width, int JumpSquat,
    int Walk, int Dash, int Run, int RunAcceleration, int Traction,
    int ShortHop, int FullHop, int DoubleJump, int Gravity, int FallSpeed,
    int FastFall, int AirSpeed, int AirAcceleration, int AirDodge, int RecoveryRise,
    Dictionary<MoveId, MoveSpec> Moves);

public sealed record SurfaceSpec(int Left, int Right, int Y, bool PassThrough);

public sealed record StageSpec(
    int SpawnLeft, int SpawnRight, int BlastLeft, int BlastRight,
    int BlastTop, int BlastBottom, SurfaceSpec[] Surfaces);

public sealed record GameData(FighterSpec Kitsune, FighterSpec Oni, StageSpec Shrine, StageSpec Dojo)
{
    public static GameData Parse(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var data = JsonSerializer.Deserialize<GameData>(json, options)
            ?? throw new InvalidDataException("Missing fighter and stage data.");

        foreach (FighterSpec fighter in new[] { data.Kitsune, data.Oni })
        {
            if (fighter is null || fighter.Weight <= 0 || fighter.Width <= 0 || fighter.Height <= 0
                || fighter.JumpSquat <= 0 || fighter.Traction <= 0 || fighter.Gravity <= 0
                || fighter.AirDodge <= 0 || fighter.RecoveryRise <= 0 || fighter.Moves is null)
            {
                throw new InvalidDataException("Invalid fighter profile.");
            }

            foreach (MoveId id in Enum.GetValues<MoveId>())
            {
                if (!fighter.Moves.TryGetValue(id, out MoveSpec? move) || move.Startup < 0
                    || move.Active <= 0 || move.Recovery < 0 || move.Landing < 0 || move.Radius <= 0)
                {
                    throw new InvalidDataException($"Invalid {fighter.Name} move {id}.");
                }
            }
        }

        foreach (StageSpec stage in new[] { data.Shrine, data.Dojo })
        {
            if (stage?.Surfaces is not { Length: > 0 } || stage.Surfaces[0].PassThrough
                || stage.Surfaces.Any(surface => surface.Left >= surface.Right)
                || stage.SpawnLeft < stage.Surfaces[0].Left || stage.SpawnRight > stage.Surfaces[0].Right)
            {
                throw new InvalidDataException("Invalid stage geometry.");
            }
        }

        return data;
    }
}
