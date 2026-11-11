namespace ThrashFighters.Sim;

[Flags]
public enum Buttons
{
    None = 0, Jump = 1, Attack = 2, Special = 4, Shield = 8, Grab = 16, Smash = 32
}

// Axes are quantized to [-1000, 1000]. Positive Y points down.
public readonly record struct InputFrame(int X, int Y, Buttons Held, Buttons Pressed, int AimX = 0, int AimY = 0)
{
    public bool Has(Buttons button) => (Held & button) != 0;
    public bool Press(Buttons button) => (Pressed & button) != 0;
}

public enum ActionState
{
    Ground, Dash, Run, JumpSquat, Air, AirDodge, SpecialFall, Landing,
    Attack, Shield, ShieldStun, Hitstun, Tech, Ledge, GrabHold, Captured, Respawn, Out
}

public enum LandingResult { None, Normal, Wavedash, Waveland, LCancel, Miss, Autocancel, Tech }
public enum EventKind { Jump, Dodge, Land, Cancel, Hit, Block, Grab, Knockout }
public readonly record struct MatchEvent(EventKind Kind, int Player, int X, int Y);
public readonly record struct HitCircle(int X, int Y, int Radius);

public sealed class FighterState
{
    public FighterSpec Spec { get; }
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public int VelocityX { get; internal set; }
    public int VelocityY { get; internal set; }
    public int LaunchX { get; internal set; }
    public int LaunchY { get; internal set; }
    public int Facing { get; internal set; }
    public int Percent { get; internal set; }
    public int Stocks { get; internal set; }
    public int Ground { get; internal set; } = -1;
    public int Age { get; internal set; }
    public int Hitlag { get; internal set; }
    public int Stun { get; internal set; }
    public int Invulnerability { get; internal set; }
    public int Shield { get; internal set; } = 600;
    public int LandingTicks { get; internal set; }
    public ActionState Action { get; internal set; }
    public MoveId Move { get; internal set; }
    public LandingResult LastLanding { get; internal set; }
    public InputFrame Input { get; internal set; }
    public bool Grounded => Ground >= 0;

    internal InputFrame PreviousInput;
    internal int LastShieldPress = -100;
    internal int LastTechPress = -100;
    internal int TechLock;
    internal int DropSurface = -1;
    internal int LedgeSide;
    internal int LedgeLock;
    internal bool LedgeInvulnerabilityUsed;
    internal bool DoubleJumpAvailable = true;
    internal bool JumpReleased;
    internal bool FastFalling;
    internal bool DodgeFromJump;
    internal bool HitConnected;
    internal bool PendingDi;
    internal bool RecoveryUsed;
    internal int RespawnTicks;

    internal FighterState(FighterSpec spec) => Spec = spec;

    internal void Enter(ActionState action)
    {
        Action = action;
        Age = 0;
    }
}
