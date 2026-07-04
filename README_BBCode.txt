[size=6][b]Blasphemous Achievement Toggler[/b][/size]

A lightweight mod that adds a [b]toggle panel to the in-game Achievements page[/b], letting you turn each achievement [b]on or off[/b] — individually, or all at once.

This mod is open source!  Check it out at [url=https://github.com/SirCabby/BlasphemousAchievementToggler]https://github.com/SirCabby/BlasphemousAchievementToggler[/url]

[size=5][b]What it does[/b][/size]
[list]
[*]Open [b]Extras → Achievements[/b] and a panel appears with a [b]toggle button for every achievement[/b].
[*][b]Unlock[/b] or [b]Lock[/b] any achievement, or use [b]Unlock all[/b] / [b]Lock all[/b].
[*][b]Filter[/b] box to quickly find an achievement by name or id.
[*]The page updates instantly and the change is saved.
[/list]

[size=5][b]Steam-safe[/b][/size]
This toggles the game's [b]local[/b] achievement records — exactly what the in-game Achievements page reads — [b]not[/b] Steam. It never permanently earns or clears a Steam achievement, and toggling off genuinely re-locks the achievement in game.

[size=5][b]Requirements[/b][/size]
[list]
[*]Blasphemous
[*]BepInEx 5.4.x (x64 / Mono) — installed in Step 1 below.
[/list]

[size=5][b]Step 1 — Install BepInEx (the mod loader)[/b][/size]
[list=1]
[*]Open the [url=https://github.com/BepInEx/BepInEx/releases]BepInEx releases page[/url] and download the latest [b]BepInEx 5.4.x[/b] for [b]x64 Windows[/b] — the file is named like [i]BepInEx_win_x64_5.4.x.zip[/i]. [b]Do not[/b] use the 6.x (BepInEx 6 / IL2CPP) builds.
[*]Find your game folder: in Steam, right-click [b]Blasphemous[/b] → [b]Manage[/b] → [b]Browse local files[/b]. It looks like:
[code]C:\Program Files (x86)\Steam\steamapps\common\Blasphemous[/code]
[*]Extract the contents of the zip [b]directly into that folder[/b] (next to [i]Blasphemous.exe[/i]). You should now see a [i]BepInEx[/i] folder, plus [i]winhttp.dll[/i] and [i]doorstop_config.ini[/i].
[*]Launch the game once, then quit. This first run creates the [i]BepInEx\plugins[/i] and [i]BepInEx\config[/i] folders.
[/list]

[size=5][b]Step 2 — Install this mod[/b][/size]
[list=1]
[*]Drop [b]BlasAchievementToggler.dll[/b] into the plugins folder:
[code]...\Blasphemous\BepInEx\plugins\[/code]
[*]Launch the game.
[/list]

[size=5][b]How to use it[/b][/size]
[list=1]
[*]Open [b]Extras → Achievements[/b]. The toggle panel appears.
[*]Click [b]Unlock[/b] / [b]Lock[/b] on any achievement (use the mouse — the cursor is shown while the panel is open).
[*]Press [b]F8[/b] any time to hide or show the panel.
[/list]

[size=5][b]Uninstall[/b][/size]
Delete [b]BlasAchievementToggler.dll[/b] from [i]BepInEx\plugins[/i]. Any toggles you already made stay as they were saved; the Achievements page returns to normal.

[size=5][b]Troubleshooting[/b][/size]
[list]
[*][b]Panel doesn't appear?[/b] Press [b]F8[/b] to force it on. Then open [i]BepInEx\LogOutput.log[/i] and look for [b][AchToggler] v1.0 ready=True[/b]; a warning there means the game's achievement code didn't match (e.g. after a game update).
[*][b]Nothing loads at all?[/b] Double-check you used BepInEx [b]5.4.x x64 (Mono)[/b], extracted it into the game folder (not a subfolder), and ran the game once before adding the DLL.
[/list]
