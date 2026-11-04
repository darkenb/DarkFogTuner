using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

namespace DarkFogTuner
{
    [BepInPlugin("bug.dsp.darkfogtuner", "DarkFogTuner", "1.3.1")]
    public class Plugin : BaseUnityPlugin
    {
        const int Aggression = 0, Frequency = 1, FleetSize = 2, FleetGrowth = 3, Spread = 4, Growth = 5, PowerThreat = 6, BattleThreat = 7, BattleExp = 8, Capacity = 9, Production = 10, EnemyDamage = 11, EnemyHealth = 12, NewLevel = 13, NewGrowth = 14;
        const long NoGalaxy = -2;

        // Must be the game's exact values: any other aggressiveness maps to no EAggressiveLevel.
        static readonly float[] AggressionValues = { -1f, 0f, 0.5f, 1f, 2f, 3f };

        static readonly string[] AggressionNames = { "Dummy", "Passive", "Torpid", "Normal", "Sharp", "Rampage" };

        class Row { public string Name; public float Min, Max; public bool Log, ZeroAtLeft, Whole; }

        static readonly Row[] Rows =
        {
            new Row { Name = "Aggressiveness", Min = 0, Max = 5, Whole = true },
            new Row { Name = "Attack frequency", Min = 0.1f, Max = 100, Log = true },
            new Row { Name = "Fleet size", Min = 0.1f, Max = 20, Log = true },
            new Row { Name = "Fleet growth per wave", Min = 0.1f, Max = 10, Log = true, ZeroAtLeft = true },
            new Row { Name = "Spread to other stars", Min = 0.01f, Max = 20, Log = true, ZeroAtLeft = true },
            new Row { Name = "Build and replication speed", Min = 0.1f, Max = 10, Log = true },
            new Row { Name = "Power threat (ground bases)", Min = 0.01f, Max = 100, Log = true },
            new Row { Name = "Threat from combat", Min = 0.01f, Max = 100, Log = true },
            new Row { Name = "Experience from combat", Min = 0.01f, Max = 20, Log = true },
            new Row { Name = "Unit capacity", Min = 1, Max = 20, Log = true },
            new Row { Name = "Free unit production", Min = 1, Max = 50, Log = true },
            new Row { Name = "Enemy damage", Min = 0.1f, Max = 100, Log = true },
            new Row { Name = "Enemy durability", Min = 0.1f, Max = 100, Log = true },
            new Row { Name = "New hive level", Min = 0, Max = 30, Whole = true },
            new Row { Name = "New hive head start", Min = 0, Max = 4 },
        };

        // NaN keeps the current value. Order follows Rows.
        static readonly KeyValuePair<string, float[]>[] Presets =
        {
            Preset("Easy", 2, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.2f, 0.2f, 0.2f, 1, 1, 0.5f, 0.5f),
            Preset("Normal", 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1),
            Preset("Hard", 4, 1.5f, 1.5f, 1.5f, 1.5f, 2, 2, 2, 2, 2, 2, 2, 2),
            Preset("Extreme", 5, 10, 5, 5, 5, 10, 20, 20, 5, 10, 10, 5, 5),
        };

        const int NormalPreset = 1;

        // First row of each group after the first; rows keep their order.
        static readonly int[] GroupStarts = { 1, 5, 9, 13 };

        static KeyValuePair<string, float[]> Preset(string name, params float[] v) =>
            new KeyValuePair<string, float[]>(name, v.Concat(new[] { float.NaN, float.NaN }).ToArray());

        // Read by the patches from the game's worker threads.
        public static float AttackFrequency = 1f, FleetSizeFactor = 1f, FleetGrowthFactor = 1f, SpreadFactor = 1f, UnitCapacity = 1f, ProductionFactor = 1f, EnemyDamageFactor = 1f, EnemyHealthFactor = 1f;

