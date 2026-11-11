namespace ThrashFighters.Sim;

public sealed class MatchSimulation
{
    public const int Unit = 1024;
    public const int TicksPerSecond = 60;
    public const int StartingStocks = 4;
    public const int MatchTicks = 8 * 60 * TicksPerSecond;
    public const int Tie = 2;

    private const int Flick = 700;
    private const int InitialDashTicks = 11;
    private readonly FighterState[] _fighters;
    private readonly List<MatchEvent> _events = new(6);

    public StageSpec Stage { get; }
    public IReadOnlyList<FighterState> Fighters => _fighters;
    public IReadOnlyList<MatchEvent> Events => _events;
    public bool Training { get; }
    public int Tick { get; private set; }
    public int TimeLeft { get; private set; } = MatchTicks;
    public int? Winner { get; private set; }

    public MatchSimulation(FighterSpec first, FighterSpec second, StageSpec stage, bool training = false)
    {
        Stage = stage;
        Training = training;
        _fighters = new[] { new FighterState(first), new FighterState(second) };
        Spawn(0, StartingStocks, 0, 0);
        Spawn(1, StartingStocks, 0, 0);
    }

    public void SetTrainingPercent(int percent)
    {
        if (Training)
        {
            _fighters[1].Percent = Math.Clamp(percent, 0, 300);
        }
    }

    public void Step(InputFrame first, InputFrame second)
    {
        _events.Clear();
        if (Winner.HasValue)
        {
            return;
        }

        Tick++;
        _fighters[0].Input = first;
        _fighters[1].Input = second;
        Advance(0);
        Advance(1);
        SeparateGroundedFighters();
        ResolveCombat();
        UpdateGrab(0);
        UpdateGrab(1);
        CheckKnockout(0);
        CheckKnockout(1);
        foreach (FighterState fighter in _fighters)
        {
            fighter.PreviousInput = fighter.Input;
        }

        if (!Training && (--TimeLeft == 0 || _fighters[0].Stocks == 0 || _fighters[1].Stocks == 0))
        {
            int advantage = _fighters[0].Stocks.CompareTo(_fighters[1].Stocks);
            if (advantage == 0 && _fighters[0].Stocks > 0)
            {
                advantage = _fighters[1].Percent.CompareTo(_fighters[0].Percent);
            }

            Winner = advantage == 0 ? Tie : advantage > 0 ? 0 : 1;
        }
    }

    public HitCircle? ActiveHitbox(FighterState fighter)
    {
        if (fighter.Action != ActionState.Attack)
        {
            return null;
        }

        MoveSpec move = fighter.Spec.Moves[fighter.Move];
        return fighter.Age >= move.Startup && fighter.Age < move.Startup + move.Active
            ? new HitCircle(fighter.X + fighter.Facing * move.OffsetX * Unit,
                fighter.Y + move.OffsetY * Unit, move.Radius * Unit)
            : null;
    }

    public HitCircle Hurtbox(FighterState fighter) => new(
        fighter.X, fighter.Y - fighter.Spec.Height * Unit / 2, fighter.Spec.Height * Unit / 2 - Unit);

    public HitCircle Shieldbox(FighterState fighter) => new(
        fighter.X, fighter.Y - fighter.Spec.Height * Unit / 2,
        (fighter.Spec.Height / 2 + 5) * Unit + fighter.Shield * Unit / 75);

