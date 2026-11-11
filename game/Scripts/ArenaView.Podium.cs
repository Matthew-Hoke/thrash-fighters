using Godot;
using ThrashFighters.Sim;

namespace ThrashFighters;

public partial class ArenaView
{
    private int? _podium;
    private float _celebration;

    public void ShowPodium(int winner)
    {
        _podium = winner;
        _celebration = 0;
        QueueRedraw();
    }

    public void ClearPodium() => _podium = null;

    public override void _Process(double delta)
    {
        if (_podium.HasValue)
        {
            _celebration += (float)delta;
            QueueRedraw();
        }
    }

    private void DrawPodium(MatchSimulation match, int winner)
    {
        bool tie = winner == MatchSimulation.Tie;
        int first = tie ? 0 : winner;
        int second = 1 - first;
        Color slate = new("798da5");
        float reveal = Mathf.Clamp(_celebration * 2.8f, 0, 1);
        reveal = 1 - (1 - reveal) * (1 - reveal);
        float slide = Mathf.Round((1 - reveal) * 24);

        Box(0, 0, 640, 360, new Color(Ink, 0.29f));
        Spotlight(tie ? 243 : 246, Gold);
        if (tie)
        {
            Spotlight(397, Gold);
        }
        CelebrationParticles(tie);
        Box(0, 271, 640, 89, new Color("151e30"));
        Box(0, 271, 640, 2, new Color("45536a"));

        ResultFighter(match, first, tie ? 243 : 246, 213 + slide, true, Gold, 1);
        ResultFighter(match, second, tie ? 397 : 408, (tie ? 213 : 236) + slide, tie, tie ? Gold : slate, tie ? 1 : 2);

        Box(0, 0, 640, 76, new Color(Ink, 0.78f));
        Box(220, 75, 200, 1, new Color(Gold, 0.65f));
        Caption(tie ? "DRAW" : $"{match.Fighters[winner].Spec.Name.ToUpperInvariant()} WINS", 320, 40, 32, Paper);
        Caption(match.TimeLeft == 0 ? "TIME EXPIRED" : "STOCK MATCH", 320, 60, 11, Gold);
    }

    private void Spotlight(int x, Color color)
    {
        DrawColoredPolygon(new[]
        {
            new Vector2(x - 10, 77), new Vector2(x + 10, 77),
            new Vector2(x + 75, 263), new Vector2(x - 75, 263)
        }, new Color(color, 0.12f));
    }

    private void CelebrationParticles(bool tie)
    {
        for (int i = 0; i < 46; i++)
        {
            float age = _celebration - (i % 12) * 0.04f;
            if (age < 0)
            {
                continue;
            }
            float y = (i * 59 % 153 + age * (28 + i % 9 * 4)) % 226;
            float x = 75 + i * 101 % 488 + Mathf.Sin(age * 1.8f + i) * 12;
            Color color = i % 3 == 0 ? Paper : i % 3 == 1 && !tie ? Vermilion : Gold;
            Box(Mathf.Round(x), 53 + Mathf.Round(y), i % 3 == 0 ? 2 : 4, i % 2 == 0 ? 2 : 4,
                new Color(color, 0.66f));
        }
    }

    private void ResultFighter(MatchSimulation match, int player, int x, float top, bool champion, Color trim, int rank)
    {
        FighterState fighter = match.Fighters[player];
        int sprite = fighter.Spec.Name == "Oni" ? 1 : 0;
        int width = champion ? 122 : 100;
        float left = x - width / 2f;
        Box(left + 6, top, width, 274 - top, new Color("0d1323"));
        Box(left, top, width, 271 - top, champion ? new Color("604757") : new Color("34435a"));
        Box(left, top, width, 4, trim);
        Box(left + 6, top + 9, width - 12, 261 - top, new Color(Ink, 0.28f));
        Caption(rank.ToString(), x, top + (271 - top) * 0.66f + 4, champion ? 26 : 18, trim);

        float hop = champion ? Mathf.Round(Mathf.Abs(Mathf.Sin(_celebration * 4.4f)) * 11) : 0;
        float height = (sprite == 1 ? 74 : 66) * (champion ? 1 : 0.80f);
        float spriteWidth = (sprite == 1 ? 74 : 70) * (champion ? 1 : 0.80f);
        Color tint = champion ? Colors.White : new Color("c8d3e5");
        if (player == 1 && match.Fighters[0].Spec.Name == match.Fighters[1].Spec.Name)
        {
            tint = new Color("9ecbff");
        }
        Sprite(sprite, new Vector2(x, top - hop), champion ? 1 : -1, spriteWidth, height,
            champion && hop > 5 ? 1 : 0, tint);
        if (champion)
        {
            float crown = top - hop - height - 14;
            Box(x - 10, crown + 5, 20, 6, Gold);
            Box(x - 10, crown, 4, 6, Gold);
            Box(x - 2, crown - 3, 4, 9, Gold);
            Box(x + 6, crown, 4, 6, Gold);
            Box(x - 1, crown + 6, 2, 3, Vermilion);
        }

        string slot = player == 0 ? "P1" : Cpu ? "CPU" : "P2";
        Caption($"{slot}  {fighter.Spec.Name.ToUpperInvariant()}", x, 286, 12, trim);
        Caption($"{fighter.Stocks} {(fighter.Stocks == 1 ? "stock" : "stocks")}  ·  {fighter.Percent}%", x, 302, 11, Paper);
    }
}
