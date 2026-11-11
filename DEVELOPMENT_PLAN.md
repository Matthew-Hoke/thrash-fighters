# Thrash Fighters development plan

Thrash Fighters is a 2D competitive platform fighter starring original interpretations of supernatural figures from Japanese folklore. The first playable build should let two people pick up controllers and play a full stock match on a shrine rooftop. A Melee player should be able to dash-dance at the edge of threat range, wavedash backward into a punish, short-hop fast-fall an aerial, L-cancel it, and continue the exchange on a platform.

Ship it as a native Windows desktop game. Players can double-click `ThrashFighters.exe`, use a desktop shortcut, or start the same executable from a launcher.

## Play the current draft

Open `build/ThrashFighters/ThrashFighters.exe` or extract the entire `build/ThrashFighters-Windows-x64.zip` package and launch the executable inside it. Choose **Solo match**, **Local versus**, or **Training**, then pick Kitsune or Oni for each slot. At the end of a match, the podium shows the finishing order, remaining stocks, and damage before you choose **Rematch** or **Main menu**.

Open **Controls** from the title or pause menu to review or change bindings. Choose a player and input device, select an action, then press the new key, button, or controller direction. Changes save automatically. **Reset defaults** restores the selected player and device. **Esc** or controller **Start** cancels a binding in progress.

For a quick movement check, enter Training as Kitsune. Press **J** to jump, then press **I** while holding **S** and **A** or **D** to wavedash. After a jumping **K** aerial, tap **I** just before landing to L-cancel. **F4** displays collision shapes, **P** pauses the simulation, and **F6** advances one tick. **F7** records P2 inputs from a reset position, and **F8** replays the recording on its original stage. **F9** switches between the shrine and flat training floor.

The draft has provisional movement and frame data in `game/Data/draft.json`. The simulation checks cover stock and timer outcomes, movement timing, combat interactions, and repeatable input sequences. Melee capture matching, controller hardware playtests, and the later roster and networking milestones remain on the roadmap.

## Engine, languages, and tools

| Use | Choice | Reason |
| --- | --- | --- |
| Engine | **Godot 4.7.2 .NET**, Windows desktop first | Use Godot for controller input, sprites, camera, sound, menus, and exports. The game can use a custom 2D simulation inside Godot. |
| Game code | **C#** for both the simulation and Godot scripts | Typed state and move definitions are easy to navigate in VS Code. The simulation checks run in a standalone .NET process. |
| Content | **JSON** for fighter stats, move data, and stage geometry | Change frame data and movement values without rewriting combat logic. Keep authored values readable and validate them at load. |
| Editing | **VS Code** with Microsoft's C# extension, plus the Godot editor | Write, build, debug, and test in VS Code. Set up scenes, preview sprites, and configure exports in Godot. |
| Art | Pixel sprites exported as PNG atlases | Aseprite or LibreSprite can handle the sprite workflow. Start with rectangles and a few readable poses. |

Godot is the free, open-source engine and visual editor we use to assemble stages, preview sprites, run the game, and export it. VS Code is where we write and debug the C# code. Pin Godot 4.7.2 and its matching .NET export templates for the first Windows build. [Godot release](https://godotengine.org/article/maintenance-release-godot-4-7-2/) · [C# and VS Code setup](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_basics.html)

### Set up the Windows workspace

The existing workspace uses portable Godot and .NET SDK copies under `.tools/`. To work on the code, open the repository root in VS Code, install the recommended Microsoft C# extension when prompted, and choose **Debug Thrash Fighters** from Run and Debug. Press **F5** to build and run the draft. A fresh checkout can follow the setup steps below and point the VS Code task and launch configuration at its own tool paths.

