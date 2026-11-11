using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThrashFighters.Sim;

namespace ThrashFighters;

internal enum ControlAction { Left, Right, Up, Down, Jump, Attack, Special, Shield, Grab, Smash }

internal sealed class ControlProfiles
{
    private const string Path = "user://controls.cfg";
    private static readonly Key[][] DefaultKeys =
    {
        new[] { Key.A, Key.D, Key.W, Key.S, Key.J, Key.K, Key.L, Key.I, Key.U, Key.O },
        new[] { Key.Left, Key.Right, Key.Up, Key.Down, Key.N, Key.M, Key.Comma, Key.Period, Key.Slash, Key.Semicolon }
    };
    private static readonly string[] Labels =
    {
        "Move left", "Move right", "Aim up", "Aim down", "Jump", "Attack", "Special", "Shield / dodge", "Grab / throw", "Smash / aerial"
    };
    private static readonly Buttons[] FightButtons =
    {
        Buttons.Jump, Buttons.Attack, Buttons.Special, Buttons.Shield, Buttons.Grab, Buttons.Smash
    };

    public static readonly ControlAction[] Actions = Enum.GetValues<ControlAction>();
    public static readonly ControlAction[] Combat = Actions[4..];
    private readonly Key[][] _keys = DefaultKeys.Select(keys => keys.ToArray()).ToArray();
    private readonly PadBinding[][][] _pads = { DefaultPad(), DefaultPad() };

    public Key Keyboard(int slot, ControlAction action) => _keys[slot][(int)action];
    public PadBinding[] Pad(int slot, ControlAction action) => _pads[slot][(int)action];
    public static Buttons Button(ControlAction action) => FightButtons[(int)action - 4];
    public static string Label(ControlAction action) => Labels[(int)action];

    public static bool AllowedKey(Key key) => key != 0 && Enum.IsDefined(key)
        && key is not (Key.Escape or Key.P or Key.F1 or Key.F2 or Key.F3 or Key.F4 or Key.F6 or Key.F7 or Key.F8 or Key.F9);

    public string BindingLabel(int slot, ControlAction action, bool gamepad)
    {
        if (gamepad)
        {
            PadBinding[] bindings = Pad(slot, action);
            return bindings.Length == 4 && bindings.All(binding => binding.IsRightStick)
                ? "Right stick" : string.Join(" / ", bindings.Select(binding => binding.Label));
        }
        Key physical = Keyboard(slot, action);
        Key local = DisplayServer.KeyboardGetKeycodeFromPhysical(physical);
        return OS.GetKeycodeString(local == 0 ? physical : local);
    }

    public string BindKey(int slot, ControlAction action, Key key)
    {
        int index = (int)action;
        string status = "Binding saved.";
        for (int player = 0; player < _keys.Length; player++)
        {
            int other = Array.IndexOf(_keys[player], key);
            if (other >= 0 && (player != slot || other != index))
            {
                _keys[player][other] = _keys[slot][index];
                status = $"Swapped with P{player + 1} {Label((ControlAction)other)}.";
                break;
            }
        }
        _keys[slot][index] = key;
        return status;
    }

    public string BindPad(int slot, ControlAction action, PadBinding binding)
    {
        int index = (int)action;
        PadBinding[][] profile = _pads[slot];
        string status = "Binding saved.";
        for (int other = 0; other < profile.Length; other++)
        {
            int position = Array.IndexOf(profile[other], binding);
            if (other != index && position >= 0)
            {
                profile[other][position] = profile[index][0];
                status = $"Swapped with {Label((ControlAction)other)}.";
                break;
            }
        }
        profile[index] = new[] { binding };
        return status;
    }

    public void Reset(int slot, bool gamepad)
    {
        if (gamepad)
        {
            _pads[slot] = DefaultPad();
            return;
        }
        foreach (ControlAction action in Actions)
        {
            BindKey(slot, action, DefaultKeys[slot][(int)action]);
        }
    }

    public void Load()
    {
        var config = new ConfigFile();
        if (config.Load(Path) != Error.Ok)
        {
            return;
        }
        var keyProfiles = new Key[2][];
        for (int slot = 0; slot < 2; slot++)
        {
            string section = $"player_{slot + 1}";
            keyProfiles[slot] = Actions.Select(action =>
                (Key)config.GetValue(section, $"key_{action}", (int)Keyboard(slot, action)).AsInt32()).ToArray();
            var padProfile = new PadBinding[Actions.Length][];
            foreach (ControlAction action in Actions)
            {
                Variant raw = config.GetValue(section, $"pad_{action}", Encode(Pad(slot, action)));
                padProfile[(int)action] = raw.AsString().Split(',').Select(code =>
                    new PadBinding(int.TryParse(code, out int value) ? value : -1)).ToArray();
            }
            PadBinding[] flat = padProfile.SelectMany(bindings => bindings).ToArray();
            if (padProfile.All(bindings => bindings.Length is >= 1 and <= 4) && flat.All(binding => binding.Valid)
                && flat.Distinct().Count() == flat.Length)
            {
                _pads[slot] = padProfile;
            }
        }
        Key[] keys = keyProfiles.SelectMany(profile => profile).ToArray();
        if (keys.All(AllowedKey) && keys.Distinct().Count() == keys.Length)
        {
            _keys[0] = keyProfiles[0];
            _keys[1] = keyProfiles[1];
        }
    }

