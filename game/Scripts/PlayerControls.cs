using System.Linq;
using Godot;
using ThrashFighters.Sim;

namespace ThrashFighters;

internal sealed class PlayerControls(int slot, ControlProfiles profiles)
{
    private readonly bool[] _triggers = new bool[2];
    private int _pad = -1;
    private Buttons _previous;
    private Buttons _pressed;

    public void SetPad(int device)
    {
        if (_pad != device)
        {
            _pad = device;
            System.Array.Clear(_triggers);
        }
    }

    public void Observe(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            foreach (ControlAction action in ControlProfiles.Combat)
            {
                if (profiles.Keyboard(slot, action) == key.PhysicalKeycode)
                {
                    _pressed |= ControlProfiles.Button(action);
                    return;
                }
            }
        }
        else if (input is InputEventJoypadButton { Pressed: true } button && button.Device == _pad)
        {
            ObservePadPress(PadBinding.Button(button.ButtonIndex));
        }
        else if (input is InputEventJoypadMotion motion && motion.Device == _pad
            && motion.Axis is JoyAxis.TriggerLeft or JoyAxis.TriggerRight)
        {
            if (UpdateTrigger(motion.Axis, motion.AxisValue))
            {
                ObservePadPress(PadBinding.Axis(motion.Axis, 1));
            }
        }
    }

    public InputFrame Sample()
    {
        int x = KeyAxis(ControlAction.Left, ControlAction.Right);
        int y = KeyAxis(ControlAction.Up, ControlAction.Down);
        int aimX = 0;
        int aimY = 0;
        Buttons held = Buttons.None;
        Vector2 left = Vector2.Zero;
        Vector2 right = Vector2.Zero;
        if (_pad >= 0)
        {
            left = ReadStick(JoyAxis.LeftX, JoyAxis.LeftY);
            right = ReadStick(JoyAxis.RightX, JoyAxis.RightY);
            UpdateTrigger(JoyAxis.TriggerLeft, Input.GetJoyAxis(_pad, JoyAxis.TriggerLeft));
            UpdateTrigger(JoyAxis.TriggerRight, Input.GetJoyAxis(_pad, JoyAxis.TriggerRight));
            if (x == 0 && y == 0)
            {
                x = PadAxis(ControlAction.Left, ControlAction.Right, left, right);
                y = PadAxis(ControlAction.Up, ControlAction.Down, left, right);
            }
        }

        foreach (ControlAction action in ControlProfiles.Combat)
        {
            bool down = Input.IsPhysicalKeyPressed(profiles.Keyboard(slot, action));
            if (_pad >= 0)
            {
                PadBinding[] bindings = profiles.Pad(slot, action);
                bool defaultSmash = action == ControlAction.Smash && bindings.Length == 4
                    && bindings.All(binding => binding.IsRightStick);
                bool aimed = defaultSmash ? right.LengthSquared() >= 0.48f
                    : action == ControlAction.Smash && bindings.Any(binding => binding.IsRightStick && Strength(binding, left, right) >= 0.65f);
                if (aimed)
                {
                    aimX = Mathf.RoundToInt(right.X * 1000);
                    aimY = Mathf.RoundToInt(right.Y * 1000);
                }
                down |= aimed || !defaultSmash && bindings.Any(binding => Strength(binding, left, right) >= 0.65f);
            }
            if (down)
            {
                held |= ControlProfiles.Button(action);
            }
        }

        Buttons pressed = _pressed | (held & ~_previous);
        _pressed = Buttons.None;
        _previous = held;
        return new InputFrame(x, y, held, pressed, aimX, aimY);
    }

    private void ObservePadPress(PadBinding binding)
    {
        foreach (ControlAction action in ControlProfiles.Combat)
        {
            if (profiles.Pad(slot, action).Contains(binding))
            {
                _pressed |= ControlProfiles.Button(action);
                return;
            }
        }
    }

    private bool UpdateTrigger(JoyAxis axis, float value)
    {
        int index = (int)axis - (int)JoyAxis.TriggerLeft;
        bool held = _triggers[index];
        _triggers[index] = value >= (held ? 0.30f : 0.42f);
        return !held && _triggers[index];
    }

    private int PadAxis(ControlAction negative, ControlAction positive, Vector2 left, Vector2 right) =>
        Mathf.RoundToInt((profiles.Pad(slot, positive).Max(binding => Strength(binding, left, right))
            - profiles.Pad(slot, negative).Max(binding => Strength(binding, left, right))) * 1000);

    private float Strength(PadBinding binding, Vector2 left, Vector2 right)
    {
        if (!binding.IsAxis)
        {
            return Input.IsJoyButtonPressed(_pad, (JoyButton)binding.Code) ? 1 : 0;
        }
        float value = binding.AxisCode switch
        {
            JoyAxis.LeftX => left.X, JoyAxis.LeftY => left.Y,
            JoyAxis.RightX => right.X, JoyAxis.RightY => right.Y,
            _ => _triggers[(int)binding.AxisCode - (int)JoyAxis.TriggerLeft] ? 1 : 0
        };
        return Mathf.Max(0, value * binding.Direction);
    }

    private Vector2 ReadStick(JoyAxis x, JoyAxis y)
    {
        var stick = new Vector2(Input.GetJoyAxis(_pad, x), Input.GetJoyAxis(_pad, y));
        float length = stick.Length();
        return length <= 0.19f ? Vector2.Zero : stick / length * Mathf.Min(1, (length - 0.19f) / 0.81f);
    }

    private int KeyAxis(ControlAction negative, ControlAction positive) =>
        ((Input.IsPhysicalKeyPressed(profiles.Keyboard(slot, positive)) ? 1 : 0)
            - (Input.IsPhysicalKeyPressed(profiles.Keyboard(slot, negative)) ? 1 : 0)) * 1000;
}