        static ConfigEntry<KeyboardShortcut> hotkey;
        static ConfigFile config;
        static long galaxy = NoGalaxy;
        static bool dirty, syncing;
        static readonly float[] draft = new float[Rows.Length];
        // Tokens from the game's own window: panel teal, line cyan, selection orange. Glass order: rest, hover, pressed, text.
        static readonly Color Ink = new Color32(232, 246, 252, 255), Soft = new Color32(176, 208, 220, 255), Accent = new Color32(253, 150, 94, 255), AccentInk = new Color32(28, 14, 6, 255), Scrim = new Color32(4, 22, 30, 168), Well = new Color32(0, 10, 16, 120), Line = new Color32(167, 233, 255, 40);
        static readonly Color[] Glass = { new Color32(167, 233, 255, 56), new Color32(167, 233, 255, 92), new Color32(167, 233, 255, 32), Ink };
        static readonly Color Easier = new Color32(167, 233, 255, 255);
        static GameObject win, content;
        static Text noCombatText, summaryText;
        static readonly Slider[] sliders = new Slider[Rows.Length];
        static readonly Text[] valueTexts = new Text[Rows.Length];
        static readonly Image[] deviations = new Image[Rows.Length];
        static readonly UIButton[] presetButtons = new UIButton[Presets.Length];

        void Awake()
        {
            config = Config;
            hotkey = Config.Bind("General", "Hotkey", new KeyboardShortcut(KeyCode.K, KeyCode.LeftControl, KeyCode.LeftShift), "Opens and closes the panel.");
            new Harmony("bug.dsp.darkfogtuner").PatchAll(typeof(Patches));
        }

        static bool InGame => GameMain.isRunning && !DSPGame.IsMenuDemo && GameMain.data != null && GameMain.data.gameDesc != null;

        void Update()
        {
            long key = InGame ? GameMain.data.GetClusterSeedKey() : NoGalaxy;
            if (key != galaxy) { galaxy = key; LoadGalaxy(); }
            // Only the panel stops here, the galaxy's factors stay loaded. Paused means the Esc menu; the game UI is closed on the main menu and while loading.
            if (key == NoGalaxy || GameMain.isPaused || !UIRoot.instance.uiGame.active)
            {
                if (Open)
                {
                    if (dirty) Apply();
                    win.SetActive(false);
                }
                return;
            }
            if (hotkey.Value.IsDown() && !VFInput.inputing)
            {
                if (win == null) Build();
                win.SetActive(!win.activeSelf);
                if (win.activeSelf)
                {
                    win.transform.SetAsLastSibling();
                    ReadDraft();
                    ShowDraft();
                }
            }
            if (Open && dirty && !Input.GetMouseButton(0))  // on release: rescaling fleets mid-drag would stack rounding and clamps
            {
                Apply();
                ShowDraft();
            }
        }

        static bool Open => win != null && win.activeSelf;

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

        public static bool Matches(float[] preset, float[] values)
        {
            for (int i = 0; i < preset.Length; i++)
                if (!float.IsNaN(preset[i]) && Math.Abs(preset[i] - values[i]) > 0.001f) return false;
            return true;
        }

        // The Normal preset's value; rows it leaves alone start from their minimum.
        public static float NormalValue(int i)
        {
            float v = Presets[NormalPreset].Value[i];
            return float.IsNaN(v) ? Rows[i].Min : v;
        }

        // Where a row's slider sits at that value. Log ranges differ per row, so positions only compare against this mark.
        public static float Origin(int i) => ToSlider(Rows[i].Min, Rows[i].Max, Rows[i].Log, NormalValue(i));

        static void SetDraft(int i, float value)
        {
            draft[i] = value;
            dirty = true;
        }

        static string ValueText(int i)
        {
            Row row = Rows[i];
            return i == Aggression ? (draft[i] >= 0 ? AggressionNames[(int)draft[i]] : "?") : row.Whole ? draft[i].ToString("0") : "×" + draft[i].ToString("0.##");
        }

        // Every row reads "higher is harder", so the bar's side and colour say which way a setting left Normal.
        static void ShowRow(int i)
        {
            valueTexts[i].text = ValueText(i);
            valueTexts[i].color = Math.Abs(draft[i] - NormalValue(i)) > 0.001f ? Ink : Soft;
            float origin = Origin(i), value = sliders[i].value;
            var bar = deviations[i].rectTransform;
            bar.anchorMin = new Vector2(Math.Min(origin, value), 0f);
            bar.anchorMax = new Vector2(Math.Max(origin, value), 1f);
            deviations[i].color = value > origin ? Accent : Easier;
        }

