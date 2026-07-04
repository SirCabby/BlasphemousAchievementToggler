# Blasphemous Achievement Toggler

A BepInEx plugin that adds a **toggle panel to the in-game Achievements page**, letting you flip
each achievement **on or off** (and *Unlock all* / *Lock all*).

**Steam-safe:** it drives the game's **local** achievement records — the same list the in-game
Achievements page reads — not the Steam API. It never permanently earns or clears a Steam
achievement.

## How it works (discovered)

The Achievements tab of `Gameplay.UI.Others.MenuLogic.ExtrasMenuWidget` shows one display-only row
per achievement. Each row's locked/unlocked look is derived, inside
`AchievementsManager.GetAllAchievements()`, from the **local achievements list**
(`Framework.Achievements.LocalAchievementsHelper`, persisted to `local_achievements.data`). So that
local list is the real switch for the page.

When the Achievements page is open, this mod overlays an IMGUI panel with a toggle per achievement:

- **Unlock** → `LocalAchievementsHelper.SetAchievementUnlocked(id)` (adds it to the local list and
  saves), and sets the in-memory `Achievement.Progress = 100` / `CurrentStatus = UNLOCKED`.
- **Lock** → removes the id from `LocalAchievementsHelper`'s cache and calls
  `SaveLocalAchievements()`, and sets `Progress = 0` / `CurrentStatus = LOCKED`.

Then it rebuilds the page (`ExtrasMenuWidget.CreateAchievements()`) so the row updates immediately.

The game's normal *grant* path is gated to actual gameplay modes
(`GameModeManager.ShouldProgressAchievements()`), so it wouldn't work from the menu — driving the
local list directly sidesteps that while keeping the page and the local file in sync.

## Build & install

- Game: Steam Blasphemous, Unity 2017.4 (**Mono CLR 2.0**) → plugin targets **net35**.
- Requires BepInEx 5.x (x64, Mono) installed in the game folder.
- Copy `GameDir.local.props.example` to `GameDir.local.props` and set `<GameDir>` to your
  install (this file is gitignored), then `dotnet build -c Release`.
- Copy `bin/Release/BlasAchievementToggler.dll` to `Blasphemous/BepInEx/plugins/`.

## Usage

1. Launch the game. Open **Extras → Achievements**. The toggle panel appears automatically.
2. Click **Unlock** / **Lock** on any row, or **Unlock all** / **Lock all**. Use the **Filter** box
   to find an achievement by name or id.
3. The page updates immediately and the change is saved to `local_achievements.data`.

## Controls (rebind in `BepInEx/config/local.blasphemous.achievementtoggler.cfg`)

- **F8** — show / hide the panel (it also auto-shows on the Achievements page; that can be turned
  off in the config).

## Notes

- The panel uses the mouse (it forces the cursor visible while open), so click the buttons directly.
- Because this changes the **local** records only, it's fully reversible — toggling off genuinely
  re-locks the achievement in-game. Steam is not involved.
