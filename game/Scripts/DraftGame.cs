using System;
using System.Collections.Generic;
using Godot;
using ThrashFighters.Sim;

namespace ThrashFighters;

public partial class DraftGame : Node2D
{
    private enum Screen { Title, Select, Playing, Pause, Result, Controls }
    private enum Mode { Solo, Local, Training }
    private enum Dummy { Stand, Shield, Fight, PlayerTwo }

    private readonly ControlProfiles _profiles = new();
    private PlayerControls[] _players = null!;
    private readonly int[] _picks = { 0, 1 };
    private readonly List<InputFrame> _recording = new(600);
    private GameData _data = null!;
    private ArenaView _arena = null!;
    private DraftAudio _audio = null!;
    private Control _menu = null!;
    private MatchSimulation _match = null!;
    private Screen _screen;
    private Mode _mode;
    private Dummy _dummy;
    private int _countdown;
    private int _playback = -1;
    private bool _record;
    private bool _frozen;
    private bool _step;
    private bool _boxes;
    private bool _trainingShrine;
    private bool _recordingShrine;
    private Buttons _lastCpuButtons;

    public override void _Ready()
    {
        Input.UseAccumulatedInput = false;
        _profiles.Load();
        _players = new[] { new PlayerControls(0, _profiles), new PlayerControls(1, _profiles) };
        _data = GameData.Parse(FileAccess.GetFileAsString("res://Data/draft.json"));
        _arena = new ArenaView { TitleStage = _data.Shrine };
        AddChild(_arena);
        _audio = new DraftAudio();
        AddChild(_audio);
        var layer = new CanvasLayer();
        AddChild(layer);
        _menu = new Control { Size = new Vector2(640, 360), MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_menu);
        ShowTitle();

        string[] arguments = OS.GetCmdlineUserArgs();
        if (Array.Exists(arguments, arg => arg == "--training"))
        {
            Begin(Mode.Training);
        }
        else if (Array.Exists(arguments, arg => arg == "--local"))
        {
            Begin(Mode.Local);
        }
        else if (Array.Exists(arguments, arg => arg == "--solo"))
        {
            Begin(Mode.Solo);
        }
    }

    public override void _Input(InputEvent input)
    {
        bool cancel = input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
            || input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Start };
        if (_screen == Screen.Controls && CaptureBinding(input, cancel))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (cancel && _screen != Screen.Title)
        {
            switch (_screen)
            {
                case Screen.Playing: ShowPause(); break;
                case Screen.Pause: Resume(); break;
                case Screen.Controls: CloseBindings(); break;
                default: ShowTitle(); break;
            }
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_screen != Screen.Playing)
        {
            return;
        }

        foreach (PlayerControls player in _players)
        {
            player.Observe(input);
        }
        if (_mode == Mode.Training && input is InputEventKey { Pressed: true, Echo: false } key)
        {
            TrainingCommand(key.Keycode);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var pads = Input.GetConnectedJoypads();
        for (int i = 0; i < _players.Length; i++)
        {
            _players[i].SetPad(i < pads.Count ? pads[i] : -1);
        }
        if (_screen != Screen.Playing)
        {
            return;
        }

        if (_frozen && !_step)
        {
            RefreshArena();
            return;
        }
        _step = false;
        InputFrame first = _players[0].Sample();
        InputFrame second = _players[1].Sample();
        if (_countdown > 0)
        {
            _countdown--;
            RefreshArena();
            return;
        }
        if (_mode == Mode.Solo || _mode == Mode.Training && _dummy == Dummy.Fight && !_record && _playback < 0)
        {
            second = CpuInput();
        }
        else if (_mode == Mode.Training)
        {
            second = TrainingInput(second);
        }

        _match.Step(first, second);
        foreach (MatchEvent effect in _match.Events)
        {
            _arena.AddEffect(effect);
            _audio.Play(effect.Kind);
        }
        _arena.AdvanceEffects();
        RefreshArena();
        if (_match.Winner is int winner)
        {
            ShowResult(winner);
        }
    }

    private void Begin(Mode mode)
    {
        if (_screen is Screen.Title or Screen.Select)
        {
            _dummy = Dummy.Stand;
            _recording.Clear();
        }
        _mode = mode;
        ResetMatch();
    }

    private void ResetMatch()
    {
        _match = new MatchSimulation(FighterFor(0), FighterFor(1),
            _mode == Mode.Training && !_trainingShrine ? _data.Dojo : _data.Shrine, _mode == Mode.Training);
        _arena.Match = _match;
        _arena.ClearPodium();
        _audio.StopVictory();
        _arena.ResetEffects();
        _countdown = _mode == Mode.Training ? 0 : 132;
        _playback = -1;
        _record = _frozen = _step = false;
        _lastCpuButtons = Buttons.None;
        Resume();
    }

    private void Resume()
    {
        _screen = Screen.Playing;
        _menu.Hide();
        foreach (PlayerControls player in _players)
        {
            player.Sample();
        }
        RefreshArena();
    }