        static void ShowPresets()
        {
            for (int p = 0; p < Presets.Length; p++) presetButtons[p].highlighted = Matches(Presets[p].Value, draft);
        }

        static void ShowDraft()
        {
            bool combat = GameMain.data.gameDesc.isCombatMode;
            content.SetActive(combat);
            noCombatText.gameObject.SetActive(!combat);
            if (!combat) return;
            syncing = true;
            for (int i = 0; i < Rows.Length; i++)
            {
                sliders[i].value = ToSlider(Rows[i].Min, Rows[i].Max, Rows[i].Log, draft[i]);
                ShowRow(i);
            }
            syncing = false;
            ShowPresets();
            GameHistoryData history = GameMain.data.history;
            summaryText.text = "Difficulty " + history.combatSettings.difficulty.ToString("0.##") + "    Metadata ×" + MetadataMultiplier(history).ToString("0.##");
        }

        // Same frame and templates as PowerPlantCalc: the generator window with its contents and behaviour removed.
        static void Build()
        {
            const float Width = 582f, Height = 572f, Margin = 20f, RowsTop = -92f, RowStep = 24f, GroupGap = 9f;
            var src = UIRoot.instance.uiGame.generatorWindow.transform;
            var buttonTpl = src.Find("ray-receiver/switch-button-1").gameObject;
            var labelTpl = src.Find("power-network-desc/desc-group/label-1").GetComponent<Text>();
            var valueTpl = src.Find("power-network-desc/desc-group/value-1").GetComponent<Text>();
            var thumbSprite = src.Find("boost-switch/thumb-rail/switch-thumb").GetComponent<Image>().sprite;

            win = UnityEngine.Object.Instantiate(src.gameObject, src.parent, false);
            win.name = "Dark Fog Settings";
            UnityEngine.Object.DestroyImmediate(win.GetComponent<UIPowerGeneratorWindow>());
            foreach (Transform c in win.transform) if (c.name != "panel-bg") UnityEngine.Object.Destroy(c.gameObject);
            UnityEngine.Object.Destroy(win.transform.Find("panel-bg/btn-box/menu-wnd-btn").gameObject);
            StripLocalizers(win);
            Backdrop(win.transform.Find("panel-bg"));
            var close = win.transform.Find("panel-bg/btn-box/close-wnd-btn").GetComponent<Button>();
            close.onClick = new Button.ButtonClickedEvent();
            close.onClick.AddListener(() => win.SetActive(false));
            var title = win.transform.Find("panel-bg/title-text").GetComponent<Text>();
            title.text = "Dark Fog settings";
            title.rectTransform.sizeDelta = new Vector2(400f, title.rectTransform.sizeDelta.y);
            var root = (RectTransform)win.transform;
            root.anchoredPosition = new Vector2(-500f, 330f);
            root.sizeDelta = new Vector2(Width, Height);

            noCombatText = NewText(labelTpl, root, new Vector2(Margin, -50f), new Vector2(Width - 2 * Margin, 20f), TextAnchor.MiddleLeft, "This save has no combat mode.");
            content = new GameObject("content", typeof(RectTransform));
            var area = (RectTransform)content.transform;
            area.SetParent(root, false);
            Place(area, Vector2.zero, new Vector2(Width, Height));

            for (int p = 0; p < Presets.Length; p++)
            {
                float[] values = Presets[p].Value;
                presetButtons[p] = NewButton(buttonTpl, area, new Vector2(Margin + p * 136f, -50f), new Vector2(134f, 28f), Presets[p].Key, () =>
                {
                    for (int i = 0; i < draft.Length; i++)
                        if (!float.IsNaN(values[i])) SetDraft(i, values[i]);
                    ShowDraft();
                });
            }
            Card(area, new Vector2(12f, RowsTop + 8f), new Vector2(Width - 24f, Rows.Length * RowStep + GroupStarts.Length * GroupGap + 12f), Well);
            float y = RowsTop;
            for (int r = 0; r < Rows.Length; r++)
            {
                int i = r;
                Row row = Rows[i];
                if (Array.IndexOf(GroupStarts, i) >= 0)
                {
                    y -= GroupGap;
                    Card(area, new Vector2(Margin, y + 7f), new Vector2(Width - 2 * Margin, 1f), Line);
                }
                NewText(labelTpl, area, new Vector2(Margin, y), new Vector2(190f, 20f), TextAnchor.MiddleLeft, row.Name);
                sliders[i] = NewSlider(i, thumbSprite, area, new Vector2(214f, y), new Vector2(256f, 20f));
                valueTexts[i] = NewText(valueTpl, area, new Vector2(474f, y), new Vector2(Width - Margin - 474f, 20f), TextAnchor.MiddleRight, "");
                y -= RowStep;
                sliders[i].onValueChanged.AddListener(t =>
                {
                    if (syncing) return;
                    SetDraft(i, FromSlider(row.Min, row.Max, row.Log, row.ZeroAtLeft, row.Whole, t));
                    ShowRow(i);
                    ShowPresets();
                });
            }
            summaryText = NewText(valueTpl, area, new Vector2(Margin, y - 12f), new Vector2(Width - 2 * Margin, 24f), TextAnchor.MiddleLeft, "");
            summaryText.color = Ink;
            summaryText.fontSize = 16;
            var note = NewText(labelTpl, area, new Vector2(Margin, y - 38f), new Vector2(Width - 2 * Margin, 34f), TextAnchor.UpperLeft, "Applies when you release the mouse. Hive count and starting hives are fixed at new game.");
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.fontSize = 12;
            win.SetActive(false);
        }

