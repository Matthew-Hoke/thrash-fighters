using Godot;

namespace ThrashFighters;

public partial class ToolchainCheck : Node2D
{
    private const int PixelsPerTick = 4;

    private ColorRect _floor = null!;
    private ColorRect _marker = null!;
    private Label _tickReadout = null!;
    private long _physicsTick;

    public override void _Ready()
    {
        _floor = GetNode<ColorRect>("Floor");
        _marker = GetNode<ColorRect>("Marker");
        _tickReadout = GetNode<Label>("TickReadout");
    }

    public override void _PhysicsProcess(double delta)
    {
        _physicsTick++;
        if (_physicsTick == 1)
        {
            GD.Print("Thrash Fighters physics loop started.");
        }

        int direction = (Input.IsActionPressed("ui_right") ? 1 : 0)
            - (Input.IsActionPressed("ui_left") ? 1 : 0);
        Vector2 position = _marker.Position;
        float rightEdge = _floor.Position.X + _floor.Size.X - _marker.Size.X;
        position.X = Mathf.Clamp(position.X + direction * PixelsPerTick, _floor.Position.X, rightEdge);
        _marker.Position = position;
        _tickReadout.Text = $"Physics tick {_physicsTick}";
    }
}
