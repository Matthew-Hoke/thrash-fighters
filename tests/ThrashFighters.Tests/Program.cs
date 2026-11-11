using ThrashFighters.Sim;

const int unit = MatchSimulation.Unit;
var data = GameData.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "draft.json")));
var close = data.Dojo with { SpawnLeft = 303, SpawnRight = 335 };

var dash = New();
dash.Step(new InputFrame(1000, 0, 0, 0), default);
Require(dash.Fighters[0].Action == ActionState.Dash, "a full horizontal flick starts a dash");
for (int i = 0; i < 5; i++) dash.Step(new InputFrame(1000, 0, 0, 0), default);
dash.Step(new InputFrame(-1000, 0, 0, 0), default);
Require(dash.Fighters[0] is { Action: ActionState.Dash, Age: 0, VelocityX: < 0 }, "dash dance reverses in the initial dash window");

int shortApex = JumpApex(hold: false);
int fullApex = JumpApex(hold: true);
Require(fullApex + 20 * unit < shortApex, "releasing jump during squat produces a lower hop");

var fast = New();
TakeOff(fast, player: 0, hold: true);
Until(fast, () => fast.Fighters[0].VelocityY >= 0, new InputFrame(0, 0, Buttons.Jump, 0));
fast.Step(new InputFrame(0, 1000, 0, 0), default);
Require(fast.Fighters[0].VelocityY == data.Kitsune.FastFall, "a down flick on descent fast-falls");

int kitsuneSlide = Wavedash(0);
int oniSlide = Wavedash(1);
Require(kitsuneSlide > oniSlide + 5 * unit, "Kitsune's traction yields the longer wavedash");

var platform = New(data.Shrine);
TakeOff(platform, player: 0, hold: true);
Until(platform, () => platform.Fighters[0].Y < 196 * unit, new InputFrame(0, 0, Buttons.Jump, 0));
platform.Step(new InputFrame(0, 1000, Buttons.Shield, Buttons.Shield), default);
Until(platform, () => platform.Fighters[0].Ground == 1);
Require(platform.Fighters[0].LastLanding == LandingResult.Waveland, "air dodge lands on a one-way platform");
Until(platform, () => platform.Fighters[0].Action == ActionState.Ground);
platform.Step(new InputFrame(0, 1000, 0, 0), default);
Require(platform.Fighters[0].Ground == -1 && platform.Fighters[0].Y > 204 * unit, "a down flick drops through the platform");

int missedLag = AerialLanding(cancel: false);
int cancelledLag = AerialLanding(cancel: true);
Require(missedLag == data.Kitsune.Moves[MoveId.NeutralAir].Landing && cancelledLag == missedLag / 2,
    "a fresh L-cancel cuts the aerial landing lock in half");

var hit = New(close);
hit.Step(Press(Buttons.Attack), default);
Until(hit, () => hit.Fighters[1].Hitlag > 0);
Require(hit.Fighters[1].Percent == data.Kitsune.Moves[MoveId.Jab].Damage
    && hit.Fighters[1].Action == ActionState.Hitstun && hit.Fighters[0].Hitlag > 0, "hits produce damage, hitlag, and stun");
for (int i = 0; i < 24; i++) hit.Step(default, default);
Require(hit.Fighters[1].Percent == data.Kitsune.Moves[MoveId.Jab].Damage, "an attack instance connects only once");

var defense = New(close);
defense.Step(Press(Buttons.Attack), Press(Buttons.Shield));
for (int i = 0; i < 16; i++) defense.Step(default, new InputFrame(0, 0, Buttons.Shield, 0));
Require(defense.Fighters[1].Percent == 0 && defense.Fighters[1].Shield < 565, "shield blocks damage and loses durability");
var grab = New(close);
grab.Step(Press(Buttons.Grab), Press(Buttons.Shield));
for (int i = 0; i < 25; i++) grab.Step(default, new InputFrame(0, 0, Buttons.Shield, 0));
Require(grab.Fighters[1].Percent == data.Kitsune.Moves[MoveId.Grab].Damage, "grab beats shield and completes a throw");

var trade = New(close);
trade.Step(default, Press(Buttons.Attack));
trade.Step(Press(Buttons.Attack), default);
trade.Step(default, default);
trade.Step(default, default);
Require(trade.Fighters[0].Percent > 0 && trade.Fighters[1].Percent > 0, "same-tick strikes trade");

var diNeutral = New(close);
var diHeld = New(close);
diNeutral.Step(Press(Buttons.Attack), default);
diHeld.Step(Press(Buttons.Attack), default);
for (int i = 0; i < 6; i++)
{
    diNeutral.Step(default, default);
    diHeld.Step(default, new InputFrame(0, -1000, 0, 0));
}
Require(diNeutral.Fighters[1].Hitlag == 0 && diNeutral.Fighters[1].LaunchY != diHeld.Fighters[1].LaunchY,
    "DI alters launch direction on the last hitlag tick");

var tech = New(close);
tech.Step(Press(Buttons.Attack), default);
Until(tech, () => tech.Fighters[1].Hitlag > 0);
tech.Step(default, Press(Buttons.Shield));
Until(tech, () => tech.Fighters[1].Action == ActionState.Tech, limit: 22);
Require(tech.Fighters[1].LastLanding == LandingResult.Tech, "a shield press before hitstun landing produces a tech");

var ledge = New();
Until(ledge, () => ledge.Fighters[0].Ground < 0, new InputFrame(-600, 0, 0, 0), limit: 250);
Until(ledge, () => ledge.Fighters[0].Action == ActionState.Ledge, limit: 20);
Until(ledge, () => ledge.Fighters[0].Age >= 9);
ledge.Step(Press(Buttons.Jump), default);
Require(ledge.Fighters[0] is { Action: ActionState.Air, VelocityY: < 0 }, "a ledge grab can transition to a ledge jump");