    private void Advance(int player)
    {
        FighterState fighter = _fighters[player];
        InputFrame input = fighter.Input;
        FighterSpec spec = fighter.Spec;

        if (fighter.Action == ActionState.Out)
        {
            return;
        }

        if (fighter.Action == ActionState.Respawn)
        {
            if (--fighter.RespawnTicks == 0)
            {
                Spawn(player, fighter.Stocks, Training ? fighter.Percent : 0, 100);
            }
            return;
        }

        if (input.Press(Buttons.Shield))
        {
            fighter.LastShieldPress = Tick;
            if (fighter.TechLock == 0)
            {
                fighter.LastTechPress = Tick;
                fighter.TechLock = 36;
            }
        }
        fighter.TechLock = Math.Max(0, fighter.TechLock - 1);

        if (fighter.Hitlag > 0)
        {
            if (fighter.PendingDi)
            {
                ApplySdi(fighter);
            }
            if (--fighter.Hitlag == 0 && fighter.PendingDi)
            {
                ApplyDi(fighter);
                fighter.PendingDi = false;
            }
            return;
        }

        fighter.Invulnerability = Math.Max(0, fighter.Invulnerability - 1);
        fighter.LedgeLock = Math.Max(0, fighter.LedgeLock - 1);
        fighter.Age++;
        if (fighter.Action is not (ActionState.Shield or ActionState.ShieldStun))
        {
            fighter.Shield = Math.Min(600, fighter.Shield + 3);
        }

        switch (fighter.Action)
        {
            case ActionState.Captured:
            case ActionState.GrabHold:
                return;
            case ActionState.Ledge:
                if (UpdateLedge(fighter))
                {
                    return;
                }
                break;
            case ActionState.JumpSquat:
                fighter.JumpReleased |= !input.Has(Buttons.Jump);
                if (!fighter.Grounded)
                {
                    fighter.Enter(ActionState.Air);
                }
                else if (fighter.Age >= spec.JumpSquat)
                {
                    Jump(player, fighter.JumpReleased ? spec.ShortHop : spec.FullHop, false);
                }
                break;
            case ActionState.Hitstun:
            case ActionState.ShieldStun:
                if (--fighter.Stun <= 0)
                {
                    fighter.Enter(fighter.Grounded && fighter.Action == ActionState.ShieldStun && input.Has(Buttons.Shield)
                        ? ActionState.Shield : FreeState(fighter));
                }
                break;
            case ActionState.Landing:
            case ActionState.Tech:
                if (fighter.Age >= fighter.LandingTicks)
                {
                    fighter.Enter(FreeState(fighter));
                }
                break;
            case ActionState.Attack:
                if (fighter.Age >= spec.Moves[fighter.Move].Duration)
                {
                    fighter.Enter(fighter.Move == MoveId.Recovery && !fighter.Grounded
                        ? ActionState.SpecialFall : FreeState(fighter));
                }
                break;
            case ActionState.AirDodge:
                if (fighter.Age >= 17)
                {
                    fighter.Enter(ActionState.SpecialFall);
                }
                break;
        }

        if (fighter.Grounded)
        {
            GroundAction(player);
        }
        else
        {
            AirAction(player);
        }

        Integrate(player);
        TryLedge(player);
    }