    private void RefreshArena()
    {
        _arena.Countdown = _countdown;
        _arena.DebugBoxes = _boxes && _mode == Mode.Training;
        _arena.PracticeText = _mode != Mode.Training ? "" : _record ? $"REC P2  {_recording.Count}/1800"
            : _playback >= 0 ? $"PLAYBACK  {_playback}/{_recording.Count}"
            : $"DUMMY  {_dummy.ToString().ToUpperInvariant()}";
        _arena.Frozen = _frozen;
        _arena.Cpu = _mode == Mode.Solo;
        _arena.QueueRedraw();
    }

    private void TrainingCommand(Key key)
    {
        switch (key)
        {
            case Key.F1:
                ResetMatch();
                break;
            case Key.F2:
                _record = false;
                _playback = -1;
                _dummy = (Dummy)(((int)_dummy + 1) % 4);
                break;
            case Key.F3:
                int percent = _match.Fighters[1].Percent;
                _match.SetTrainingPercent(percent >= 300 ? 0 : percent + 10);
                break;
            case Key.F4:
                _boxes = !_boxes;
                break;
            case Key.P:
                _frozen = !_frozen;
                break;
            case Key.F6:
                _frozen = _step = true;
                break;
            case Key.F7:
                if (_record)
                {
                    _record = false;
                    break;
                }
                ResetMatch();
                _recording.Clear();
                _recordingShrine = _trainingShrine;
                _record = true;
                break;
            case Key.F8:
                if (_recording.Count > 0)
                {
                    _trainingShrine = _recordingShrine;
                    ResetMatch();
                    _playback = 0;
                }
                break;
            case Key.F9:
                _trainingShrine = !_trainingShrine;
                ResetMatch();
                break;
        }
    }

    private InputFrame TrainingInput(InputFrame second)
    {
        if (_record)
        {
            _recording.Add(second);
            _record = _recording.Count < 1800;
            return second;
        }
        if (_playback >= 0)
        {
            InputFrame replay = _recording[_playback++];
            if (_playback == _recording.Count)
            {
                _playback = -1;
            }
            return replay;
        }
        return _dummy switch
        {
            Dummy.PlayerTwo => second,
            Dummy.Shield => new InputFrame(0, 0, Buttons.Shield, Buttons.None),
            _ => default
        };
    }

    private InputFrame CpuInput()
    {
        FighterState target = _match.Fighters[0];
        FighterState cpu = _match.Fighters[1];
        SurfaceSpec floor = _match.Stage.Surfaces[0];
        int x = cpu.X / MatchSimulation.Unit;
        int y = cpu.Y / MatchSimulation.Unit;
        int gap = (target.X - cpu.X) / MatchSimulation.Unit;
        int height = (target.Y - cpu.Y) / MatchSimulation.Unit;
        int phase = _match.Tick % 53;
        int horizontal = 0;
        int vertical = 0;
        Buttons held = Buttons.None;
        if (x < floor.Left || x > floor.Right)
        {
            horizontal = x < floor.Left ? 1000 : -1000;
            if (cpu.Action == ActionState.Air && y > floor.Y + 24)
            {
                held = y < floor.Y + 58 && phase < 19 ? Buttons.Jump : Buttons.Special;
                vertical = -1000;
            }
        }
        else if (cpu.Action == ActionState.Ledge)
        {
            horizontal = x < 320 ? 1000 : -1000;
        }
        else if (target.Action is not (ActionState.Respawn or ActionState.Out))
        {
            int distance = Math.Abs(gap);
            horizontal = distance > 46 ? Math.Sign(gap) * 1000 : Math.Sign(gap) * 480;
            if (height < -28 && distance < 100 && cpu.Grounded && phase is >= 8 and < 25)
            {
                held = Buttons.Jump;
            }
            else if (distance < 65 && Math.Abs(height) < 58)
            {
                held = phase switch
                {
                    < 12 => Buttons.Attack,
                    < 24 when distance < 29 => Buttons.Grab,
                    < 36 when target.Action == ActionState.Attack => Buttons.Shield,
                    >= 45 when target.Percent >= 45 => Buttons.Smash,
                    _ => Buttons.None
                };
                vertical = height < -21 ? -1000 : height > 26 && !cpu.Grounded ? 1000 : 0;
            }
        }
        if (cpu.Action == ActionState.Hitstun)
        {
            horizontal = cpu.X < 320 * MatchSimulation.Unit ? 1000 : -1000;
            vertical = -380;
            held = phase < 5 ? Buttons.Shield : Buttons.None;
        }

        Buttons pressed = held & ~_lastCpuButtons;
        _lastCpuButtons = held;
        return new InputFrame(horizontal, vertical, held, pressed);
    }