var recovery = New();
var leave = new InputFrame(-1000, 1000, 0, 0);
Until(recovery, () => recovery.Fighters[0].Y > 272 * unit, leave, limit: 150);
int beforeRecovery = recovery.Fighters[0].Y;
recovery.Step(new InputFrame(1000, -1000, Buttons.Special, Buttons.Special), default);
Require(recovery.Fighters[0].Move == MoveId.Recovery && recovery.Fighters[0].Y < beforeRecovery, "up-special climbs from below the stage lip");

var stocks = New();
Until(stocks, () => stocks.Winner.HasValue, new InputFrame(-1000, 1000, 0, 0), limit: 1100);
Require(stocks.Winner == 1 && stocks.Fighters[0].Stocks == 0 && stocks.Fighters[1].Stocks == 4, "four knockouts end the match");
var timed = New();
for (int i = 0; i < MatchSimulation.MatchTicks; i++) timed.Step(default, default);
Require(timed.TimeLeft == 0 && timed.Winner == MatchSimulation.Tie, "the timer resolves an even match as a draw");

var first = New(data.Shrine);
var second = New(data.Shrine);
var random = new Random(42);
for (int i = 0; i < 2400; i++)
{
    InputFrame a = RandomInput();
    InputFrame b = RandomInput();
    first.Step(a, b);
    second.Step(a, b);
    Require(first.Winner == second.Winner && first.TimeLeft == second.TimeLeft, $"replay match state at tick {i}");
    for (int slot = 0; slot < 2; slot++)
    {
        Require(Snapshot(first.Fighters[slot]) == Snapshot(second.Fighters[slot]), $"replay fighter {slot} at tick {i}");
    }
}
Console.WriteLine("Passed movement, wavedash, waveland, platform drop, L-cancel, combat, DI, tech, ledge, recovery, stock, timer, and 2,400-tick replay checks.");

MatchSimulation New(StageSpec? stage = null) => new(data.Kitsune, data.Oni, stage ?? data.Dojo);

int JumpApex(bool hold)
{
    var game = New();
    TakeOff(game, 0, hold);
    Until(game, () => game.Fighters[0].VelocityY >= 0);
    return game.Fighters[0].Y;
}

void TakeOff(MatchSimulation game, int player, bool hold)
{
    var jump = Press(Buttons.Jump);
    var next = new InputFrame(0, 0, hold ? Buttons.Jump : 0, 0);
    StepAs(game, player, jump);
    for (int i = 0; i < game.Fighters[player].Spec.JumpSquat; i++) StepAs(game, player, next);
    Require(!game.Fighters[player].Grounded, "jump squat ends in takeoff");
}

int Wavedash(int player)
{
    var game = New();
    StepAs(game, player, Press(Buttons.Jump));
    for (int i = 1; i < game.Fighters[player].Spec.JumpSquat; i++) StepAs(game, player, default);
    FighterState fighter = game.Fighters[player];
    int start = fighter.X;
    StepAs(game, player, new InputFrame(player == 0 ? -1000 : 1000, 1000, Buttons.Shield, Buttons.Shield));
    Require(fighter.LastLanding == LandingResult.Wavedash && fighter.LandingTicks == 10, "first-airborne-tick dodge slides");
    for (int i = 0; i < 10; i++) StepAs(game, player, default);
    Require(fighter.Action == ActionState.Ground, "wavedash is actionable after ten ticks");
    return Math.Abs(fighter.X - start);
}

int AerialLanding(bool cancel)
{
    var game = New();
    TakeOff(game, 0, hold: false);
    game.Step(Press(Buttons.Attack), default);
    Until(game, () => game.Fighters[0].VelocityY > 0 && game.Fighters[0].Y >= 240 * unit);
    game.Step(cancel ? Press(Buttons.Shield) : default, default);
    Until(game, () => game.Fighters[0].Grounded);
    Require(game.Fighters[0].LastLanding == (cancel ? LandingResult.LCancel : LandingResult.Miss), "aerial landing records timing result");
    return game.Fighters[0].LandingTicks;
}

InputFrame RandomInput()
{
    Buttons button = random.Next(0, 7) switch
    {
        0 => Buttons.Jump, 1 => Buttons.Attack, 2 => Buttons.Special,
        3 => Buttons.Shield, 4 => Buttons.Grab, 5 => Buttons.Smash, _ => 0
    };
    return new InputFrame(random.Next(-1, 2) * 1000, random.Next(-1, 2) * 1000, button,
        random.Next(0, 7) == 0 ? button : 0);
}

static InputFrame Press(Buttons buttons) => new(0, 0, buttons, buttons);
static void StepAs(MatchSimulation game, int player, InputFrame input) =>
    game.Step(player == 0 ? input : default, player == 1 ? input : default);

static void Until(MatchSimulation game, Func<bool> condition, InputFrame input = default, int limit = 120)
{
    for (int i = 0; i < limit && !condition(); i++) game.Step(input, default);
    Require(condition(), $"expected state was not reached by tick {game.Tick}");
}

static (int, int, int, int, int, int, ActionState, int, int, int, int, int, LandingResult) Snapshot(FighterState f) =>
    (f.X, f.Y, f.VelocityX, f.VelocityY, f.LaunchX, f.LaunchY, f.Action, f.Age, f.Ground, f.Percent, f.Stocks, f.Hitlag, f.LastLanding);

static void Require(bool condition, string expected)
{
    if (!condition) throw new InvalidOperationException(expected);
}