    private void GroundAction(int player)
    {
        FighterState fighter = _fighters[player];
        InputFrame input = fighter.Input;
        FighterSpec spec = fighter.Spec;
        int direction = Math.Sign(input.X);
        if (fighter.Action is ActionState.Shield or ActionState.Ground or ActionState.Dash or ActionState.Run)
        {
            if (DownFlick(fighter) && Stage.Surfaces[fighter.Ground].PassThrough)
            {
                fighter.DropSurface = fighter.Ground;
                fighter.Ground = -1;
                fighter.Y += Unit;
                fighter.VelocityY = Unit;
                fighter.Enter(ActionState.Air);
                return;
            }
            if (input.Press(Buttons.Jump))
            {
                fighter.Enter(ActionState.JumpSquat);
                fighter.JumpReleased = !input.Has(Buttons.Jump);
                return;
            }
            if (input.Press(Buttons.Grab))
            {
                StartMove(fighter, MoveId.Grab, direction);
                return;
            }
            if (input.Has(Buttons.Shield))
            {
                if (fighter.Action != ActionState.Shield)
                {
                    fighter.Enter(ActionState.Shield);
                }
                fighter.Shield = Math.Max(0, fighter.Shield - 2);
                if (fighter.Shield == 0)
                {
                    BreakShield(fighter);
                    return;
                }
            }
            else
            {
                if (fighter.Action == ActionState.Shield)
                {
                    fighter.Enter(ActionState.Ground);
                }
                if (input.Press(Buttons.Special))
                {
                    Special(player);
                    return;
                }
                if (input.Press(Buttons.Smash | Buttons.Attack))
                {
                    MoveId move = input.Press(Buttons.Smash) ? MoveId.Smash
                        : fighter.Action is ActionState.Dash or ActionState.Run ? MoveId.Dash
                        : input.Y < -Flick ? MoveId.UpTilt : input.Y > Flick ? MoveId.DownTilt
                        : input.X != 0 ? MoveId.Tilt : MoveId.Jab;
                    StartMove(fighter, move, input.AimX != 0 ? Math.Sign(input.AimX) : direction);
                    if (move == MoveId.Dash)
                    {
                        fighter.VelocityX = fighter.Facing * spec.Dash;
                    }
                    return;
                }

                bool fullTilt = Math.Abs(input.X) >= Flick;
                bool flicked = fullTilt && (Math.Abs(fighter.PreviousInput.X) < Flick
                    || direction != Math.Sign(fighter.PreviousInput.X));
                if (flicked && (fighter.Action != ActionState.Run || direction == fighter.Facing))
                {
                    fighter.Enter(ActionState.Dash);
                    fighter.Facing = direction;
                    fighter.VelocityX = direction * spec.Dash;
                    return;
                }
                if (fighter.Action == ActionState.Dash && fullTilt && fighter.Facing == direction)
                {
                    if (fighter.Age >= InitialDashTicks)
                    {
                        fighter.Enter(ActionState.Run);
                    }
                    else
                    {
                        return;
                    }
                }
                if (fighter.Action == ActionState.Run && fullTilt)
                {
                    int acceleration = direction == fighter.Facing ? spec.RunAcceleration : spec.Traction;
                    fighter.VelocityX = Approach(fighter.VelocityX, direction * spec.Run, acceleration);
                    if (Math.Sign(fighter.VelocityX) == direction)
                    {
                        fighter.Facing = direction;
                    }
                    return;
                }
                if (fighter.Action != ActionState.Ground)
                {
                    fighter.Enter(ActionState.Ground);
                }
                if (direction != 0)
                {
                    fighter.Facing = direction;
                    fighter.VelocityX = Approach(fighter.VelocityX, spec.Walk * input.X / 1000, spec.RunAcceleration);
                    return;
                }
            }
        }

        int traction = fighter.Action == ActionState.Attack && fighter.Move == MoveId.Dash
            ? spec.Traction / 3 : spec.Traction;
        fighter.VelocityX = Approach(fighter.VelocityX, 0, traction);
    }

    private void AirAction(int player)
    {
        FighterState fighter = _fighters[player];
        FighterSpec spec = fighter.Spec;
        InputFrame input = fighter.Input;
        if (fighter.Action == ActionState.Air)
        {
            if (input.Press(Buttons.Shield))
            {
                int magnitude = Math.Max(1000, (int)Math.Sqrt(input.X * input.X + input.Y * input.Y));
                fighter.VelocityX = spec.AirDodge * input.X / magnitude;
                fighter.VelocityY = spec.AirDodge * input.Y / magnitude;
                fighter.DodgeFromJump = fighter.DodgeFromJump && fighter.Age <= 5;
                fighter.Enter(ActionState.AirDodge);
                Emit(EventKind.Dodge, player);
                return;
            }
            if (input.Press(Buttons.Jump) && fighter.DoubleJumpAvailable && fighter.Age > 0)
            {
                Jump(player, spec.DoubleJump, true);
                fighter.VelocityX = spec.AirSpeed * input.X / 1000;
            }
            if (input.Press(Buttons.Special))
            {
                Special(player);
                if (fighter.Move == MoveId.Recovery && fighter.Action == ActionState.Attack)
                {
                    return;
                }
            }
            else if (input.Press(Buttons.Attack | Buttons.Smash))
            {
                int x = input.AimX != 0 || input.AimY != 0 ? input.AimX : input.X;
                int y = input.AimX != 0 || input.AimY != 0 ? input.AimY : input.Y;
                MoveId move = y < -450 && Math.Abs(y) >= Math.Abs(x) ? MoveId.UpAir
                    : y > 450 && Math.Abs(y) >= Math.Abs(x) ? MoveId.DownAir
                    : Math.Abs(x) < 350 ? MoveId.NeutralAir
                    : Math.Sign(x) == fighter.Facing ? MoveId.ForwardAir : MoveId.BackAir;
                StartMove(fighter, move, 0);
            }
        }

        if (fighter.Action == ActionState.AirDodge)
        {
            fighter.VelocityX = fighter.VelocityX * 9 / 10;
            fighter.VelocityY = fighter.VelocityY * 9 / 10 + (fighter.Age >= 7 ? spec.Gravity / 2 : 0);
            return;
        }

        bool recovery = fighter.Action == ActionState.Attack && fighter.Move == MoveId.Recovery;
        bool drift = fighter.Action is ActionState.Air or ActionState.SpecialFall
            || fighter.Action == ActionState.Attack && !recovery;
        if (drift && (input.X != 0 || fighter.Action != ActionState.Air || fighter.Age > 1))
        {
            int acceleration = input.X == 0 || fighter.Action == ActionState.SpecialFall
                ? spec.AirAcceleration / 3 : spec.AirAcceleration;
            fighter.VelocityX = Approach(fighter.VelocityX, spec.AirSpeed * input.X / 1000, acceleration);
        }
        if (drift && fighter.Action != ActionState.SpecialFall && fighter.VelocityY >= 0 && DownFlick(fighter))
        {
            fighter.FastFalling = true;
        }
        fighter.VelocityY = fighter.FastFalling ? spec.FastFall
            : Math.Min(spec.FallSpeed, fighter.VelocityY + spec.Gravity);
    }