1. **Download Godot Engine .NET 4.7.2 for Windows x86-64** from the official release page. Extract the whole ZIP to a folder and run the editor executable from there. The .NET edition includes C# support. To verify the download on Windows, right-click the extracted `.exe` and open **Properties → Digital Signatures**. Godot lists **Prehensile Tales B.V.** as the signer. Select the signature and open **Details** to check that Windows reports it as valid. [Official 4.7.2 download](https://godotengine.org/download/archive/4.7.2-stable/) · [Godot Windows signing information](https://godotengine.org/download/windows/)
2. **Install the .NET 10 LTS SDK for Windows x64** using Microsoft's SDK installer. Open a fresh PowerShell window and run `dotnet --info` to confirm it is available. In your existing VS Code installation, add Microsoft's **C#** extension. [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) · [Microsoft C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)
3. Open the repository in VS Code and create the Godot project in `game/`. Choose the Compatibility renderer for the initial 2D build. Add a `Node2D` test scene and a C# script to generate the project file. Keep Godot's generated desktop target framework for the first build. The 4.7.2 generator defaults to `net8.0`. Target a compatible framework in the simulation library and verify the SDK, editor, and export combination on day one. [Godot 4.7.2 project generator](https://raw.githubusercontent.com/godotengine/godot/4.7.2-stable/modules/mono/editor/GodotTools/GodotTools.ProjectEditor/ProjectGenerator.cs)
4. In Godot, select VS Code under **Editor Settings → Dotnet → Editor → External Editor**. Use a `.vscode/tasks.json` task to build the game `.csproj` and a `.vscode/launch.json` configuration to start the Godot executable with `--path` pointing at `game/`. Prove that a breakpoint hits in `_PhysicsProcess`. [Godot VS Code configuration](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_basics.html#visual-studio-code)
5. In **Editor → Manage Export Templates**, install the matching Windows x86-64 .NET templates. In **Project → Export**, add a Windows Desktop x86-64 preset. Export the test scene as `ThrashFighters.exe` into a build folder outside `game/` and launch it from File Explorer. Commit the project settings and export preset. Ignore editor caches and build outputs. [Godot export templates](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_projects.html#export-templates) · [Godot Windows export guide](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_windows.html)

### Ownership and licensing

Godot's contributors hold the copyright to the engine code. The Godot Foundation is a Dutch nonprofit that funds development and holds the Godot trademarks. You keep the rights to your original Thrash Fighters code and art. Godot's MIT license permits commercial release without engine royalties. Include Godot's copyright and MIT license text and the applicable third-party notices in a `THIRD_PARTY_NOTICES.txt` file in the game ZIP. [Godot governance](https://godotengine.org/governance/) · [Godot license](https://godotengine.org/license/#your-game) · [Godot license compliance guide](https://docs.godotengine.org/en/4.7/about/complying_with_licenses.html)

### Standalone build and launcher support

1. **Standalone package.** Zip the entire exported build folder so the executable stays together with its game data and any .NET files produced by the export. Configure the export preset to include the fighter, stage, and move JSON. Read that data through Godot's `res://` paths so it works from the exported package. [Godot export guide](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_projects.html)
2. **Launcher entry point.** Configure the launcher to open `ThrashFighters.exe` by its full installed path. A store launcher can use the same build. If we want a dedicated Thrash Fighters launcher, add a small C# desktop app with a **Play** button after the first standalone build works.
3. **Shared player settings.** Keep controller mappings, display settings, and replays under Godot's `user://` directory. The game reads the same settings whether started directly or from a launcher. A game update can replace the installed files while preserving the player data. [Godot file paths](https://docs.godotengine.org/en/4.7/tutorials/io/data_paths.html)
4. **Release check.** Extract the ZIP to a fresh folder on a Windows machine or VM without the development tools. Launch the game by double-clicking it, then from a shortcut or launcher. Verify the stage and fighter data load, confirm a controller setting persists across both launch paths, and check that the ZIP contains `THIRD_PARTY_NOTICES.txt`.

## First playable build

- **Format** — Local 1v1, four stocks, an eight-minute timer, percent-based knockback, blast zones, respawns, and a rematch button. Use one simple stage with a flat main surface, grabbable ledges, and three pass-through platforms. Add a flat training layout for physics measurements.
- **Kitsune** — Light and quick on the ground, three-tick jump squat as a starting point, a quick tail aerial, a rising foxfire recovery, and a slippery wavedash that carries into spacing changes.
- **Oni** — Heavy with a longer jump squat, shorter slide under higher traction, slower startup on big swings, and a rising horn charge for recovery. Give Oni a quick close-range attack so the prototype can test pressure and interruption from both sides.
- **Minimum kit per fighter** — A jab, a tilt, a dash attack, three aerials with different landing windows, a grab and throw, and an up special. Build one complete interaction at a time, starting with a grounded hit, a landing aerial, and a hit offstage.
- **Training mode** — Reset positions, set percent, choose dummy responses, pause and advance one tick, and display input history, collision shapes, and state timers. Build this alongside movement so every later mechanic has a way to be inspected.

Tengu and Yuki-onna are good candidates once the first matchup works. Tengu could emphasize aerial drift and platform routes, while Yuki-onna could use long, deliberate spacing tools. These are original game interpretations. Use museum collections for the visual research behind each design. [Japan Yōkai Museum exhibition catalog](https://miyoshi-mononoke.jp/wp/wp-content/uploads/2020/10/%E5%B1%95%E7%A4%BA%E7%9B%AE%E9%8C%B2%E3%80%8C%E4%BA%AC%E9%83%BD%E3%81%8B%E3%82%89%E3%82%84%E3%81%A3%E3%81%A6%E3%81%8D%E3%81%9F%E5%A6%96%E6%80%AA%E3%81%9F%E3%81%A1%E3%80%8D_%E6%97%A5%E8%8B%B1.pdf) · [Ferenc Hopp Museum Kitsune print](https://hoppmuseum.hu/en/collection/pdfexport/31063-fox-fairy-disguised-as-a-nun-or-wandering-monk)

## Simulation architecture

```text
Thrash Fighters/
  game/                        Godot project, scenes, input, sprites, UI, sound
    Data/                      Fighter, move, and stage JSON
  src/ThrashFighters.Sim/       C# library for match rules, movement, and combat
  tests/ThrashFighters.Tests/   Simulation tests and recorded input fixtures
```

Reference the simulation library from the game and test projects. Keep the full gameplay state in the library. A single Godot match node calls `MatchSimulation.Step(player1Input, player2Input)` from `_PhysicsProcess` at **60 ticks per second**. Each call advances one game tick, about 16.67 ms. The render side reads the state to position sprites and play effects. Use custom collision for fighter movement and combat. Keep Godot bodies and collision callbacks out of decisions that affect frame timing. [Godot fixed-tick documentation](https://docs.godotengine.org/en/4.7/tutorials/physics/interpolation/physics_interpolation_introduction.html)

Run the simulation checks from PowerShell at the repository root.

```powershell
$env:DOTNET_CLI_HOME = (Resolve-Path '.tools').Path
$env:NUGET_PACKAGES = Join-Path $env:DOTNET_CLI_HOME 'nuget/packages'
& '.tools/dotnet/dotnet.exe' run --project 'tests/ThrashFighters.Tests/ThrashFighters.Tests.csproj'
```

### State and timing

Store position, movement velocity, launch velocity, facing, current action and action tick, percent, jumps, active surface, hitlag, hitstun, invulnerability, input history, and active attack IDs in the match state. Use integer subpixels for position and velocity, with a documented scale such as 1,024 subpixels per stage unit. Use deterministic rounding and widened intermediates for multiplication and geometry. Establish stable ordering for contacts and use a seeded random generator for any gameplay randomness.

1. Latch both players' inputs at the same tick boundary. Update press and release edges and per-mechanic input history.
2. Check action transitions and interrupt windows. A fighter in hitlag can still have eligible inputs processed. Advance motion and action timers according to the state's freeze rules.
3. Integrate eligible movement, sweep against stage geometry, and produce contacts for landings, walls, and ledges.
4. Build the active hitboxes, hurtboxes, shields, and grabs for the relevant action tick. Collect interactions for both fighters before committing their outcomes. Apply explicit precedence for same-tick landing, hit, grab, and ledge cases. Establish the initial rules with reference traces and cover them with tests.
5. Commit the resolved state, advance the match tick, and publish the snapshot and presentation events.

Define whether a new action enters on tick 0 and whether each boundary is inclusive. For example, a move can use active ticks `[5, 8)`, which means 5, 6, and 7. Keep match tick, action tick, and hitlag counters explicit. Those clocks matter when an aerial hits just above a platform or a defensive input arrives during hitlag.

## Movement and controller feel

| Mechanic | Implementation and feel check |
| --- | --- |
| Walk, dash, run, and dash dance | Distinguish a slow tilt from a quick stick flick. Give initial dash an explicit reversal window, and give committed run its own turnaround and skid rules. Check reversals on the last valid tick and one tick later. |
| Jumping and aerial drift | Set jump squat per fighter. Track whether jump is released before takeoff for short-hop selection. Tune takeoff velocity, gravity, terminal fall speed, air acceleration, air speed cap, and drift separately. Enable fast-fall only in eligible descending states and give it its own speed. |
| Directional air dodge and wavedash | Use the stick direction to set air-dodge velocity. When a downward dodge meets a floor, enter air-dodge landing recovery and preserve along-surface momentum. Apply fighter traction while sliding. Test shallow and steep angles, both directions, and the first legal airborne tick. Different angles should change the slide, and the two fighters should cover visibly different distances. [Melee air-dodge reference](https://github.com/doldecomp/melee/blob/master/src/melee/ft/kinds/ftCommon/ftCo_EscapeAir.c) |
| Waveland and platform drop | Use the same air-dodge landing rule for wavelands. Model one-way platforms with a descending crossing test using the previous and proposed foot positions. Give drops a temporary exemption for the platform being dropped through. Check high-speed crossings, platform endpoints, and falling past one platform onto another. Add shield drop once analog thresholds and platform collision are stable. |
| L-cancel and autocancel | Aerial data defines when the move incurs landing lag. Autocancel windows use normal landing behavior. During an eligible landing, consult a fresh trigger press in a seven-tick starting window and reduce the move's landing lag, for example from 14 ticks to 7. Confirm landing-tick inclusion, odd-duration rounding, and hitlag behavior against reference captures. Test a held trigger and each tick at the window boundary. [Melee aerial landing reference](https://github.com/doldecomp/melee/blob/master/src/melee/ft/kinds/ftCommon/ftCo_LandingAir.c) |
| Ledges and recovery | Define grab ranges, approach requirements, occupied ledge behavior, regrab rules, and intangibility windows. Add ledge get-up, drop, and jump. Check simultaneous claims and whether each fighter can contest and escape the ledge at different percents. |

Use three independent collision representations. The **environment collision box** meets floors and walls, **hurtboxes** describe where fighters can be hit, and **hitboxes** describe what a move can hit. Add a distinct shield shape. Define stage surfaces as segments and keep them aligned with the art. Start with horizontal floors and vertical walls. Add slopes after flat-stage edge cases pass.

Capture per-device input with normalized, quantized stick axes, secondary stick axes, trigger values, and held and pressed buttons. Preserve brief press and release edges that occur between simulation ticks. Expose raw and processed stick values for calibration. Use trigger hysteresis so input near a threshold stays stable. Track separate windows for techs and L-cancels even if the same button can trigger both. Support keyboard testing and common USB controllers first. Test a real GameCube controller and adapter early if it will be a primary input device. Godot uses SDL 3 for desktop controllers, and adapter behavior depends on the device and mode. [Godot controller documentation](https://docs.godotengine.org/en/4.7/tutorials/inputs/controllers_gamepads_joysticks.html)

## Combat and character data

- **Fighter data** owns dash and run speed, jump squat, short-hop and full-hop velocities, traction, air acceleration and cap, gravity, terminal speed, fast-fall speed, weight, collision dimensions, and recovery parameters. Tune weight and fall speed independently. They influence different parts of launch and combo behavior.
- **Move data** owns startup, active ticks, recovery, interrupt windows, autocancel ranges, landing lag, shape offsets, damage, launch angle, base knockback, knockback growth, and hitlag modifiers. Each attack instance tracks which targets it has hit. Multi-hit moves declare when they may hit a target again. Begin with circles or capsules and add complexity when a move calls for it.
- **Hit resolution** reads the target's percent and weight to calculate launch strength using the move's base knockback and growth. Pick and test a consistent rule for when damage enters that calculation. Keep launch velocity distinct from player-controlled velocity. Apply hitlag, hitstun, launch decay, and tumble as separate rules. Test low-, mid-, and high-percent outcomes with each fighter as the target.
- **Defensive interactions** include shield stun and pushback, aerial pressure into shield, grabs and throws, floor and wall techs with their own timing and lockout, and directional influence (DI) at launch. Add smash directional influence (SDI) during hitlag as a separate positional adjustment. Test the same combo route with several DI directions and both fighter profiles.
- **Simultaneous contacts** need a small written rules table. Start with attack versus hurtbox, shield versus hit, grab versus shield, and two valid strikes on the same tick. Add eligible clanks, crouch-cancel behavior, and stale moves during the fidelity pass. Cover each rule with a two-fighter fixture so processing order cannot decide a trade.

For the first aerial, try startup ticks `0–4`, active ticks `5–7`, and aerial recovery from tick `8` onward. Give it 14 ticks of landing lag in its landing-lag window. Put that move on both placeholder fighters initially and compare what each can follow up with after a short-hop fast-fall. The frame counts are starting values for Thrash Fighters. Tune after watching the same interaction at full speed and frame by frame.

## Measure the feel against Melee

Use NTSC 1.02 as the initial reference and record the rules and controller fixes in use for each capture. Collect controlled Slippi traces for a short-hop aerial, dash reversal, shallow and steep wavedash, platform waveland, successful and missed L-cancel, and a launch at several percents with neutral and angled DI. Record pre-tick input and post-tick position, percent, and action state. Compare event ticks directly and normalize distances using stage geometry when the coordinate scales differ. Use the decompilation as a research reference for edge cases, then implement our own code, moves, stages, and assets. [Slippi replay format](https://github.com/project-slippi/slippi-wiki/blob/master/SPEC.md) · [Melee NTSC 1.02 reference project](https://github.com/doldecomp/melee)

For each comparable mechanic, keep a fixture with inputs, starting state, and expected outcomes. Aim to match reference transition timing within one tick in controlled scenarios, then investigate the boundary differences. Measure jump apex height and time, wavedash distance and first actionable tick, aerial landing recovery, and launch trajectories. For original moves, use playtests to judge whether comparable decisions are available and whether new routes are worth learning.

Run the same recorded inputs twice and compare per-tick state hashes. Compare fixture outcomes at 60 Hz and a higher render refresh rate, and test a full match in the exported Windows build. Track input-to-first-visible-response on real hardware during the first movement milestone. Check the impact of VSync, rendering interpolation, and input accumulation with measurements. [Godot input accumulation documentation](https://docs.godotengine.org/en/4.7/classes/class_input.html#class-input-property-use-accumulated-input)

## Pixel art and readability

Prototype at a **640 × 360** viewport with nearest-neighbor filtering and integer scaling. Anchor sprites at their feet and snap rendered positions to the pixel grid after applying the camera transform. Keep the simulation at subpixel precision. Start with a fixed gameplay camera, then try discrete zoom levels or a restrained following camera after the stage reads well at match distance. Check fast pans and diagonal movement for pixel shimmer. [Godot pixel-art scaling guide](https://docs.godotengine.org/en/stable/tutorials/rendering/multiple_resolutions.html#common-use-case-scenarios)

Draw distinct anticipation, active, and recovery poses. A sprite pose can hold for several ticks while movement continues at 60 Hz. Drive sprite and effect timing from the move timeline. Give priority to readable silhouettes, strong platform edges, and clear feedback for hitlag, shielding, and landings. A restrained leaf burst or dust effect can make a clean wavedash readable at match speed.

## Build order and exit criteria

| Milestone | Deliverable | Ready when |
| --- | --- | --- |
| 1. Toolchain | Godot scene, C# build, VS Code breakpoint, standalone Windows export | `ThrashFighters.exe` opens from File Explorer and a shortcut. The debug build steps through a physics tick. |
| 2. Movement lab | One placeholder fighter, main floor, one platform, input overlay, frame advance | Walk, dash dance, short-hop, drift, fast-fall, wavedash, and waveland repeat correctly in recorded inputs. |
| 3. First exchange | Second controller, dummy recording, one ground attack and aerial, percent, knockback, hitlag, and L-cancel | Both players can trade and punish. Frame advance shows why each hit and landing outcome happened. |
| 4. Complete local match | Kitsune and Oni profiles, minimum move kits, shield, throws, DI, techs, ledges, stocks, and reset | Two players can finish and rematch a game. The same starter creates different follow-ups against each fighter. |
| 5. Fidelity and playtest build | Reference fixtures, platform and ledge edge cases, readable sprites, camera, HUD, audio, and a Windows ZIP | Playtesters can start the game on a clean machine. Experienced Melee players can reproduce the core movement sequences and give specific feedback on timing, spacing, and responsiveness. |
| 6. Expansion | Roster, stages, and a rollback prototype | Snapshot restore plus input resimulation reproduces the same state hashes. For rollback, test simulated delay, jitter, and packet loss before building matchmaking. |

The first coding target is a rectangle that can dash-dance, short-hop, and wavedash onto a flat floor from a real controller. Put the current state, stick angle, velocity, and landing recovery on screen. A recorded input sequence should reproduce the same slide distance and first actionable tick every time. Once that works, add a second fighter with different traction and jump squat and prove the movement system produces two distinct feels from data alone.
