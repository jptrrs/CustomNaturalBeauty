using System.Linq;
using HugsLib;
using Verse;
using HugsLib.Settings;
using RimWorld;
using System.Collections.Generic;
using System;
using UnityEngine;
using HarmonyLib;

namespace CustomNaturalBeauty
{
    public class Base : ModBase
    {
        public static Dictionary<SettingHandle, BeautyHandleInfo> BeautyHandlesCache = new Dictionary<SettingHandle, BeautyHandleInfo>();
        private static readonly int boxPadding = 2;
        private static readonly string profilePrefix = "profile";
        private ProfileEnum PresetSelected;

        public Base()
        {
            Instance = this;
            Settings.EntryName = "Custom Natural Beauty";
        }

        private enum ProfileEnum
        { Default, NatureIsBeautiful, BeautifulOutdoors, LastSaved }

        public override string ModIdentifier
        {
            get { return "CustomNaturalBeauty"; }
        }

        internal static Base Instance { get; private set; }

        public static Rect DrawWithGoButton(Rect rect, Action payload, string goLabel, int trim = 3)
        {
            Rect goBox = new Rect();
            Rect box = DrawWithGoButton(rect, out goBox, trim);
            if (Widgets.ButtonText(goBox, goLabel)) payload();
            return box;
        }

        public static Rect DrawWithGoButton(Rect rect, out Rect goBox, int trim = 3)
        {
            var trimWidht = trim * rect.height;
            Rect box = rect;
            box.width = rect.width - trimWidht;
            goBox = box;
            goBox.x = box.xMax + 1;
            goBox.width = trimWidht - 1;
            return box;
        }

        public override void DefsLoaded()
        {
            var plants = DefDatabase<ThingDef>.AllDefs.Where(x => x.plant != null).OrderBy(x => x.label);
            var chunks = DefDatabase<ThingDef>.AllDefs.Where(x => x.defName.StartsWith("Chunk")).OrderBy(x => x.label);
            var rocks = DefDatabase<ThingDef>.AllDefs.Where(x => x.mineable).OrderBy(x => x.label);
            var filth = DefDatabase<ThingDef>.AllDefs.Where(x => x.filth != null).OrderBy(x => x.label);
            var terrain = DefDatabase<TerrainDef>.AllDefs.OrderBy(x => x.label);
            IEnumerable<BuildableDef> affected = plants.Cast<BuildableDef>().Concat(chunks.Cast<BuildableDef>()).Concat(rocks.Cast<BuildableDef>()).Concat(filth.Cast<BuildableDef>()).Concat(terrain.Cast<BuildableDef>());
            var source = Settings.GetHandle("DefaultsFrom", "PresetsTitle".Translate(), "PresetsDesc".Translate(), ProfileEnum.Default, null, "profile");
            source.ValueChanged += handle => PresetSelected = source.Value;
            source.CanBeReset = true;
            source.Unsaved = true;
            source.CustomDrawer = rect => DrawSettingDropDown(DrawWithGoButton(rect, SetFromPreset, "Apply".Translate()), source);
            bool isCustom = source.Value == ProfileEnum.Default;
            var searchField = Settings.GetHandle<string>("SearchField", "SearchField".Translate(), "SearchFieldDesc".Translate(), "");
            searchField.ValueChanged += handle => MatchSearch(handle.StringValue);
            searchField.Unsaved = true;
            searchField.CustomDrawer = rect => DrawSettingSearchField(rect, searchField, searchField.Value);
            List<string> log = new List<string>();
            foreach (BuildableDef e in affected) ProcessDef(source, e);
        }

        public void ResetValues()
        {
            foreach (SettingHandle h in Settings.Handles)
            {
                if (h is SettingHandle<int> handle)
                {
                    handle.ResetToDefault();
                    handle.ForceSaveChanges();
                }
            }
        }