    private void Jump(int player, int speed, bool doubleJump)
    {
        FighterState fighter = _fighters[player];
        fighter.Ground = -1;
        fighter.VelocityY = -speed;
        fighter.FastFalling = false;
        fighter.DodgeFromJump = !doubleJump;
        fighter.DoubleJumpAvailable = !doubleJump;
        fighter.Enter(ActionState.Air);
        Emit(EventKind.Jump, player);
    }

    private void Special(int player)
    {
        FighterState fighter = _fighters[player];
        int direction = Math.Sign(fighter.Input.X);
        if (fighter.Input.Y < -450 && !fighter.RecoveryUsed)
        {
            StartMove(fighter, MoveId.Recovery, direction);
            fighter.RecoveryUsed = true;
            fighter.Ground = -1;
            fighter.FastFalling = false;
            fighter.DodgeFromJump = false;
            fighter.VelocityX = fighter.Spec.AirSpeed * fighter.Input.X / 650;
            fighter.VelocityY = -fighter.Spec.RecoveryRise;
        }
        else
        {
            StartMove(fighter, MoveId.Special, direction);
        }
    }

    private static void StartMove(FighterState fighter, MoveId id, int direction)
    {
        if (direction != 0)
        {
            fighter.Facing = direction;
        }
        fighter.Move = id;
        fighter.HitConnected = false;
        fighter.Enter(ActionState.Attack);
    }

    private void Integrate(int player)
    {
        FighterState fighter = _fighters[player];
        int nextX = fighter.X + fighter.VelocityX + fighter.LaunchX;
        int nextY = fighter.Y + fighter.VelocityY + fighter.LaunchY;
        if (fighter.Grounded)
        {
            SurfaceSpec support = Stage.Surfaces[fighter.Ground];
            if (Within(support, nextX))
            {
                fighter.X = nextX;
                return;
            }
            fighter.Ground = -1;
            fighter.LedgeLock = Math.Max(6, fighter.LedgeLock);
            if (fighter.Action is ActionState.Ground or ActionState.Run or ActionState.Dash
                or ActionState.JumpSquat or ActionState.Shield or ActionState.ShieldStun)
            {
                fighter.Enter(ActionState.Air);
                fighter.DodgeFromJump = false;
            }
        }

        int contact = FindLanding(fighter, nextX, nextY);
        if (contact >= 0)
        {
            SurfaceSpec floor = Stage.Surfaces[contact];
            fighter.X = Math.Clamp(nextX, floor.Left * Unit, floor.Right * Unit);
            fighter.Y = floor.Y * Unit;
            Land(player, contact);
        }
        else
        {
            CheckCeiling(fighter, nextX, ref nextY);
            fighter.X = nextX;
            fighter.Y = nextY;
            if (fighter.DropSurface >= 0 && fighter.Y >= (Stage.Surfaces[fighter.DropSurface].Y + 8) * Unit)
            {
                fighter.DropSurface = -1;
            }
        }
        fighter.LaunchX = Approach(fighter.LaunchX, 0, 168);
        fighter.LaunchY = Approach(fighter.LaunchY, 0, 138);
    }