    private void ShowTitle()
    {
        _screen = Screen.Title;
        _arena.Match = null;
        _arena.ClearPodium();
        _audio.StopVictory();
        _arena.QueueRedraw();
        ClearMenu();
        Text("THRASH", 33, 23, 42, ArenaView.Paper);
        Text("FIGHTERS", 33, 64, 38, ArenaView.Gold);
        MenuButtons(34, 126, 205,
            ("Solo match", () => ShowFighterSelect(Mode.Solo)), ("Local versus", () => ShowFighterSelect(Mode.Local)),
            ("Training", () => ShowFighterSelect(Mode.Training)), ("Controls", () => ShowBindings(false)), ("Quit", () => GetTree().Quit()));
    }

    private FighterSpec FighterFor(int slot) => _picks[slot] == 0 ? _data.Kitsune : _data.Oni;

    private void ShowFighterSelect(Mode mode)
    {
        _screen = Screen.Select;
        ClearMenu();
        Overlay();
        Text("Choose fighters", 185, 33, 32, ArenaView.Paper);
        Text("Select a slot to switch between Kitsune and Oni.", 185, 83, 12, ArenaView.Gold);
        VBoxContainer stack = ButtonStack(185, 112, 270);
        string second = mode == Mode.Solo ? "CPU" : mode == Mode.Training ? "Dummy" : "P2";
        Button firstPick = FighterPick(stack, 0, "P1");
        FighterPick(stack, 1, second);
        AddButton(stack, mode == Mode.Training ? "Enter training" : "Start match", 270).Pressed += () => Begin(mode);
        AddButton(stack, "Back", 270).Pressed += ShowTitle;
        firstPick.GrabFocus();
    }

    private Button FighterPick(VBoxContainer stack, int slot, string prefix)
    {
        Button pick = AddButton(stack, $"{prefix}   {FighterFor(slot).Name}", 270);
        pick.Pressed += () =>
        {
            _picks[slot] = 1 - _picks[slot];
            pick.Text = $"{prefix}   {FighterFor(slot).Name}";
        };
        return pick;
    }

    private void ShowPause()
    {
        _screen = Screen.Pause;
        ClearMenu();
        Overlay();
        Text("Paused", 238, 83, 32, ArenaView.Paper);
        MenuButtons(238, 133, 164, ("Resume", Resume), ("Controls", () => ShowBindings(true)),
            ("Restart", ResetMatch), ("Main menu", ShowTitle));
    }

    private void ShowResult(int winner)
    {
        _screen = Screen.Result;
        ClearMenu();
        _arena.ShowPodium(winner);
        if (winner != MatchSimulation.Tie)
        {
            _audio.PlayVictory();
        }
        var buttons = new HBoxContainer { Position = new Vector2(145, 312) };
        buttons.AddThemeConstantOverride("separation", 12);
        _menu.AddChild(buttons);
        Button rematch = AddButton(buttons, "Rematch", 169);
        rematch.Pressed += ResetMatch;
        AddButton(buttons, "Main menu", 169).Pressed += ShowTitle;
        rematch.GrabFocus();
    }

    private void Overlay()
    {
        _menu.AddChild(new ColorRect
        {
            Size = new Vector2(640, 360), Color = new Color(0.025f, 0.043f, 0.073f, 0.91f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
    }

    private void ClearMenu()
    {
        foreach (Node child in _menu.GetChildren())
        {
            _menu.RemoveChild(child);
            child.QueueFree();
        }
        _menu.Show();
    }

    private Label Text(string value, int x, int y, int size, Color color)
    {
        var label = new Label { Text = value, Position = new Vector2(x, y), MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        _menu.AddChild(label);
        return label;
    }

    private void MenuButtons(int x, int y, int width, params (string Text, System.Action Click)[] items)
    {
        VBoxContainer stack = ButtonStack(x, y, width);
        Button? first = null;
        foreach (var item in items)
        {
            Button button = AddButton(stack, item.Text, width);
            button.Pressed += item.Click;
            first ??= button;
        }
        first?.GrabFocus();
    }

    private VBoxContainer ButtonStack(int x, int y, int width)
    {
        var stack = new VBoxContainer { Position = new Vector2(x, y), Size = new Vector2(width, 0) };
        stack.AddThemeConstantOverride("separation", 7);
        _menu.AddChild(stack);
        return stack;
    }

    private static Button AddButton(Control parent, string text, int width, int height = 31, int fontSize = 15)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, height), Alignment = HorizontalAlignment.Left };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color("233349"), new Color("496078")));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color("3e4e60"), ArenaView.Gold));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color("55483c"), ArenaView.Gold));
        button.AddThemeStyleboxOverride("focus", new StyleBoxFlat { DrawCenter = false, BorderColor = ArenaView.Gold, BorderWidthLeft = 3 });
        parent.AddChild(button);
        return button;
    }

    private static StyleBoxFlat ButtonStyle(Color background, Color border) => new()
    {
        BgColor = background, BorderColor = border, BorderWidthBottom = 1,
        ContentMarginLeft = 13, ContentMarginRight = 9
    };
}
