# DarkFogTuner

Change Dark Fog difficulty while playing, in the game's own window style. The panel is available only in a running game, not in the Esc menu or on the main menu. Press `Ctrl+Shift+K` in a game to open the panel, move a slider, release the mouse and it applies. The Easy, Normal, Hard and Extreme buttons set everything at once. The hotkey is in `BepInEx/config/bug.dsp.darkfogtuner.cfg`.

Each slider has a mark at the Normal value. A bar runs from that mark: orange means harder than Normal, blue means easier. Values at Normal are dimmed, and the button of the preset you are on is highlighted.

Below the sliders, orange hints say what a combination of settings needs from the others while you drag, for example that more ground base landings also need spread and build speed.

[Türkçe açıklama](https://github.com/darkenb/DarkFogTuner/blob/main/README.tr.md)

| Setting | Range | What it does |
|---|---|---|
| Aggressiveness | Dummy to Rampage | The game's six levels |
| Attack frequency | ×0.1 to ×100 | Threat needed for an attack, on hives and ground bases |
| Fleet size | ×0.1 to ×20 | Units per attack |
| Fleet growth | 0 to ×10 | How fast attacks grow wave by wave; 0 stops growth |
| Spread to other stars | 0 to ×20 | Chance that seeds launch; 0 stops new launches |
| Ground bases landing | 0 to ×20 | How often hives send relays to found ground bases on planets of their star; 0 stops new landings |
| Ground bases per planet | ×1 to ×5 | Relays a hive may have on each planet at once (the game allows 7, fewer in stars with many hives) |
| Build and replication speed | ×0.1 to ×10 | Hive and base construction and unit production |
| Power threat, combat threat | ×0.01 to ×100 | The game's threat factors. Power threat only feeds ground bases; space threat from Dyson spheres follows attack frequency |
| Experience from combat | ×0.01 to ×20 | The game's experience factor |
| Unit capacity | ×1 to ×20 | Units a hive or base may hold. Also raises the space attack cap, up to 1440 |
| Free unit production | ×1 to ×50 | Extra replicator passes per tick, costing the hive no matter or energy |
| Enemy damage | ×0.1 to ×100 | Damage to buildings, the mecha and planetary shields |
| Enemy durability | ×0.1 to ×100 | Divides damage taken by Dark Fog units and buildings. Works on top of level 30 |
| New hive level, head start | 0 to 30, 0 to 4 | Only hives founded from now on |
| Threat level | 0 to 100% | Sets the attack meter of every hive and ground base once, when you release the slider; 100% starts their attacks. Shows the highest meter. Not stored; Passive and lower never attack |

The game's own settings are stored in your save. Attack frequency, fleet size, fleet growth, spread, ground base landing and per-planet limit, unit capacity, production, enemy damage and durability are stored per galaxy in the config file, so saves of the same galaxy share them.

## They Come From Void (DSP_Battle)

Loading, saving and space attacks work in a game with this mod. Both mods patch the same damage methods, so their effects may combine in unexpected ways, for example relic damage bonuses or shields behaving differently. If you see this, set both back to ×1 and [report it](https://github.com/darkenb/DarkFogTuner/issues).

## Limits

- Hive count and the hives present at the start are generated from the new game settings and cannot change later; use UniverseGenTweaks on the new game screen for those.
- An attack can only use units that exist, so fleet size needs unit capacity and production to show. A space attack stops at 1440 units per hive, a ground attack at 180 per base (the game's unit slots).
- Very high capacity and production can lower the frame rate.
- Ground bases come only from hives in the same star system. Planets of stars without a hive stay empty until a seed founds a hive there, so raise spread as well; with Galactic Scale's many stars most systems start without one.
- Landing needs idle relays: a hive builds them with build speed, so very high landing values run out of relays.
- Lowering difficulty is recorded the same way as the game's own change, so metadata follows the lowest value reached. Unlike the game's own change, no metadata is charged.
- Build speed and combat threat are also written to the save's game description. Not built for multiplayer. Removing the mod keeps the save's game settings, and attack thresholds return to normal after each hive's next attack.

## Building from source

You need a current .NET SDK and the .NET Framework 4.7.2 targeting pack. The game, Unity and BepInEx assemblies are referenced from your own installation and are not part of this repository.

```text
dotnet build -c Release "-p:DSPGamePath=D:\Games\Dyson Sphere Program" "-p:BepInExCorePath=C:\path\to\BepInEx\core"
```

The output is `bin/Release/net472/DarkFogTuner.dll`. Copy it to `BepInEx/plugins/DarkFogTuner/`.

## License

MIT, see [LICENSE](LICENSE).