    private int FindLanding(FighterState fighter, int nextX, int nextY)
    {
        if (nextY <= fighter.Y)
        {
            return -1;
        }
        int result = -1;
        int earliestY = int.MaxValue;
        for (int i = 0; i < Stage.Surfaces.Length; i++)
        {
            SurfaceSpec surface = Stage.Surfaces[i];
            int y = surface.Y * Unit;
            if (i == fighter.DropSurface || fighter.Y > y || nextY < y || y >= earliestY)
            {
                continue;
            }
            int crossX = fighter.X + (int)((long)(nextX - fighter.X) * (y - fighter.Y) / (nextY - fighter.Y));
            if (Within(surface, crossX))
            {
                earliestY = y;
                result = i;
            }
        }
        return result;
    }

    private void CheckCeiling(FighterState fighter, int nextX, ref int nextY)
    {
        SurfaceSpec floor = Stage.Surfaces[0];
        int bottom = (floor.Y + 14 + fighter.Spec.Height) * Unit;
        if (nextY < bottom && fighter.Y >= bottom && nextY < fighter.Y)
        {
            int crossX = fighter.X + (int)((long)(nextX - fighter.X) * (bottom - fighter.Y) / (nextY - fighter.Y));
            if (Within(floor, crossX))
            {
                nextY = bottom;
                fighter.VelocityY = 0;
                fighter.LaunchY = 0;
            }
        }
    }

    private void Land(int player, int surface)
    {
        FighterState fighter = _fighters[player];
        int lag = 4;
        LandingResult result = LandingResult.Normal;
        ActionState action = ActionState.Landing;
        switch (fighter.Action)
        {
            case ActionState.AirDodge:
                lag = 10;
                result = fighter.DodgeFromJump && fighter.Age <= 5 ? LandingResult.Wavedash : LandingResult.Waveland;
                break;
            case ActionState.SpecialFall:
                lag = 19;
                break;
            case ActionState.Attack:
                MoveSpec move = fighter.Spec.Moves[fighter.Move];
                if (fighter.Move == MoveId.Recovery)
                {
                    lag = move.Landing;
                }
                else if (move.Landing > 0 && fighter.Age >= Math.Max(0, move.Startup - 2)
                    && fighter.Age < move.Duration - 5)
                {
                    bool cancelled = Tick - fighter.LastShieldPress < 7;
                    lag = cancelled ? Math.Max(1, move.Landing / 2) : move.Landing;
                    result = cancelled ? LandingResult.LCancel : LandingResult.Miss;
                    if (cancelled)
                    {
                        fighter.LastShieldPress = -100;
                        Emit(EventKind.Cancel, player);
                    }
                }
                else
                {
                    result = LandingResult.Autocancel;
                }
                break;
            case ActionState.Hitstun:
                bool tech = Tick - fighter.LastTechPress <= 15;
                lag = tech ? 16 : Math.Max(17, fighter.Stun);
                if (tech)
                {
                    result = LandingResult.Tech;
                    action = ActionState.Tech;
                    fighter.LastTechPress = -100;
                    fighter.VelocityX = Math.Sign(fighter.Input.X) * 4 * Unit;
                    fighter.Invulnerability = 15;
                }
                break;
        }

        fighter.Ground = surface;
        fighter.VelocityY = 0;
        fighter.LaunchX = fighter.LaunchY = 0;
        fighter.FastFalling = false;
        fighter.DodgeFromJump = false;
        fighter.DoubleJumpAvailable = true;
        fighter.RecoveryUsed = false;
        fighter.LedgeInvulnerabilityUsed = false;
        fighter.DropSurface = -1;
        fighter.LastLanding = result;
        fighter.LandingTicks = lag;
        fighter.Enter(action);
        Emit(EventKind.Land, player);
    }

