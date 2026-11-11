using System;
using System.Collections.Generic;
using Godot;
using ThrashFighters.Sim;

namespace ThrashFighters;

public partial class ArenaView : Node2D
{
    public static readonly Color Paper = new("f9ead1");
    public static readonly Color Gold = new("ffc779");
    private static readonly Color Vermilion = new("ff8777");
    private static readonly Color Ink = new("10162a");
    private static readonly Color Muted = new("b4c1cd");
    private const float Unit = MatchSimulation.Unit;

    public StageSpec TitleStage { get; set; } = null!;
    public MatchSimulation? Match { get; set; }
    public int Countdown { get; set; }
    public bool DebugBoxes { get; set; }
    public bool Frozen { get; set; }
    public bool Cpu { get; set; }
    public string PracticeText { get; set; } = "";

    private readonly List<Burst> _effects = new(16);
    private Font _font = null!;
    private Texture2D[][] _sprites = null!;

    public override void _Ready()
    {
        _font = ThemeDB.FallbackFont;
        _sprites = new[] { SpriteFrames(Fox, FoxColors), SpriteFrames(Oni, OniColors) };
    }

    public void AddEffect(MatchEvent effect)
    {
        if (_effects.Count == 32)
        {
            _effects.RemoveAt(0);
        }
        _effects.Add(new Burst(effect, 0));
    }

    public void ResetEffects() => _effects.Clear();

