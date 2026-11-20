# 🎰 Unity Slot Machine

A classic three-reel slot machine built in Unity (2022.3 LTS, uGUI) for the internship assignment.
Pull the lever (or press **Space**), watch the reels spin, and match all three symbols to win.

## Game overview

| | |
|---|---|
| **Reels** | 3 reels, 1 payline (centre row) |
| **Symbols** | 🍒 Cherry · 🔔 Bell · BAR · **7** (from the supplied art) |
| **Win rule** | All three reels show the same symbol |
| **Start balance** | 1,000 credits |
| **Bets** | 5 / 10 / 25 / 50 / 100 (arrow buttons) |

### Paytable

Payout = **bet × multiplier**.

| Symbol | Weight (of 10) | Triple multiplier |
|---|---|---|
| 7 (lucky) | 1 | ×100 – **JACKPOT** |
| BAR | 2 | ×30 |
| Bell | 3 | ×10 |
| Cherry | 4 | ×5 |

Maths (calculated in `PayoutEvaluator`, asserted in unit tests, printed to the console on Play):

- Hit frequency (any triple): **10.0 %**
- Base RTP: **93.0 %**
- Free-spin chance (exactly two 7s): **2.7 %**
- Total theoretical RTP including free spins: **≈ 95.6 %**

I cross-checked the closed-form numbers with a 2-million-spin Monte-Carlo simulation (≈ 96 %, within the variance expected from the ×100 jackpot).

### Controls

| Action | Input |
|---|---|
| Spin | Click the lever, or press **Space** |
| Change bet | ◀ / ▶ arrows in the bottom panel |
| Auto-play | **AUTO** button (stops on a jackpot or when out of credits) |
| Mute | **SOUND** button |
| Dismiss win banner | Click it, the ✕, or wait |

## Bonus features

- **Free-spin bonus** – exactly two 7s (without a full line) awards a free spin at your current bet.
- **Jackpot celebration** – a full line of 7s triggers confetti, screen shake and a fanfare.
- **Auto-play** with sensible stop conditions.
- **Adjustable bet** with guard rails (can't raise the bet above your balance; it auto-lowers when funds run low).
- **Out-of-credits flow** – YES / NO restart dialog (uses the supplied popup + Yes/No art).
- **Live paytable** generated from the config, so the displayed payouts can never drift from the real ones.
- **Animator-driven payline glow**, win-pulse on the winning symbols, counting-up balance, motion-blur stretch on fast reels.
- **Sound effects** (synthesised, committed as `.wav`) with a mute toggle.

## Running the project

### Open in Unity
1. Install **Unity 2022.3 LTS** with the **WebGL Build Support** module.
2. Open this folder in Unity Hub (*Add project from disk*).
3. When prompted – or via **Slot Game ▸ Build Everything** – the scene, prefabs, animation clips and data assets are generated and wired automatically.
4. Open `Assets/Scenes/SlotGame.unity` and press **Play**.

> Why a generator? Building the scene from code makes the setup reproducible and reviewable in Git instead of being hidden in hand-clicked scene state. Re-running it is safe.

### Run the tests
*Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All* (RNG distribution, payout rules, wallet, simulated RTP).

### Run the WebGL build
The build lives in `Build/WebGL`. Browsers block WebGL from `file://`, so serve it over HTTP:

```bash
cd Build/WebGL
python3 -m http.server 8000
# then open http://localhost:8000
```

To (re)generate it: **Slot Game ▸ Build WebGL** (compression is disabled so any static host works, e.g. GitHub Pages or itch.io).

## Project structure

```
Assets/
├── Scripts/
│   ├── Core/      SlotGameManager, PayoutEvaluator, WalletModel, WeightedRandomService, SlotConfig, SymbolDefinition ...
│   ├── Reels/     ReelController (spin / decelerate / settle animation)
│   ├── UI/        HudView, ResultPopup, LeverController, PaylineHighlight, PaytableView, ConfettiBurst, ScreenShake
│   ├── Audio/     AudioManager
│   └── Editor/    SlotSceneBuilder (one-click setup + WebGL build), SpriteImportPostprocessor
├── Prefabs/       SymbolCell, Reel, ResultPopup   (generated)
├── Animations/    Payline clips + Animator controller (generated)
├── UI/            Symbols, Machine, Buttons, Popup, Background (supplied art, buttons sliced per state)
├── Sounds/        spin loop, reel stop, lever, click, win, jackpot, bonus, game over
├── Data/          SlotConfig + SymbolDefinition assets (generated, designer-editable)
├── Scenes/        SlotGame.unity (generated)
└── Tests/EditMode NUnit tests
```

## Thought process & architecture

**1. Separate rules from presentation.** `PayoutEvaluator`, `WalletModel` and `WeightedRandomService` are plain C# with no scene dependencies, so they are unit-tested. MonoBehaviours only present state.

**2. Decide first, animate second (fairness).** When a spin starts, `SlotGameManager` asks the RNG for each reel's symbol *before* anything moves. `ReelController.StopAt(symbol)` then animates to that outcome. Animation timing can never influence the result, and reels are independent draws.

**3. The RNG.** `WeightedRandomService` wraps `System.Random` (uniform `Next(int)`, no modulo bias) with a seed mixed from time, GUID and tick count; a fixed seed in `SlotConfig` gives reproducible runs for debugging. It sits behind `IRandomService` so tests can substitute it.

**4. Reel animation.** Each reel keeps a *virtual scroll position* measured in symbols and pools just five cell images. Phases: ease-in spin-up → constant speed → **ease-out cubic deceleration** whose duration is derived from the entry speed (`T = 3·distance / speed`) so slow-down starts without a visible jerk → a small **overshoot and settle** for a physical feel. The landing symbol is forced onto the target row only after it is off-screen, and the position is wrapped by the strip length after every stop to avoid float drift. Reels stop left-to-right with a stagger for suspense.

**5. Data-driven design.** Weights, multipliers, bets and timings are `ScriptableObject`s, so balancing needs no code changes, and the paytable UI reads the same data.

**6. State machine.** `Idle → Spinning → Resolving → Idle / GameOver`. Input is ignored outside `Idle`, which prevents double-spins and mid-spin bet changes.

**7. Asset use.** `Reel_Windows` sits behind the reels, `Machine_Frame` in front (its windows are transparent), and the up/down lever images swap on a click area placed over the knob. All layers share one 816×624 canvas so they align exactly.

## Assumptions & limitations

- Single payline, three reels (matches the supplied artwork).
- Credits are virtual and reset on reload (no persistence).
- Sound effects are generated tones, not studio recordings.
- Uses Unity's legacy input (`Input.GetKeyDown`) and legacy UI `Text`, which keeps the project free of extra package setup.

## Credits

Machine, symbol, button and popup art supplied with the assignment. Everything else is original.