    private void TryLedge(int player)
    {
        FighterState fighter = _fighters[player];
        if (fighter.Grounded || fighter.LedgeLock > 0 || fighter.Input.Y >= Flick
            || fighter.Action is not (ActionState.Air or ActionState.SpecialFall))
        {
            return;
        }
        SurfaceSpec floor = Stage.Surfaces[0];
        int side = fighter.X < floor.Left * Unit ? -1 : fighter.X > floor.Right * Unit ? 1 : 0;
        int lip = (side < 0 ? floor.Left : floor.Right) * Unit;
        FighterState opponent = _fighters[1 - player];
        if (side == 0 || Math.Abs(fighter.X - lip) > 18 * Unit || fighter.Y < (floor.Y - 6) * Unit
            || fighter.Y > (floor.Y + 32) * Unit || opponent.Action == ActionState.Ledge && opponent.LedgeSide == side)
        {
            return;
        }

        fighter.LedgeSide = side;
        fighter.Facing = -side;
        fighter.X = lip + side * 7 * Unit;
        fighter.Y = (floor.Y + 22) * Unit;
        fighter.VelocityX = fighter.VelocityY = fighter.LaunchX = fighter.LaunchY = 0;
        fighter.DoubleJumpAvailable = true;
        fighter.RecoveryUsed = false;
        fighter.FastFalling = false;
        if (!fighter.LedgeInvulnerabilityUsed)
        {
            fighter.Invulnerability = 30;
            fighter.LedgeInvulnerabilityUsed = true;
        }
        fighter.Enter(ActionState.Ledge);
    }

    private bool UpdateLedge(FighterState fighter)
    {
        if (fighter.Age < 9)
        {
            return true;
        }
        int inward = -fighter.LedgeSide;
        fighter.LedgeLock = 28;
        if (fighter.Input.Press(Buttons.Jump))
        {
            fighter.VelocityX = inward * fighter.Spec.AirSpeed;
            fighter.VelocityY = -fighter.Spec.FullHop;
            fighter.Enter(ActionState.Air);
            return false;
        }
        if (fighter.Input.Y > Flick || fighter.Input.X * inward < -Flick || fighter.Age >= 120)
        {
            fighter.VelocityX = -inward * Unit;
            fighter.Enter(ActionState.Air);
            return false;
        }
        if (fighter.Input.X * inward > Flick || fighter.Input.Press(Buttons.Attack))
        {
            SurfaceSpec floor = Stage.Surfaces[0];
            fighter.X = (inward > 0 ? floor.Left + 13 : floor.Right - 13) * Unit;
            fighter.Y = floor.Y * Unit;
            fighter.Ground = 0;
            fighter.LandingTicks = 13;
            fighter.LedgeInvulnerabilityUsed = false;
            fighter.Enter(ActionState.Landing);
        }
        return true;
    }

    private void ResolveCombat()
    {
        Contact first = DetectContact(0);
        Contact second = DetectContact(1);
        if (first == Contact.Grab && second != Contact.None)
        {
            first = Contact.None;
            if (second == Contact.Grab)
            {
                second = Contact.None;
            }
        }
        else if (second == Contact.Grab && first != Contact.None)
        {
            second = Contact.None;
        }

        // Snapshot both intents before applying either hit, so same-tick strikes trade.
        MoveSpec firstMove = _fighters[0].Spec.Moves[_fighters[0].Move];
        MoveSpec secondMove = _fighters[1].Spec.Moves[_fighters[1].Move];
        ApplyContact(0, first, firstMove);
        ApplyContact(1, second, secondMove);
    }

    private void SeparateGroundedFighters()
    {
        FighterState left = _fighters[0];
        FighterState right = _fighters[1];
        if (!left.Grounded || left.Ground != right.Ground || left.Invulnerability > 0 || right.Invulnerability > 0
            || left.Action is ActionState.Respawn or ActionState.Out || right.Action is ActionState.Respawn or ActionState.Out)
        {
            return;
        }
        if (left.X > right.X)
        {
            (left, right) = (right, left);
        }
        int spacing = (left.Spec.Width + right.Spec.Width) * Unit / 3;
        int overlap = spacing - (right.X - left.X);
        if (overlap > 0)
        {
            SurfaceSpec floor = Stage.Surfaces[left.Ground];
            spacing = Math.Min(spacing, (floor.Right - floor.Left) * Unit);
            left.X = Math.Clamp(left.X - overlap / 2, floor.Left * Unit, floor.Right * Unit - spacing);
            right.X = left.X + spacing;
        }
    }