    public void AdvanceEffects()
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            Burst burst = _effects[i];
            if (burst.Age >= 21)
            {
                _effects.RemoveAt(i);
            }
            else
            {
                _effects[i] = burst with { Age = burst.Age + 1 };
            }
        }
    }

    public override void _Draw()
    {
        Backdrop();
        if (_podium is int winner && Match is { } finished)
        {
            DrawPodium(finished, winner);
            return;
        }
        if (Match is not { } match)
        {
            Stage(TitleStage);
            Box(0, 0, 274, 360, new Color("111a2b"));
            Box(274, 0, 1, 360, new Color("425067"));
            Sprite(0, new Vector2(379, 258), 1, 66, 58, 0, Colors.White);
            Sprite(1, new Vector2(511, 258), -1, 83, 71, 0, Colors.White);
            Caption("KITSUNE", 379, 314, 13, Gold);
            Caption("ONI", 511, 314, 13, Vermilion);
            return;
        }

        float zoom = WorldZoom(match);
        var origin = new Vector2(320, 172);
        DrawSetTransform(origin * (1f - zoom), 0, new Vector2(zoom, zoom));
        Stage(match.Stage);
        for (int player = 0; player < 2; player++)
        {
            DrawFighter(match, player);
        }
        foreach (Burst effect in _effects)
        {
            DrawBurst(effect);
        }
        DrawSetTransform(Vector2.Zero);
        Hud(match);
        if (Countdown > 0)
        {
            Box(287, 109, 66, 65, new Color("1a2239"));
            Box(287, 172, 66, 2, Gold);
            Caption(((Countdown + 43) / 44).ToString(), 320, 158, 40, Paper);
        }
    }

    private void Backdrop()
    {
        var zenith = new Color("121d37");
        var horizon = new Color("695066");
        for (int y = 0; y < 360; y += 12)
        {
            Box(0, y, 640, 12, zenith.Lerp(horizon, y / 410f));
        }
        for (int i = 0; i < 36; i++)
        {
            int x = (i * 109 + 29) % 627;
            int y = 9 + (i * 73 + 11) % 142;
            Box(x, y, i % 7 == 0 ? 3 : 1, i % 5 == 0 ? 2 : 1, i % 3 == 0 ? new Color("dccdbd") : new Color("858ca5"));
        }

        Disc(469, 104, 49, new Color("41465e"));
        Disc(469, 104, 39, new Color("dfb699"));
        Disc(466, 100, 35, new Color("f3d6ae"));
        Box(435, 115, 65, 3, new Color("dfba9d"));
        Box(446, 123, 45, 2, new Color("dfba9d"));
        Box(414, 135, 96, 4, new Color("6d6179"));
        Box(445, 139, 107, 3, new Color("6d6179"));
        Box(77, 96, 102, 2, new Color("53516b"));
        Box(84, 99, 132, 3, new Color("53516b"));

        Ridge(188, 47, new Color("50445c"), 63);
        Ridge(226, 59, new Color("343950"), 83);
        Ridge(264, 35, new Color("263149"), 71);
        Box(0, 264, 640, 96, new Color("202b3e"));
        for (int i = 0; i < 24; i++)
        {
            int x = (i * 89 + 5) % 657 - 12;
            int y = 277 + (i * 19) % 73;
            Box(x, y, 14 + i % 5 * 8, 1, new Color("35435a"));
        }

        Torii(320, 238, 0.8f, new Color("34324b"));
        Torii(561, 255, 0.42f, new Color("35344a"));
    }

    private void Ridge(int baseline, int height, Color color, int step)
    {
        for (int x = -step, i = 0; x < 640; x += step, i++)
        {
            int peak = baseline - height - (i * 23 % 45);
            DrawColoredPolygon(new[]
            {
                new Vector2(x, baseline + 27), new Vector2(x + step * 0.75f, peak),
                new Vector2(x + step * 1.58f, baseline + 27)
            }, color);
        }
    }

    private void Torii(int center, int floor, float scale, Color color)
    {
        Box(center - 31 * scale, floor - 85 * scale, 8 * scale, 85 * scale, color);
        Box(center + 23 * scale, floor - 85 * scale, 8 * scale, 85 * scale, color);
        Box(center - 51 * scale, floor - 88 * scale, 102 * scale, 8 * scale, color);
        Box(center - 42 * scale, floor - 67 * scale, 84 * scale, 6 * scale, color);
        Box(center - 5 * scale, floor - 81 * scale, 10 * scale, 16 * scale, color);
        Box(center - 56 * scale, floor - 94 * scale, 13 * scale, 5 * scale, color);
        Box(center + 43 * scale, floor - 94 * scale, 13 * scale, 5 * scale, color);
    }

    private void Stage(StageSpec stage)
    {
        for (int i = 0; i < stage.Surfaces.Length; i++)
        {
            SurfaceSpec surface = stage.Surfaces[i];
            int width = surface.Right - surface.Left;
            int thickness = i == 0 ? 15 : 8;
            Box(surface.Left, surface.Y, width, thickness, new Color("443246"));
            Box(surface.Left, surface.Y, width, 3, i == 0 ? new Color("e0ad85") : new Color("c7a38f"));
            Box(surface.Left, surface.Y + 4, width, 2, new Color("806078"));
            for (int x = surface.Left + 8; x < surface.Right - 7; x += 16)
            {
                Box(x, surface.Y + 5, 2, thickness - 5, new Color("5d4359"));
            }
            if (i == 0)
            {
                Box(surface.Left + 16, surface.Y + thickness, width - 32, 13, new Color("283044"));
                Box(surface.Left + 34, surface.Y + thickness + 13, width - 68, 8, new Color("273346"));
                Lantern(surface.Left + 23, surface.Y + 19);
                Lantern(surface.Right - 23, surface.Y + 19);
            }
        }
    }

    private void Lantern(int x, int y)
    {
        Box(x - 1, y - 4, 2, 6, new Color("88646a"));
        Box(x - 5, y + 2, 10, 12, new Color("6c4354"));
        Box(x - 3, y + 4, 6, 8, new Color("dfa56f"));
        Box(x - 1, y + 5, 2, 5, new Color("ffe1a4"));
    }

    private void DrawFighter(MatchSimulation match, int player)
    {
        FighterState fighter = match.Fighters[player];
        if (fighter.Action is ActionState.Out or ActionState.Respawn)
        {
            return;
        }

        var feet = new Vector2(Mathf.Round(fighter.X / Unit), Mathf.Round(fighter.Y / Unit));
        Color accent = player == 0 ? Gold : Vermilion;
        int frame = fighter.Grounded && Math.Abs(fighter.VelocityX) > 750 ? match.Tick / 5 % 2 : 0;
        float squat = fighter.Action is ActionState.JumpSquat or ActionState.Tech ? 0.76f : 1;
        int sprite = fighter.Spec.Name == "Oni" ? 1 : 0;
        float width = fighter.Spec.Width + (sprite == 0 ? 13 : 6);
        bool mirrorMatch = match.Fighters[0].Spec.Name == match.Fighters[1].Spec.Name;
        Color tint = fighter.Hitlag > 0 ? new Color("ffe6c6")
            : fighter.Invulnerability > 0 && match.Tick % 6 < 3 ? new Color(1, 1, 1, 0.54f)
            : mirrorMatch && player == 1 ? new Color("9ecbff") : Colors.White;

        if (fighter.Grounded)
        {
            Box(feet.X - 13, feet.Y - 2, 26, 3, new Color(0.09f, 0.08f, 0.13f, 0.5f));
        }
        if (fighter.Action == ActionState.AirDodge)
        {
            Box(feet.X - 14 - fighter.Facing * 6, feet.Y - 17, 29, 5, new Color(accent, 0.27f));
            Box(feet.X - 12 - fighter.Facing * 4, feet.Y - 7, 24, 2, new Color(accent, 0.45f));
        }
        Sprite(sprite, feet, fighter.Facing, width, (fighter.Spec.Height + 3) * squat, frame, tint);
        Caption(player == 0 ? "P1" : Cpu ? "CPU" : "P2", feet.X, feet.Y - fighter.Spec.Height - 12, 10, accent);

        HitCircle? hit = match.ActiveHitbox(fighter);
        if (hit is HitCircle attack)
        {
            AttackEffect(fighter, attack, accent);
        }
        else if (fighter.Action == ActionState.Attack && fighter.Move == MoveId.Smash
            && fighter.Age < fighter.Spec.Moves[fighter.Move].Startup)
        {
            Box(feet.X + fighter.Facing * 13 - 2, feet.Y - 18, 4, 4, Gold);
        }
        if (fighter.Action is ActionState.Shield or ActionState.ShieldStun)
        {
            HitCircle shield = match.Shieldbox(fighter);
            var center = new Vector2(shield.X / Unit, shield.Y / Unit);
            float radius = shield.Radius / Unit;
            DrawCircle(center, radius, new Color(accent, 0.17f));
            DrawArc(center, radius, 0, Mathf.Tau, 24, new Color(accent, 0.82f), 1.5f);
        }
        if (DebugBoxes)
        {
            DrawRect(new Rect2(feet.X - fighter.Spec.Width / 2f, feet.Y - fighter.Spec.Height,
                fighter.Spec.Width, fighter.Spec.Height), new Color("ffd764"), false, 1);
            CircleOutline(match.Hurtbox(fighter), new Color("79e4ac"));
            if (hit is HitCircle box)
            {
                CircleOutline(box, new Color("ff4c64"));
            }
        }
    }

    private void AttackEffect(FighterState fighter, HitCircle hit, Color accent)
    {
        var center = new Vector2(hit.X / Unit, hit.Y / Unit);
        float radius = hit.Radius / Unit;
        if (fighter.Move is MoveId.Special or MoveId.Recovery or MoveId.NeutralAir)
        {
            DrawArc(center, radius * 0.83f, fighter.Age * 0.2f, fighter.Age * 0.2f + 4.8f, 13,
                new Color(accent, 0.7f), 3);
        }
        else
        {
            float angle = fighter.Move switch
            {
                MoveId.UpAir or MoveId.UpTilt => -Mathf.Pi / 2,
                MoveId.DownAir => Mathf.Pi / 2,
                MoveId.BackAir => fighter.Facing > 0 ? Mathf.Pi : 0,
                _ => fighter.Facing > 0 ? 0 : Mathf.Pi
            };
            DrawArc(center, radius * 0.74f, angle - 1.12f, angle + 1.12f, 10, new Color(accent, 0.83f), 4);
            DrawArc(center, radius * 0.89f, angle - 0.62f, angle + 0.69f, 8, Paper, 1);
        }
    }

    private void CircleOutline(HitCircle circle, Color color) => DrawArc(
        new Vector2(circle.X / Unit, circle.Y / Unit), circle.Radius / Unit, 0, Mathf.Tau, 20, color, 1);

    private void DrawBurst(Burst burst)
    {
        EventKind kind = burst.Event.Kind;
        if (kind == EventKind.Jump)
        {
            return;
        }
        float x = burst.Event.X / Unit;
        float y = burst.Event.Y / Unit - (kind is EventKind.Hit or EventKind.Block or EventKind.Grab ? 18 : 1);
        if (kind == EventKind.Knockout)
        {
            x = Mathf.Clamp(x, 41, 599);
            y = Mathf.Clamp(y, 69, 321);
        }
        Color color = kind switch
        {
            EventKind.Cancel => new Color("86f1be"),
            EventKind.Block => new Color("8dcfff"),
            EventKind.Hit => new Color("ffe4a5"),
            EventKind.Knockout => Vermilion,
            _ => new Color("e9c5a9")
        };
        float speed = kind is EventKind.Hit or EventKind.Knockout ? 1.9f : 1f;
        int count = kind == EventKind.Land ? 5 : 8;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.Tau / count;
            float radius = 4 + burst.Age * speed;
            float px = Mathf.Round(x + Mathf.Cos(angle) * radius);
            float py = Mathf.Round(y + Mathf.Sin(angle) * radius * (kind == EventKind.Land ? 0.22f : 0.72f));
            float size = burst.Age < 7 ? 4 : 2;
            Box(px, py, size, size, new Color(color, 1f - burst.Age / 23f));
        }
        if (kind == EventKind.Cancel && burst.Age < 19)
        {
            Caption("L-CANCEL", x, y - 42 - burst.Age * 0.5f, 12, color);
        }
    }

    private void Hud(MatchSimulation match)
    {
        FighterPlate(match.Fighters[0], 17, "P1", Gold, match.Training);
        FighterPlate(match.Fighters[1], 474, Cpu ? "CPU" : "P2", Vermilion, match.Training);
        Box(272, 13, 96, 43, new Color(Ink, 0.90f));
        if (match.Training)
        {
            Caption(Frozen ? "FRAME STEP" : "TRAINING", 320, 31, 13, Gold);
            Caption($"TICK {match.Tick}", 320, 47, 10, Muted);
            TrainingHud(match.Fighters[0]);
        }
        else
        {
            int seconds = (match.TimeLeft + 59) / 60;
            Caption($"{seconds / 60}:{seconds % 60:00}", 320, 41, 22, Paper);
            Caption("ESC / START  PAUSE", 320, 344, 10, Muted);
        }
    }

    private void FighterPlate(FighterState fighter, int x, string tag, Color color, bool training)
    {
        Box(x, 13, 149, 53, new Color(Ink, 0.92f));
        Box(x, 13, 2, 53, color);
        Text($"{tag}   {fighter.Spec.Name.ToUpperInvariant()}", x + 9, 30, 11, color);
        Text($"{fighter.Percent}%", x + 9, 56, 23, Paper);
        if (!training)
        {
            for (int stock = 0; stock < MatchSimulation.StartingStocks; stock++)
            {
                int px = x + 87 + stock * 13;
                Box(px + 2, 47, 4, 8, stock < fighter.Stocks ? color : new Color("4d5366"));
                Box(px, 49, 8, 4, stock < fighter.Stocks ? color : new Color("4d5366"));
            }
        }
        else
        {
            Box(x + 84, 51, 53, 3, new Color("495165"));
            Box(x + 84, 51, 53 * fighter.Shield / 600f, 3, color);
        }
    }

    private void TrainingHud(FighterState fighter)
    {
        Box(16, 297, 608, 58, new Color(Ink, 0.95f));
        string action = fighter.Action == ActionState.Attack ? fighter.Spec.Moves[fighter.Move].Name : fighter.Action.ToString();
        string state = $"{action} {fighter.Age}   |   VX {fighter.VelocityX / Unit:0.0}  VY {fighter.VelocityY / Unit:0.0}";
        Text(state, 24, 312, 11, Paper);
        Text($"{PracticeText}   ·   {LandingLabel(fighter.LastLanding)}", 321, 312, 11, Gold);
        Text("F1 Reset   F2 Dummy   F3 Damage   F4 Boxes   P Pause   F6 Step", 24, 330, 11, Muted);
        Text("F7 Record P2   F8 Replay   F9 Stage   Esc / Start Menu", 24, 347, 11, Muted);
        var stick = new Vector2(604, 313);
        DrawCircle(stick, 9, new Color("3d4b61"));
        DrawCircle(stick + new Vector2(fighter.Input.X, fighter.Input.Y) * 0.006f, 2.4f, Paper);
    }

    private static string LandingLabel(LandingResult result) => result switch
    {
        LandingResult.LCancel => "L-CANCEL",
        LandingResult.Miss => "MISSED CANCEL",
        LandingResult.None => "—",
        _ => result.ToString().ToUpperInvariant()
    };

    private static float WorldZoom(MatchSimulation match)
    {
        float horizontal = 1;
        float vertical = 1;
        foreach (FighterState fighter in match.Fighters)
        {
            if (fighter.Action is not (ActionState.Out or ActionState.Respawn))
            {
                horizontal = Mathf.Max(horizontal, Mathf.Abs(fighter.X / Unit - 320) / 268);
                vertical = Mathf.Max(vertical, Mathf.Abs(fighter.Y / Unit - 172) / 135);
            }
        }
        return Mathf.Max(0.56f, 1f / Mathf.Max(horizontal, vertical));
    }

    private void Sprite(int player, Vector2 feet, int facing, float width, float height, int frame, Color color)
    {
        Texture2D texture = _sprites[player][frame + (facing < 0 ? 2 : 0)];
        DrawTextureRect(texture, new Rect2(feet.X - width / 2, feet.Y - height, width, height),
            false, color);
    }

    private static Texture2D[] SpriteFrames(string[] pixels, IReadOnlyDictionary<char, Color> palette)
    {
        var frames = new Texture2D[4];
        for (int pose = 0; pose < frames.Length; pose++)
        {
            int width = pixels[0].Length;
            Image image = Image.CreateEmpty(width, pixels.Length, false, Image.Format.Rgba8);
            for (int y = 0; y < pixels.Length; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (palette.TryGetValue(pixels[y][x], out Color color))
                    {
                        int step = (pose & 1) != 0 && y == pixels.Length - 1 ? 1 : 0;
                        int shifted = Math.Clamp(x + (x > width / 2 ? step : -step), 0, width - 1);
                        image.SetPixel(pose >= 2 ? width - 1 - shifted : shifted, y, color);
                    }
                }
            }
            frames[pose] = ImageTexture.CreateFromImage(image);
        }
        return frames;
    }

    private void Text(string value, float x, float y, int size, Color color) =>
        DrawString(_font, new Vector2(x, y), value, HorizontalAlignment.Left, -1, size, color);

    private void Caption(string value, float x, float y, int size, Color color) =>
        Text(value, x - _font.GetStringSize(value, fontSize: size).X / 2, y, size, color);

    private void Box(float x, float y, float width, float height, Color color) =>
        DrawRect(new Rect2(x, y, width, height), color);

    private void Disc(int x, int y, int radius, Color color)
    {
        for (int row = -radius; row < radius; row += 3)
        {
            int width = (int)Mathf.Sqrt(radius * radius - row * row);
            Box(x - width, y + row, width * 2, 3, color);
        }
    }

    private readonly record struct Burst(MatchEvent Event, int Age);

    private static readonly string[] Fox =
    {
        "......d...d.....", ".....dwd.dwd....", ".....doddowd....", "......dwwwwdd...",
        ".....dwwwwwkwd..", ".....dwwwwwwwkd.", "......dwwwwddd..", "..d...dbbrrd....",
        ".dwd.dbbrrdd....", "dwooodbbrrwwd...", ".doooobbrrdd....", "..dooobbddd.....",
        "...dddbbbd......", ".....db.dbd.....", ".....dd..dd....."
    };

    private static readonly IReadOnlyDictionary<char, Color> FoxColors = new Dictionary<char, Color>
    {
        ['d'] = new("1b2336"), ['w'] = new("f4e6cd"), ['o'] = new("ed9b56"),
        ['b'] = new("465876"), ['r'] = new("bd594e"), ['k'] = new("332b3c")
    };

    private static readonly string[] Oni =
    {
        "....d.....d.....", "...dhd...dhd....", "...dhdddddhd....", "...drrrrrrrd....",
        "..drrrrrrrrrd...", "..drrwrrrwrrd...", "..drrrrrrrrrd...", "...drrkwwrdd....",
        "..dddrrrrddd....", ".drrdrrrdrrod...", ".drrrddrdrodcd..", "..dddrrrdrddcccd",
        "...dsysysyddccd.", "...dsyyysdddcd..", "....dkdkd..dd...", "...ddk.dkd......",
        "...ddd.ddd......"
    };

    private static readonly IReadOnlyDictionary<char, Color> OniColors = new Dictionary<char, Color>
    {
        ['d'] = new("271d32"), ['r'] = new("cf655c"), ['o'] = new("ac444f"), ['h'] = new("efc68d"),
        ['w'] = new("f8e4c2"), ['k'] = new("4c3652"), ['s'] = new("866249"), ['y'] = new("dd9d61"), ['c'] = new("738491")
    };
}