        // The options window's volume slider, reduced to a thin track, a Normal mark, a bar from that mark and the game's round switch thumb.
        static Slider NewSlider(int row, Sprite thumbSprite, Transform parent, Vector2 pos, Vector2 size)
        {
            const float Thumb = 12f;
            var s = UnityEngine.Object.Instantiate(UIRoot.instance.optionWindow.audioVolumeComp, parent, false);
            StripLocalizers(s.gameObject);
            s.gameObject.SetActive(true);
            foreach (var ub in s.GetComponentsInChildren<UIButton>(true)) UnityEngine.Object.DestroyImmediate(ub);  // they would recolour the thumb
            Place((RectTransform)s.transform, pos, size);
            s.onValueChanged = new Slider.SliderEvent();
            s.wholeNumbers = false;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.interactable = true;

            var hit = s.transform.Find("col")?.GetComponent<Image>();
            if (hit != null) hit.color = Color.clear;
            var clickArea = s.GetComponent<Image>();  // the visible track is too thin to click
            if (clickArea == null) clickArea = s.gameObject.AddComponent<Image>();
            clickArea.color = Color.clear;
            var background = s.transform.Find("Background");
            if (background != null)
            {
                foreach (Transform c in background) c.gameObject.SetActive(false);
                Track((RectTransform)background, 0f);
                Flat(background, Glass[0]);
            }
            var fillArea = (RectTransform)s.fillRect.parent;
            var fillAreaImage = fillArea.GetComponent<Image>();
            if (fillAreaImage != null) fillAreaImage.enabled = false;
            Track(fillArea, -Thumb);
            var fill = s.fillRect;
            s.fillRect = null;  // ShowRow draws the bar from the Normal mark, not from the left end
            fill.anchoredPosition = fill.sizeDelta = Vector2.zero;
            if (fill.GetComponent<Image>() == null) fill.gameObject.AddComponent<Image>();
            Flat(fill, Accent);
            deviations[row] = fill.GetComponent<Image>();
            var mark = Card(fillArea, Vector2.zero, new Vector2(1f, 10f), Soft).rectTransform;
            mark.anchorMin = mark.anchorMax = new Vector2(Origin(row), 0.5f);
            mark.pivot = new Vector2(0.5f, 0.5f);
            mark.anchoredPosition = Vector2.zero;

            var handle = s.handleRect;
            foreach (Transform c in handle) UnityEngine.Object.Destroy(c.gameObject);
            var slideArea = (RectTransform)handle.parent;
            slideArea.anchorMin = Vector2.zero;
            slideArea.anchorMax = Vector2.one;
            slideArea.anchoredPosition = Vector2.zero;
            slideArea.sizeDelta = new Vector2(-Thumb, 0f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(Thumb, -8f);  // rows are 20 tall, so the thumb is round
            var thumb = handle.GetComponent<Image>();
            if (thumb == null) thumb = handle.gameObject.AddComponent<Image>();
            Flat(handle, Ink);
            thumb.sprite = thumbSprite;
            s.targetGraphic = thumb;
            s.transition = Selectable.Transition.ColorTint;
            var tint = ColorBlock.defaultColorBlock;
            tint.normalColor = new Color(0.82f, 0.82f, 0.82f);
            tint.highlightedColor = Color.white;
            tint.pressedColor = new Color(1f, 0.66f, 0.42f);
            s.colors = tint;
            return s;
        }

        static void Track(RectTransform rt, float inset)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(inset, 3f);
        }