    private Contact DetectContact(int player)
    {
        FighterState source = _fighters[player];
        FighterState target = _fighters[1 - player];
        HitCircle? hit = ActiveHitbox(source);
        if (source.HitConnected || source.Hitlag > 0 || hit is null || target.Invulnerability > 0
            || target.Action == ActionState.AirDodge && target.Age is >= 2 and < 12
            || target.Action is ActionState.Out or ActionState.Respawn or ActionState.Captured)
        {
            return Contact.None;
        }
        if (source.Move != MoveId.Grab && target.Action is ActionState.Shield or ActionState.ShieldStun
            && Overlaps(hit.Value, Shieldbox(target)))
        {
            return Contact.Block;
        }
        return Overlaps(hit.Value, Hurtbox(target)) ? source.Move == MoveId.Grab ? Contact.Grab : Contact.Hit : Contact.None;
    }

    private void ApplyContact(int player, Contact contact, MoveSpec move)
    {
        if (contact == Contact.None)
        {
            return;
        }
        FighterState source = _fighters[player];
        FighterState target = _fighters[1 - player];
        source.HitConnected = true;
        if (contact == Contact.Grab)
        {
            source.Enter(ActionState.GrabHold);
            source.VelocityX = 0;
            target.Enter(ActionState.Captured);
            target.Hitlag = 0;
            target.PendingDi = false;
            Emit(EventKind.Grab, 1 - player);
        }
        else if (contact == Contact.Block)
        {
            target.Shield = Math.Max(0, target.Shield - move.Damage * 14);
            target.Hitlag = source.Hitlag = 4;
            target.VelocityX = Math.Sign(target.X - source.X) * (Unit + move.Damage * 100);
            target.Enter(ActionState.ShieldStun);
            target.Stun = 4 + move.Damage / 2;
            if (target.Shield == 0)
            {
                BreakShield(target);
            }
            Emit(EventKind.Block, 1 - player);
        }
        else
        {
            Launch(player, move, target.X == source.X ? source.Facing : Math.Sign(target.X - source.X));
        }
    }

    private void UpdateGrab(int player)
    {
        FighterState source = _fighters[player];
        FighterState target = _fighters[1 - player];
        if (target.Action != ActionState.Captured)
        {
            return;
        }
        if (source.Action != ActionState.GrabHold)
        {
            target.Enter(FreeState(target));
            return;
        }

        target.Ground = -1;
        target.X = source.X + source.Facing * 18 * Unit;
        target.Y = source.Y - 4 * Unit;
        target.VelocityX = target.VelocityY = target.LaunchX = target.LaunchY = 0;
        if (source.Age >= 16 || source.Age >= 4 && source.Input.Press(Buttons.Attack | Buttons.Grab))
        {
            int direction = source.Input.X == 0 ? source.Facing : Math.Sign(source.Input.X);
            source.Facing = direction;
            source.Enter(ActionState.Landing);
            source.LandingTicks = 12;
            Launch(player, source.Spec.Moves[MoveId.Grab], direction);
        }
    }

    private void Launch(int player, MoveSpec move, int direction)
    {
        FighterState source = _fighters[player];
        FighterState target = _fighters[1 - player];
        target.Percent = Math.Min(999, target.Percent + move.Damage);
        int strength = Math.Min(28 * Unit, (move.Base + move.Growth * target.Percent / 100) * 100 / target.Spec.Weight);
        double angle = move.Angle * Math.PI / 180;
        int lift = -(int)Math.Round(Math.Sin(angle) * strength);
        target.LaunchX = (int)Math.Round(Math.Cos(angle) * strength) * direction;
        target.LaunchY = target.Grounded && lift >= 0 ? -Math.Max(Unit, lift / 2) : lift;
        target.Ground = -1;
        target.VelocityX = target.VelocityY = 0;
        target.FastFalling = false;
        target.DodgeFromJump = false;
        target.Enter(ActionState.Hitstun);
        target.Stun = 10 + strength / 470;
        target.Hitlag = Math.Max(target.Hitlag, Math.Clamp(3 + move.Damage / 3, 3, 11));
        source.Hitlag = Math.Max(source.Hitlag, target.Hitlag);
        target.PendingDi = true;
        Emit(EventKind.Hit, 1 - player);
    }

