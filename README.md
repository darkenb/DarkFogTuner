# DarkFogTuner

Change Dark Fog difficulty while playing. Press `Ctrl+Shift+K` in a game to open the panel, move a slider, release the mouse and it applies. The Easy, Normal, Hard and Extreme buttons set everything at once. The hotkey and language are in `BepInEx/config/bug.dsp.darkfogtuner.cfg`.

[Türkçe açıklama](https://github.com/darkenb/DarkFogTuner/blob/main/README.tr.md)

| Setting | Range | What it does |
|---|---|---|
| Aggressiveness | Dummy to Rampage | The game's six levels |
| Attack frequency | ×0.1 to ×100 | Threat needed for an attack, on hives and ground bases |
| Fleet size | ×0.1 to ×20 | Units per attack |
| Fleet growth | 0 to ×10 | How fast attacks grow wave by wave; 0 stops growth |
| Spread to other stars | 0 to ×20 | Chance that seeds launch; 0 stops new launches |
| Build and replication speed | ×0.1 to ×10 | Hive and base construction and unit production |
| Power threat, combat threat | ×0.01 to ×100 | The game's threat factors. Power threat only feeds ground bases; space threat from Dyson spheres follows attack frequency |
| Experience from combat | ×0.01 to ×20 | The game's experience factor |
| Unit capacity | ×1 to ×20 | Units a hive or base may hold. Also raises the space attack cap, up to 1440 |
| Free unit production | ×1 to ×50 | Extra replicator passes per tick, costing the hive no matter or energy |
| Enemy damage | ×0.1 to ×100 | Damage to buildings, the mecha and planetary shields |
| Enemy durability | ×0.1 to ×100 | Divides damage taken by Dark Fog units and buildings. Works on top of level 30 |
| New hive level, head start | 0 to 30, 0 to 4 | Only hives founded from now on |

The game's own settings are stored in your save. Attack frequency, fleet size, fleet growth, spread, unit capacity, production, enemy damage and durability are stored per galaxy in the config file, so saves of the same galaxy share them.

## They Come From Void (DSP_Battle)

Loading, saving and space attacks worked in a game with this mod. Enemy damage and enemy durability have not been tested in the game at values other than ×1 yet. Both mods patch the same damage methods, so their effects may combine in unexpected ways, for example relic damage bonuses or shields behaving differently. If you see this, set both back to ×1 and [report it](https://github.com/darkenb/DarkFogTuner/issues).

## Limits

- Hive count and the hives present at the start are generated from the new game settings and cannot change later; use UniverseGenTweaks on the new game screen for those.
- An attack can only use units that exist, so fleet size needs unit capacity and production to show. A space attack stops at 1440 units per hive, a ground attack at 180 per base (the game's unit slots).
- Very high capacity and production can lower the frame rate.
- Spread only covers seeds travelling to other stars. Landings on planets in the same system follow build speed.
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