    public bool Save()
    {
        var config = new ConfigFile();
        for (int slot = 0; slot < 2; slot++)
        {
            string section = $"player_{slot + 1}";
            foreach (ControlAction action in Actions)
            {
                config.SetValue(section, $"key_{action}", (int)Keyboard(slot, action));
                config.SetValue(section, $"pad_{action}", Encode(Pad(slot, action)));
            }
        }
        string temporary = $"{Path}.{Guid.NewGuid():N}.tmp";
        if (config.Save(temporary) != Error.Ok)
        {
            return false;
        }
        try
        {
            System.IO.File.Move(ProjectSettings.GlobalizePath(temporary), ProjectSettings.GlobalizePath(Path), overwrite: true);
            return true;
        }
        catch (System.IO.IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Encode(IEnumerable<PadBinding> bindings) => string.Join(",", bindings.Select(binding => binding.Code));

    private static PadBinding[][] DefaultPad() => new[]
    {
        new[] { PadBinding.Axis(JoyAxis.LeftX, -1) }, new[] { PadBinding.Axis(JoyAxis.LeftX, 1) },
        new[] { PadBinding.Axis(JoyAxis.LeftY, -1) }, new[] { PadBinding.Axis(JoyAxis.LeftY, 1) },
        new[] { PadBinding.Button(JoyButton.A), PadBinding.Button(JoyButton.Y) },
        new[] { PadBinding.Button(JoyButton.X) }, new[] { PadBinding.Button(JoyButton.B) },
        new[] { PadBinding.Button(JoyButton.LeftShoulder), PadBinding.Axis(JoyAxis.TriggerLeft, 1), PadBinding.Axis(JoyAxis.TriggerRight, 1) },
        new[] { PadBinding.Button(JoyButton.RightShoulder) },
        new[] { PadBinding.Axis(JoyAxis.RightX, -1), PadBinding.Axis(JoyAxis.RightX, 1),
            PadBinding.Axis(JoyAxis.RightY, -1), PadBinding.Axis(JoyAxis.RightY, 1) }
    };
}

internal readonly record struct PadBinding(int Code)
{
    private const int FirstAxis = 128;
    public bool IsAxis => Code >= FirstAxis;
    public JoyAxis AxisCode => (JoyAxis)((Code - FirstAxis) / 2);
    public int Direction => (Code & 1) == 0 ? 1 : -1;
    public bool IsTrigger => IsAxis && AxisCode is JoyAxis.TriggerLeft or JoyAxis.TriggerRight;
    public bool IsRightStick => IsAxis && AxisCode is JoyAxis.RightX or JoyAxis.RightY;
    public bool Valid => IsAxis ? (int)AxisCode is >= 0 and <= 5 && (!IsTrigger || Direction > 0)
        : Code >= 0 && Code < FirstAxis && Code != (int)JoyButton.Start;

    public static PadBinding Axis(JoyAxis axis, int direction) => (int)axis is >= 0 and <= 5
        ? new(FirstAxis + (int)axis * 2 + (direction < 0 ? 1 : 0)) : new(-1);
    public static PadBinding Button(JoyButton button) => (int)button is >= 0 and < FirstAxis
        ? new((int)button) : new(-1);

    public string Label => IsAxis ? AxisCode switch
    {
        JoyAxis.TriggerLeft => "LT", JoyAxis.TriggerRight => "RT",
        JoyAxis.LeftX => Direction < 0 ? "LS Left" : "LS Right",
        JoyAxis.LeftY => Direction < 0 ? "LS Up" : "LS Down",
        JoyAxis.RightX => Direction < 0 ? "RS Left" : "RS Right",
        _ => Direction < 0 ? "RS Up" : "RS Down"
    } : (JoyButton)Code switch
    {
        JoyButton.LeftShoulder => "LB", JoyButton.RightShoulder => "RB",
        JoyButton.LeftStick => "L3", JoyButton.RightStick => "R3",
        JoyButton.DpadLeft => "D-pad Left", JoyButton.DpadRight => "D-pad Right",
        JoyButton.DpadUp => "D-pad Up", JoyButton.DpadDown => "D-pad Down",
        _ => Enum.IsDefined((JoyButton)Code) ? ((JoyButton)Code).ToString() : $"Button {Code}"
    };
}
