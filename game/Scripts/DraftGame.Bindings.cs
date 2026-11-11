using Godot;

namespace ThrashFighters;

public partial class DraftGame
{
    private int _bindingPlayer;
    private bool _bindingGamepad;
    private bool _returnToPause;
    private ControlAction? _capture;
    private readonly Button[] _bindingRows = new Button[ControlProfiles.Actions.Length];
    private Label _bindingStatus = null!;
    private Button _bindingBack = null!;

    private void ShowBindings(bool returnToPause)
    {
        _screen = Screen.Controls;
        _returnToPause = returnToPause;
        BuildBindings();
    }

    private void BuildBindings(int focusedTab = 0)
    {
        _capture = null;
        ClearMenu();
        Overlay();
        Text("Control bindings", 31, 12, 29, ArenaView.Paper);

        Button[] tabs =
        {
            BindingButton("Player 1", 32, 60, 115, 30, 13, () => SelectBindingTab(0, _bindingGamepad, 0)),
            BindingButton("Player 2", 155, 60, 115, 30, 13, () => SelectBindingTab(1, _bindingGamepad, 1)),
            BindingButton("Keyboard", 343, 60, 125, 30, 13, () => SelectBindingTab(_bindingPlayer, false, 2)),
            BindingButton("Controller", 476, 60, 132, 30, 13, () => SelectBindingTab(_bindingPlayer, true, 3))
        };
        MarkActive(tabs[_bindingPlayer]);
        MarkActive(tabs[_bindingGamepad ? 3 : 2]);

        foreach (ControlAction action in ControlProfiles.Actions)
        {
            int column = (int)action / 5;
            int x = 32 + column * 311;
            int y = 104 + (int)action % 5 * 28;
            Text(ControlProfiles.Label(action), x, y + 3, 12, ArenaView.Paper);
            _bindingRows[(int)action] = BindingButton("", x + 111, y, 153, 24, 11, () => BeginBinding(action));
        }

        _bindingStatus = Text("Choose an action to change its input.", 32, 249, 13, ArenaView.Gold);
        Text("Wavedash: jump, then dodge down-diagonal.  L-cancel: tap shield before landing.",
            32, 277, 11, ArenaView.Paper);
        Text("Up + Special recovers. Flick down to fast-fall or drop through a platform.",
            32, 291, 11, ArenaView.Paper);
        BindingButton("Reset defaults", 32, 317, 143, 28, 13, ResetBindings);
        _bindingBack = BindingButton("Back", 479, 317, 129, 28, 13, CloseBindings);
        RefreshBindingLabels();
        tabs[focusedTab].GrabFocus();
    }

    private void SelectBindingTab(int player, bool gamepad, int tab)
    {
        _bindingPlayer = player;
        _bindingGamepad = gamepad;
        BuildBindings(tab);
    }

    private Button BindingButton(string title, int x, int y, int width, int height, int fontSize, System.Action press)
    {
        Button button = AddButton(_menu, title, width, height, fontSize);
        button.Position = new Vector2(x, y);
        button.Pressed += press;
        return button;
    }

    private static void MarkActive(Button tab)
    {
        tab.AddThemeStyleboxOverride("normal", ButtonStyle(new Color("484036"), ArenaView.Gold));
        tab.AddThemeColorOverride("font_color", ArenaView.Gold);
    }

    private void RefreshBindingLabels()
    {
        foreach (ControlAction action in ControlProfiles.Actions)
        {
            _bindingRows[(int)action].Text = _capture == action
                ? "Listening…" : _profiles.BindingLabel(_bindingPlayer, action, _bindingGamepad);
        }
        _bindingBack.Text = _capture.HasValue ? "Cancel binding" : "Back";
    }

    private void BeginBinding(ControlAction action)
    {
        _capture = action;
        _bindingStatus.Text = _bindingGamepad
            ? "Press a button, pull a trigger, or tilt a stick. Start cancels."
            : "Press the new key. Esc cancels.";
        RefreshBindingLabels();
    }

    private bool CaptureBinding(InputEvent input, bool cancel)
    {
        if (_capture is not ControlAction action || input is InputEventMouse)
        {
            return false;
        }
        if (cancel)
        {
            EndBinding("Binding canceled.");
        }
        else if (!_bindingGamepad && input is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (ControlProfiles.AllowedKey(key.PhysicalKeycode))
            {
                SaveBinding(_profiles.BindKey(_bindingPlayer, action, key.PhysicalKeycode));
            }
            else
            {
                _bindingStatus.Text = "That key is reserved. Press a different key.";
            }
        }
        else if (_bindingGamepad)
        {
            PadBinding? binding = input switch
            {
                InputEventJoypadButton { Pressed: true } button => PadBinding.Button(button.ButtonIndex),
                InputEventJoypadMotion motion when Mathf.Abs(motion.AxisValue) >= 0.70f =>
                    PadBinding.Axis(motion.Axis, Mathf.Sign(motion.AxisValue)),
                _ => null
            };
            if (binding is { Valid: true } accepted)
            {
                SaveBinding(_profiles.BindPad(_bindingPlayer, action, accepted));
            }
        }
        return true;
    }

    private void ResetBindings()
    {
        _profiles.Reset(_bindingPlayer, _bindingGamepad);
        SaveBinding("Defaults restored for this player and device.");
    }

    private void SaveBinding(string status) => EndBinding(_profiles.Save() ? status : "Changed for this session. Save failed.");

    private void EndBinding(string status)
    {
        _capture = null;
        _bindingStatus.Text = status;
        RefreshBindingLabels();
    }

    private void CloseBindings()
    {
        if (_capture.HasValue)
        {
            EndBinding("Binding canceled.");
        }
        else if (_returnToPause)
        {
            ShowPause();
        }
        else
        {
            ShowTitle();
        }
    }
}