    private static void BreakShield(FighterState fighter)
    {
        fighter.Shield = 300;
        fighter.Enter(ActionState.Hitstun);
        fighter.Stun = 67;
        fighter.Ground = -1;
        fighter.VelocityY = -5 * Unit;
    }

    private void ApplySdi(FighterState fighter)
    {
        InputFrame input = fighter.Input;
        InputFrame previous = fighter.PreviousInput;
        int x = DirectionalFlick(input.X, previous.X) ? Math.Sign(input.X) * 2 * Unit : 0;
        int y = DirectionalFlick(input.Y, previous.Y) ? Math.Sign(input.Y) * 2 * Unit : 0;
        int contact = FindLanding(fighter, fighter.X + x, fighter.Y + y);
        fighter.X += x;
        fighter.Y = contact >= 0 ? Stage.Surfaces[contact].Y * Unit : fighter.Y + y;
    }

    private static void ApplyDi(FighterState fighter)
    {
        int x = fighter.LaunchX;
        int y = fighter.LaunchY;
        int magnitude = (int)Math.Sqrt((long)x * x + (long)y * y);
        if (magnitude == 0)
        {
            return;
        }
        int cross = (int)(((long)x * fighter.Input.Y - (long)y * fighter.Input.X) / magnitude);
        double turn = Math.Clamp(cross, -1000, 1000) * Math.PI / 10000;
        fighter.LaunchX = (int)Math.Round(x * Math.Cos(turn) - y * Math.Sin(turn));
        fighter.LaunchY = (int)Math.Round(x * Math.Sin(turn) + y * Math.Cos(turn));
    }

    private void CheckKnockout(int player)
    {
        FighterState fighter = _fighters[player];
        if (fighter.Action is ActionState.Out or ActionState.Respawn || fighter.X >= Stage.BlastLeft * Unit
            && fighter.X <= Stage.BlastRight * Unit && fighter.Y >= Stage.BlastTop * Unit && fighter.Y <= Stage.BlastBottom * Unit)
        {
            return;
        }
        Emit(EventKind.Knockout, player);
        if (!Training)
        {
            fighter.Stocks--;
        }
        fighter.Enter(fighter.Stocks == 0 ? ActionState.Out : ActionState.Respawn);
        fighter.RespawnTicks = Training ? 28 : 65;
    }

    private void Spawn(int player, int stocks, int percent, int invulnerability)
    {
        _fighters[player] = new FighterState(_fighters[player].Spec)
        {
            X = (player == 0 ? Stage.SpawnLeft : Stage.SpawnRight) * Unit,
            Y = Stage.Surfaces[0].Y * Unit,
            Ground = 0,
            Facing = player == 0 ? 1 : -1,
            Stocks = stocks,
            Percent = percent,
            Invulnerability = invulnerability,
            Input = _fighters[player].Input,
            PreviousInput = _fighters[player].PreviousInput
        };
    }

    private void Emit(EventKind kind, int player)
    {
        FighterState fighter = _fighters[player];
        _events.Add(new MatchEvent(kind, player, fighter.X, fighter.Y));
    }

    private static bool DownFlick(FighterState fighter) => fighter.Input.Y >= Flick && fighter.PreviousInput.Y < Flick;
    private static bool DirectionalFlick(int axis, int previous) => Math.Abs(axis) >= Flick
        && (Math.Abs(previous) < Flick || Math.Sign(axis) != Math.Sign(previous));
    private static bool Within(SurfaceSpec surface, int x) => x >= surface.Left * Unit && x <= surface.Right * Unit;
    private static ActionState FreeState(FighterState fighter) => fighter.Grounded ? ActionState.Ground : ActionState.Air;
    private static int Approach(int value, int target, int step) => value < target ? Math.Min(value + step, target) : Math.Max(value - step, target);

    private static bool Overlaps(HitCircle first, HitCircle second)
    {
        long x = first.X - second.X;
        long y = first.Y - second.Y;
        long radius = first.Radius + second.Radius;
        return x * x + y * y <= radius * radius;
    }

    private enum Contact { None, Hit, Block, Grab }
}