        private static bool DrawSettingDropDown(Rect rect, SettingHandle<ProfileEnum> setting)
        {
            bool change = false;
            if (Widgets.ButtonText(rect, (profilePrefix+setting.Value.ToString()).Translate()))
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                foreach (ProfileEnum p in Enum.GetValues(typeof(ProfileEnum)))
                {
                    list.Add(new FloatMenuOption((profilePrefix + p.ToString()).Translate(), delegate ()
                    {
                        setting.Value = p;
                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                }
                Find.WindowStack.Add(new FloatMenu(list));
            }
            return change;
        }

        private static bool DrawSettingSearchField(Rect rect, SettingHandle<string> setting, string text)
        {
            bool change = false;
            Rect goBox = new Rect();
            string input = Widgets.TextField(DrawWithGoButton(rect, out goBox, 1), text);
            if (Widgets.ButtonText(goBox, "x"))
            {
                input = "";
            }
            setting.Value = input;
            if (input.Length > 1 && input != text)
            {
                change = true;
            }
            return change;
        }

        private static bool Extracted(string source, string key, out int value)
        {
            TaggedString taggedString;
            bool result = LanguageDatabase.AllLoadedLanguages.Where(x => x.folderName == source).FirstOrDefault().TryGetTextFromKey(key, out taggedString);
            int.TryParse(taggedString.RawText, out value);
            return result;
        }

        private StatDef FindRelevantStat(BuildableDef def, out int defBeauty)
        {
            defBeauty = 0;
            bool outBeauty = def.statBases.StatListContains(StatDefOf.BeautyOutdoors);
            bool hasBeauty = outBeauty || def.statBases.StatListContains(StatDefOf.Beauty);
            StatDef stat = outBeauty ? StatDefOf.BeautyOutdoors : StatDefOf.Beauty;
            defBeauty = hasBeauty ? (int)def.statBases.First((StatModifier s) => s.stat == stat).value : 0;
            if (!hasBeauty)
            {
                def.statBases = new List<StatModifier>();
                def.statBases.Add(new StatModifier() { stat = StatDefOf.Beauty, value = defBeauty });
            }
            return stat;
        }

        private void MatchSearch(string word)
        {
            if (word.Count() < 1)
            {
                //reset search
                BeautyHandlesCache.Where(x => !x.Value.matchesSearch).Select(x => x.Value).Do(x => x.matchesSearch = true);
                return;
            }
            BeautyHandlesCache.Where(x => !x.Key.Title.Contains(word)).Select(x => x.Value).Do(x => x.matchesSearch = false);
        }

        private void ProcessDef(SettingHandle<ProfileEnum> source, BuildableDef def)
        {
            string name = def.defName;
            int defValue;
            bool saved = Settings.ValueExists(name);
            var tempstat = FindRelevantStat(def, out defValue);
            var stat = (tempstat != null) ? tempstat : new StatDef();
            var customBeauty = Settings.GetHandle<int>(def.defName, def.label, def.description, defValue, Validators.IntRangeValidator(-50, +50));
            customBeauty.ValueChanged += handle =>
            {
                def.SetStatBaseValue(stat, customBeauty.Value);
                customBeauty.HasUnsavedChanges = true;
            };
            customBeauty.VisibilityPredicate = delegate
            {
                return BeautyHandlesCache[customBeauty].matchesSearch;
            };
            BeautyHandlesCache.AddDistinct(customBeauty, new BeautyHandleInfo() { wasSaved = saved, savedValue = customBeauty.Value, matchesSearch = true });
            def.SetStatBaseValue(stat, customBeauty.Value);
        }

        private void SetFromPreset()
        {
            foreach (SettingHandle h in Settings.Handles)
            {
                if (h is SettingHandle<int> handle)
                {
                    string name = h.Name;
                    if (PresetSelected == ProfileEnum.LastSaved)
                    {
                        if (BeautyHandlesCache.ContainsKey(handle) && BeautyHandlesCache[handle].wasSaved)
                        {
                            handle.Value = BeautyHandlesCache[handle].savedValue;
                            handle.HasUnsavedChanges = true;
                        }
                    }
                    else if (PresetSelected > 0 && Extracted(PresetSelected.ToString(), name, out int presetValue)) // as in, not the base game
                    {
                        handle.Value = presetValue;
                        handle.HasUnsavedChanges = true;
                    }
                    else
                    {
                        handle.ResetToDefault();
                    }
                }
            }
        }
    }
}