using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace DarkFogTuner
{
    public enum Language { Auto, English, Chinese, Turkish }

    [BepInPlugin("bug.dsp.darkfogtuner", "DarkFogTuner", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        const int Aggression = 0, Frequency = 1, FleetSize = 2, FleetGrowth = 3, Spread = 4, Growth = 5, PowerThreat = 6, BattleThreat = 7, BattleExp = 8, Capacity = 9, Production = 10, EnemyDamage = 11, EnemyHealth = 12, NewLevel = 13, NewGrowth = 14;
        const long NoGalaxy = -2;

        // Must be the game's exact values: any other aggressiveness maps to no EAggressiveLevel.
        static readonly float[] AggressionValues = { -1f, 0f, 0.5f, 1f, 2f, 3f };

        // en, zh, tr
        static readonly string[][] AggressionNames =
        {
            new[] { "Dummy", "假人", "Kukla" }, new[] { "Passive", "被动", "Pasif" }, new[] { "Torpid", "迟钝", "Uyuşuk" },
            new[] { "Normal", "正常", "Normal" }, new[] { "Sharp", "敏锐", "Keskin" }, new[] { "Rampage", "狂暴", "Öfkeli" },
        };

        class Row { public string En, Zh, Tr; public float Min, Max; public bool Log, ZeroAtLeft, Whole; }

        static readonly Row[] Rows =
        {
            new Row { En = "Aggressiveness", Zh = "攻击性", Tr = "Agresiflik", Min = 0, Max = 5, Whole = true },
            new Row { En = "Attack frequency", Zh = "攻击频率", Tr = "Saldırı sıklığı", Min = 0.1f, Max = 100, Log = true },
            new Row { En = "Fleet size", Zh = "舰队规模", Tr = "Filo büyüklüğü", Min = 0.1f, Max = 20, Log = true },
            new Row { En = "Fleet growth per wave", Zh = "舰队增长速度", Tr = "Filo büyüme hızı", Min = 0.1f, Max = 10, Log = true, ZeroAtLeft = true },
            new Row { En = "Spread to other stars", Zh = "星际扩散", Tr = "Yıldızlara yayılım", Min = 0.01f, Max = 20, Log = true, ZeroAtLeft = true },
            new Row { En = "Build and replication speed", Zh = "建造与复制速度", Tr = "Üreme ve inşa hızı", Min = 0.1f, Max = 10, Log = true },
            new Row { En = "Power threat (ground bases)", Zh = "能源威胁（地面基地）", Tr = "Enerji tehdidi (yer üsleri)", Min = 0.01f, Max = 100, Log = true },
            new Row { En = "Threat from combat", Zh = "战斗威胁", Tr = "Savaş tehdidi", Min = 0.01f, Max = 100, Log = true },
            new Row { En = "Experience from combat", Zh = "战斗经验", Tr = "Savaş deneyimi", Min = 0.01f, Max = 20, Log = true },
            new Row { En = "Unit capacity", Zh = "单位容量", Tr = "Birim kapasitesi", Min = 1, Max = 20, Log = true },
            new Row { En = "Free unit production", Zh = "免费单位生产", Tr = "Bedava birim üretimi", Min = 1, Max = 50, Log = true },
            new Row { En = "Enemy damage", Zh = "敌方伤害", Tr = "Düşman hasarı", Min = 0.1f, Max = 100, Log = true },
            new Row { En = "Enemy durability", Zh = "敌方耐久", Tr = "Düşman dayanıklılığı", Min = 0.1f, Max = 100, Log = true },
            new Row { En = "New hive level", Zh = "新巢穴等级", Tr = "Yeni kovan seviyesi", Min = 0, Max = 30, Whole = true },
            new Row { En = "New hive head start", Zh = "新巢穴发展度", Tr = "Yeni kovan gelişimi", Min = 0, Max = 4 },
        };

        // NaN keeps the current value. Order follows Rows.
        static readonly KeyValuePair<string[], float[]>[] Presets =
        {
            Preset("Easy", "简单", "Kolay", 2, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.2f, 0.2f, 0.2f, 1, 1, 0.5f, 0.5f),
            Preset("Normal", "正常", "Normal", 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1),
            Preset("Hard", "困难", "Zor", 4, 1.5f, 1.5f, 1.5f, 1.5f, 2, 2, 2, 2, 2, 2, 2, 2),
            Preset("Extreme", "极限", "Aşırı", 5, 10, 5, 5, 5, 10, 20, 20, 5, 10, 10, 5, 5),
        };

        static KeyValuePair<string[], float[]> Preset(string en, string zh, string tr, params float[] v) =>
            new KeyValuePair<string[], float[]>(new[] { en, zh, tr }, v.Concat(new[] { float.NaN, float.NaN }).ToArray());

        // Read by the patches from the game's worker threads.
        public static float AttackFrequency = 1f, FleetSizeFactor = 1f, FleetGrowthFactor = 1f, SpreadFactor = 1f, UnitCapacity = 1f, ProductionFactor = 1f, EnemyDamageFactor = 1f, EnemyHealthFactor = 1f;

        static ConfigEntry<KeyboardShortcut> hotkey;
        static ConfigEntry<Language> language;
        static ConfigFile config;
        static long galaxy = NoGalaxy;
        static bool open, dirty;
        static readonly float[] draft = new float[Rows.Length];
        static Rect window = new Rect(60, 120, 500, 0);
        public static bool MouseOverWindow;
        static Texture2D panelTexture;
        static GUIStyle panelStyle, labelStyle;

        void Awake()
        {
            config = Config;
            hotkey = Config.Bind("General", "Hotkey", new KeyboardShortcut(KeyCode.K, KeyCode.LeftControl, KeyCode.LeftShift), "Opens and closes the panel.");
            language = Config.Bind("General", "Language", Language.Auto, "Panel language. Auto follows the Windows language.");
            new Harmony("bug.dsp.darkfogtuner").PatchAll(typeof(Patches));
        }

        static bool InGame => GameMain.isRunning && !DSPGame.IsMenuDemo && GameMain.data != null && GameMain.data.gameDesc != null;

        void Update()
        {
            long key = InGame ? GameMain.data.GetClusterSeedKey() : NoGalaxy;
            if (key != galaxy) { galaxy = key; LoadGalaxy(); }
            if (key == NoGalaxy) { open = false; MouseOverWindow = false; return; }
            if (hotkey.Value.IsDown() && !VFInput.inputing)
            {
                open = !open;
                if (open) ReadDraft();
            }
            if (open && dirty && !Input.GetMouseButton(0)) Apply();  // on release: rescaling fleets mid-drag would stack rounding and clamps
            MouseOverWindow = open && WindowOnScreen().Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
        }

        static float Scale => Math.Max(1f, Screen.height / 1080f);

        static Rect WindowOnScreen() => new Rect(window.x * Scale, window.y * Scale, window.width * Scale, window.height * Scale);

        static ConfigEntry<float> Stored(string name) => config.Bind("Galaxy " + galaxy, name, 1f);

        static void LoadGalaxy()
        {
            dirty = false;
            bool none = galaxy == NoGalaxy;
            AttackFrequency = none ? 1f : Stored("AttackFrequency").Value;
            FleetSizeFactor = none ? 1f : Stored("FleetSize").Value;
            FleetGrowthFactor = none ? 1f : Stored("FleetGrowth").Value;
            SpreadFactor = none ? 1f : Stored("Spread").Value;
            UnitCapacity = none ? 1f : Stored("UnitCapacity").Value;
            ProductionFactor = none ? 1f : Stored("Production").Value;
            EnemyDamageFactor = none ? 1f : Stored("EnemyDamage").Value;
            EnemyHealthFactor = none ? 1f : Stored("EnemyHealth").Value;
        }

        public static float MetadataMultiplier(GameHistoryData history) => Traverse.Create(history).Property<float>("currentPropertyMultiplier").Value;

        static void ReadDraft()
        {
            ref CombatSettings c = ref GameMain.data.history.combatSettings;
            float aggressiveness = c.aggressiveness;
            draft[Aggression] = Array.FindIndex(AggressionValues, v => Math.Abs(v - aggressiveness) < 0.01f);
            draft[Frequency] = AttackFrequency;
            draft[FleetSize] = FleetSizeFactor;
            draft[FleetGrowth] = FleetGrowthFactor;
            draft[Spread] = SpreadFactor;
            draft[Growth] = c.growthSpeedFactor;
            draft[PowerThreat] = c.powerThreatFactor;
            draft[BattleThreat] = c.battleThreatFactor;
            draft[BattleExp] = c.battleExpFactor;
            draft[Capacity] = UnitCapacity;
            draft[Production] = ProductionFactor;
            draft[EnemyDamage] = EnemyDamageFactor;
            draft[EnemyHealth] = EnemyHealthFactor;
            draft[NewLevel] = c.initialLevel;
            draft[NewGrowth] = c.initialGrowth;
            dirty = false;
        }

        static void Apply()
        {
            dirty = false;
            GameHistoryData history = GameMain.data.history;
            ref CombatSettings c = ref history.combatSettings;
            EAggressiveLevel oldLevel = c.aggressiveLevel;
            float ratio = draft[FleetSize] / FleetSizeFactor;
            c.aggressiveness = AggressionValues[Math.Max(0, (int)draft[Aggression])];
            c.growthSpeedFactor = draft[Growth];
            c.powerThreatFactor = draft[PowerThreat];
            c.battleThreatFactor = draft[BattleThreat];
            c.battleExpFactor = draft[BattleExp];
            c.initialLevel = draft[NewLevel];
            c.initialGrowth = draft[NewGrowth];
            AttackFrequency = Stored("AttackFrequency").Value = draft[Frequency];
            FleetSizeFactor = Stored("FleetSize").Value = draft[FleetSize];
            FleetGrowthFactor = Stored("FleetGrowth").Value = draft[FleetGrowth];
            SpreadFactor = Stored("Spread").Value = draft[Spread];
            UnitCapacity = Stored("UnitCapacity").Value = draft[Capacity];
            ProductionFactor = Stored("Production").Value = draft[Production];
            EnemyDamageFactor = Stored("EnemyDamage").Value = draft[EnemyDamage];
            EnemyHealthFactor = Stored("EnemyHealth").Value = draft[EnemyHealth];

            // The game reads these two from gameDesc in hive cores, tinders and relays. Leave the rest alone: StarGen rebuilds the galaxy from maxDensity and initialColonize on every load.
            GameMain.data.gameDesc.combatSettings.growthSpeedFactor = c.growthSpeedFactor;
            GameMain.data.gameDesc.combatSettings.battleThreatFactor = c.battleThreatFactor;

            // Same bookkeeping as the game's own aggressiveness change, so metadata is not earned at a level that was left.
            history.minimalPropertyMultiplier = Math.Min(history.minimalPropertyMultiplier, MetadataMultiplier(history));
            history.minimalDifficulty = Math.Min(history.minimalDifficulty, c.difficulty);

            SpaceSector sector = GameMain.data.spaceSector;
            if (sector?.dfHives != null)
                foreach (EnemyDFHiveSystem first in sector.dfHives)
                    for (EnemyDFHiveSystem hive = first; hive != null; hive = hive.nextSibling)
                    {
                        if (hive.lancerAssaultCountBase >= 1f) hive.lancerAssaultCountBase = ClampAssault(hive.lancerAssaultCountBase * ratio);
                        hive.CalcFormsSupply();
                    }
            foreach (PlanetFactory factory in GameMain.data.factories)
                factory?.enemySystem?.CalcFormsSupply();
            // Recomputes every hive's and base's attack threshold (with our factor) and keeps their progress towards it.
            sector?.OnDFAggressivenessChanged(oldLevel, c.aggressiveLevel);
        }

        // A hive has 1440 lancer ports; capacity 1 keeps the game's own cap of 360.
        public static float FleetCap() => Math.Min(1440f, 360f * UnitCapacity);

        // The game resets the base below 1.
        public static float ClampAssault(float count) => Math.Max(2.5f, Math.Min(FleetCap(), count));

        public static int ScaleSupply(int supply) => (int)(supply * (double)UnitCapacity);

        // Enemy shots carry caster type Enemy; everything aimed at an enemy is the player's.
        public static int ScaleDamage(int damage, ETargetType target, ETargetType caster)
        {
            if (damage <= 0) return damage;
            if (target == ETargetType.Enemy) return EnemyHealthFactor == 1f ? damage : Math.Max(1, (int)(damage / (double)EnemyHealthFactor));
            if (caster != ETargetType.Enemy || EnemyDamageFactor == 1f) return damage;
            return (int)Math.Min(int.MaxValue, damage * (double)EnemyDamageFactor);
        }

        // 1e9 is the game's "never attacks" threshold for passive levels.
        public static int ScaleThreat(int threat) => threat >= 1000000000 ? threat : Math.Max(1, (int)(threat / AttackFrequency));

        public static int ScaleCount(int count) => count <= 0 ? count : Math.Max(1, (int)(count * FleetSizeFactor + 0.5f));

        public static double SpreadChance() => 0.0625 * SpreadFactor;

        // Two significant digits, so a log slider lands on readable values.
        public static float Snap(float v)
        {
            if (v <= 0f) return 0f;
            int digits = Math.Max(0, Math.Min(15, 1 - (int)Math.Floor(Math.Log10(v))));
            return (float)Math.Round(v, digits);
        }

        public static float ToSlider(float min, float max, bool log, float v)
        {
            if (!log) return (v - min) / (max - min);
            if (v <= min) return 0f;
            return (float)(Math.Log(v / min) / Math.Log(max / min));
        }

        public static float FromSlider(float min, float max, bool log, bool zeroAtLeft, bool whole, float t)
        {
            if (zeroAtLeft && t < 0.01f) return 0f;
            if (whole) return (float)Math.Round(min + (max - min) * t);
            return Snap(log ? (float)(min * Math.Pow(max / min, t)) : min + (max - min) * t);
        }

        static string T(string en, string zh, string tr)
        {
            Language l = language.Value;
            if (l == Language.Auto)
                l = Application.systemLanguage == SystemLanguage.Turkish ? Language.Turkish
                  : Application.systemLanguage == SystemLanguage.Chinese || Application.systemLanguage == SystemLanguage.ChineseSimplified ? Language.Chinese
                  : Language.English;
            return l == Language.Turkish ? tr : l == Language.Chinese ? zh : en;
        }

        static string T(string[] s) => T(s[0], s[1], s[2]);

        void OnGUI()
        {
            if (!open || galaxy == NoGalaxy) return;
            if (panelTexture == null)
            {
                panelTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                panelTexture.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.10f, 1f));
                panelTexture.Apply();
                panelStyle = new GUIStyle(GUI.skin.window);
                panelStyle.normal.background = panelStyle.onNormal.background = panelTexture;
                panelStyle.normal.textColor = panelStyle.onNormal.textColor = Color.white;
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.normal.textColor = Color.white;
            }
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
            window = GUILayout.Window(0x44465455, window, Draw, T("Dark Fog settings", "黑雾设置", "Kara Sis ayarları"), panelStyle);
        }

        static void Draw(int id)
        {
            if (!GameMain.data.gameDesc.isCombatMode)
            {
                GUILayout.Label(T("This save has no combat mode.", "此存档未开启战斗模式。", "Bu kayıtta savaş modu yok."), labelStyle);
            }
            else
            {
                GUILayout.BeginHorizontal();
                foreach (var preset in Presets)
                    if (GUILayout.Button(T(preset.Key)))
                    {
                        for (int i = 0; i < draft.Length; i++)
                            if (!float.IsNaN(preset.Value[i])) draft[i] = preset.Value[i];
                        dirty = true;
                    }
                GUILayout.EndHorizontal();

                for (int i = 0; i < Rows.Length; i++)
                {
                    Row row = Rows[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(T(row.En, row.Zh, row.Tr), labelStyle, GUILayout.Width(190));
                    float t = ToSlider(row.Min, row.Max, row.Log, draft[i]);
                    float moved = GUILayout.HorizontalSlider(t, 0f, 1f, GUILayout.Width(190));
                    if (moved != t)
                    {
                        draft[i] = FromSlider(row.Min, row.Max, row.Log, row.ZeroAtLeft, row.Whole, moved);
                        dirty = true;
                    }
                    string text = i == Aggression ? (draft[i] >= 0 ? T(AggressionNames[(int)draft[i]]) : "?") : row.Whole ? draft[i].ToString("0") : "×" + draft[i].ToString("0.##");
                    GUILayout.Label(text, labelStyle, GUILayout.Width(70));
                    GUILayout.EndHorizontal();
                }

                GameHistoryData history = GameMain.data.history;
                GUILayout.Label(T("Difficulty ", "难度 ", "Zorluk ") + history.combatSettings.difficulty.ToString("0.##")
                    + T("    Metadata ×", "    元数据 ×", "    Meta veri ×") + MetadataMultiplier(history).ToString("0.##"), labelStyle);
                GUILayout.Label(T("Applies when you release the mouse. Hive count and starting hives are fixed at new game.",
                                  "松开鼠标时生效。巢穴数量和初始巢穴在新游戏时确定。",
                                  "Fareyi bırakınca uygulanır. Kovan sayısı ve başlangıç kovanları yeni oyunda belirlenir."), labelStyle);
            }
            if (GUILayout.Button(T("Close", "关闭", "Kapat"))) open = false;
            GUI.DragWindow();
        }
    }

    public static class Patches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(EvolveData), nameof(EvolveData.GetSpaceThreatMaxByWaves))]
        [HarmonyPatch(typeof(EvolveData), nameof(EvolveData.GetGroundThreatMaxByWaves))]
        static void ThreatMax(ref int __result) => __result = Plugin.ScaleThreat(__result);

        [HarmonyPrefix, HarmonyPatch(typeof(EvolveData), nameof(EvolveData.GetGoundAssaultCountByWaves))]
        static void GroundWave(ref int wave)
        {
            if (wave > 0) wave = (int)(wave * Plugin.FleetGrowthFactor + 0.5f);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(EvolveData), nameof(EvolveData.GetGoundAssaultCountByWaves))]
        static void GroundCount(ref int cnt0, ref int cnt1, ref int ap0, ref int ap1)
        {
            cnt0 = Plugin.ScaleCount(cnt0);
            cnt1 = Plugin.ScaleCount(cnt1);
            ap0 = Plugin.ScaleCount(ap0);
            ap1 = Plugin.ScaleCount(ap1);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(EnemyDFHiveSystem), nameof(EnemyDFHiveSystem.GetLancerAssaultCountInitial))]
        static void SpaceInitial(ref float __result)
        {
            if (__result > 0f) __result = Plugin.ClampAssault(__result * Plugin.FleetSizeFactor);
        }

        // The base already carries the size factor, so each wave's step has to carry it too.
        [HarmonyPostfix, HarmonyPatch(typeof(EnemyDFHiveSystem), nameof(EnemyDFHiveSystem.GetLancerAssaultCountIncrement))]
        static void SpaceIncrement(ref float __result) => __result *= Plugin.FleetSizeFactor * Plugin.FleetGrowthFactor;

        [HarmonyTranspiler, HarmonyPatch(typeof(DFTinderComponent), nameof(DFTinderComponent.PrepareDispatchLogic))]
        static IEnumerable<CodeInstruction> TinderChance(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (CodeInstruction code in instructions)
            {
                if (code.opcode == OpCodes.Ldc_R8 && (double)code.operand == 0.0625)
                {
                    found = true;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Plugin), nameof(Plugin.SpreadChance))).WithLabels(code.labels);
                }
                else yield return code;
            }
            if (!found) throw new InvalidOperationException("DarkFogTuner: tinder launch chance not found; game code changed");
        }

        [ThreadStatic] static bool producing;

        // The game forces turboRatio to 1, so extra production has to run the replicator again; the matter is free, or a starved hive would gain nothing.
        [HarmonyPostfix, HarmonyPatch(typeof(DFSReplicatorComponent), nameof(DFSReplicatorComponent.LogicTick))]
        static void SpaceProduction(ref DFSReplicatorComponent __instance, EnemyDFHiveSystem hive, EnemyBuilderComponent[] builderPool, EAggressiveLevel agglv, float turboRatio, bool isLocal)
        {
            if (producing || Plugin.ProductionFactor <= 1f) return;
            producing = true;
            try
            {
                EnemyFormation form = hive.forms[__instance.productFormId];
                ref EnemyBuilderComponent builder = ref builderPool[__instance.builderId];
                for (int i = 1; i < Plugin.ProductionFactor && builder.state >= 3 && form.vacancyCount > 0 && form.supplyBalance > 0; i++)
                {
                    builder.matter = Math.Max(builder.matter, __instance.productSpMatter);
                    builder.energy = Math.Max(builder.energy, __instance.productSpEnergy);
                    __instance.LogicTick(hive, builderPool, agglv, turboRatio, isLocal);
                }
            }
            finally { producing = false; }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(DFGReplicatorComponent), nameof(DFGReplicatorComponent.LogicTick))]
        static void GroundProduction(ref DFGReplicatorComponent __instance, DFGBaseComponent gbase, EnemyBuilderComponent[] builderPool, EAggressiveLevel agglv, float turboRatio, bool isLocal)
        {
            if (producing || Plugin.ProductionFactor <= 1f) return;
            producing = true;
            try
            {
                EnemyFormation form = gbase.forms[__instance.productFormId];
                ref EnemyBuilderComponent builder = ref builderPool[__instance.builderId];
                for (int i = 1; i < Plugin.ProductionFactor && builder.state >= 3 && form.vacancyCount > 0 && form.supplyBalance > 0; i++)
                {
                    builder.matter = Math.Max(builder.matter, __instance.productSpMatter);
                    builder.energy = Math.Max(builder.energy, __instance.productSpEnergy);
                    __instance.LogicTick(gbase, builderPool, agglv, turboRatio, isLocal);
                }
            }
            finally { producing = false; }
        }

        // An unrealized hive returns before recomputing supply, so scaling it there would compound.
        [HarmonyPostfix, HarmonyPatch(typeof(EnemyDFHiveSystem), nameof(EnemyDFHiveSystem.CalcFormsSupply))]
        static void SpaceSupply(EnemyDFHiveSystem __instance)
        {
            if (!__instance.realized) return;
            foreach (EnemyFormation form in __instance.forms) form.supply = Plugin.ScaleSupply(form.supply);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(EnemyDFGroundSystem), nameof(EnemyDFGroundSystem.CalcFormsSupply))]
        static void GroundSupply(EnemyDFGroundSystem __instance)
        {
            DFGBaseComponent[] bases = __instance.bases.buffer;
            for (int i = 1; i < __instance.bases.cursor; i++)
                if (bases[i] != null && bases[i].id == i)
                    foreach (EnemyFormation form in bases[i].forms) form.supply = Plugin.ScaleSupply(form.supply);
        }

        [HarmonyTranspiler, HarmonyPatch(typeof(EnemyDFHiveSystem), nameof(EnemyDFHiveSystem.AssaultingWavesDetermineAI))]
        static IEnumerable<CodeInstruction> SpaceFleetCap(IEnumerable<CodeInstruction> instructions)
        {
            int found = 0;
            foreach (CodeInstruction code in instructions)
            {
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 360f)
                {
                    found++;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Plugin), nameof(Plugin.FleetCap))).WithLabels(code.labels);
                }
                else yield return code;
            }
            if (found != 2) throw new InvalidOperationException("DarkFogTuner: space fleet cap not found; game code changed");
        }

        [HarmonyPrefix, HarmonyPatch(typeof(SkillSystem), nameof(SkillSystem.DamageObject))]
        static void DamageSpace(ref int damage, ref SkillTarget target, ref SkillTarget caster) => damage = Plugin.ScaleDamage(damage, target.type, caster.type);

        [HarmonyPrefix, HarmonyPatch(typeof(SkillSystem), nameof(SkillSystem.DamageGroundObjectByLocalCaster))]
        static void DamageGroundLocal(ref int damage, ref SkillTargetLocal target, ref SkillTargetLocal caster) => damage = Plugin.ScaleDamage(damage, target.type, caster.type);

        [HarmonyPrefix, HarmonyPatch(typeof(SkillSystem), nameof(SkillSystem.DamageGroundObjectByRemoteCaster))]
        static void DamageGroundRemote(ref int damage, ref SkillTargetLocal target, ref SkillTarget caster) => damage = Plugin.ScaleDamage(damage, target.type, caster.type);

        // Turrets stop firing at targets they expect to die, so the forecast must match the real hit.
        [HarmonyPostfix, HarmonyPatch(typeof(SkillSystem), nameof(SkillSystem.CalculateDamageIncoming))]
        static void DamageForecast(ref int __result, ref SkillTarget target)
        {
            if (target.type == ETargetType.Enemy) __result = Plugin.ScaleDamage(__result, ETargetType.Enemy, ETargetType.None);
        }

        // Only enemy shots reach the planetary shield. It pays the scaled hit; what passes through is unscaled again, because DamageObject scales it next.
        [HarmonyPrefix, HarmonyPatch(typeof(PlanetATField), nameof(PlanetATField.TakeDamage))]
        static void ShieldHit(ref int damage) => damage = Plugin.ScaleDamage(damage, ETargetType.None, ETargetType.Enemy);

        [HarmonyPostfix, HarmonyPatch(typeof(PlanetATField), nameof(PlanetATField.TakeDamage))]
        static void ShieldRemainder(ref int __result)
        {
            if (Plugin.EnemyDamageFactor != 1f) __result = (int)(__result / (double)Plugin.EnemyDamageFactor);
        }

        // Clicks on the panel must not reach the factory behind it.
        [HarmonyPostfix, HarmonyPatch(typeof(VFInput), nameof(VFInput.UpdateGameStates))]
        static void BlockInput()
        {
            if (!Plugin.MouseOverWindow) return;
            VFInput.onGUI = true;
            VFInput.onGUIOperate = true;
        }
    }
}