        static void Flat(Transform t, Color color)
        {
            var image = t.GetComponent<Image>();
            if (image == null) return;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.material = null;
            image.color = color;
            image.enabled = true;
        }

        static Text NewText(Text tpl, Transform parent, Vector2 pos, Vector2 size, TextAnchor align, string text)
        {
            var t = UnityEngine.Object.Instantiate(tpl, parent, false);
            StripLocalizers(t.gameObject);
            Place(t.rectTransform, pos, size);
            t.alignment = align;
            t.text = text;
            t.material = null;  // the value template's material overdrives bright colours into a glow
            t.color = Soft;
            t.raycastTarget = false;
            return t;
        }

        static UIButton NewButton(GameObject tpl, Transform parent, Vector2 pos, Vector2 size, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = UnityEngine.Object.Instantiate(tpl, parent, false);
            StripLocalizers(go);
            go.SetActive(true);
            Place((RectTransform)go.transform, pos, size);
            go.GetComponentInChildren<Text>().text = label;
            var b = go.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(onClick);
            var ub = go.GetComponent<UIButton>();
            ub.tips.tipTitle = "";
            ub.tips.tipText = "";
            Style(go, Glass, 14);
            return ub;
        }

        static void Style(GameObject go, Color[] palette, int fontSize)
        {
            var label = go.GetComponentInChildren<Text>();
            foreach (var t in go.GetComponent<UIButton>().transitions)
            {
                if (t.target == null) continue;
                bool isText = t.target is Text;
                t.normalColor = t.disabledColor = isText ? palette[3] : palette[0];
                t.mouseoverColor = isText ? palette[3] : palette[1];
                t.pressedColor = isText ? palette[3] : palette[2];
                t.highlightColorOverride = isText ? AccentInk : Accent;
                t.alphaOnly = false;
                t.mouseoverSize = t.pressedSize = t.highlightSizeMultiplier = 1f;
            }
            go.GetComponent<UIButton>().highlighted = false;  // the cloned template is the receiver's selected mode
            go.GetComponent<Button>().transition = Selectable.Transition.None;
            var fill = go.GetComponent<Image>();
            fill.material = null;  // the template's widget material boosts alpha, so colours would not read as written
            fill.color = palette[0];
            label.color = palette[3];
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = rt.sizeDelta = Vector2.zero;
        }

        // Deepens the game's glass so text holds on bright ground. The panel's own sprite keeps the cut corners; first child, so border, title and close button stay on top.
        static void Backdrop(Transform panel)
        {
            var bg = panel.GetComponent<Image>();
            var scrim = new GameObject("scrim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            scrim.transform.SetParent(panel, false);
            scrim.transform.SetAsFirstSibling();
            scrim.rectTransform.anchorMin = Vector2.zero;
            scrim.rectTransform.anchorMax = Vector2.one;
            scrim.rectTransform.anchoredPosition = scrim.rectTransform.sizeDelta = Vector2.zero;
            scrim.sprite = bg.sprite;
            scrim.type = bg.type;
            scrim.color = Scrim;
            scrim.raycastTarget = false;
        }

        static Image Card(Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var image = new GameObject("card", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            Place(image.rectTransform, pos, size);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        // Cloned game texts would otherwise be reset to their original translation keys.
        static void StripLocalizers(GameObject go)
        {
            foreach (var l in go.GetComponentsInChildren<Localizer>(true)) UnityEngine.Object.DestroyImmediate(l);
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
    }
}
