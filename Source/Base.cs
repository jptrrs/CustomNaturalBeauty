using System.Linq;
using HugsLib;
using Verse;
using HugsLib.Settings;
using RimWorld;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Reflection;
using HarmonyLib;

namespace CustomNaturalBeauty
{
    public class Base : ModBase
    {
        public static Dictionary<SettingHandle, bool> Matched = new Dictionary<SettingHandle, bool>();
        internal static Base Instance { get; private set; }
        public override string ModIdentifier
        {
            get { return "CustomNaturalBeauty"; }
        }

        public Base()
        {
            Instance = this;
            Settings.EntryName = "Custom Natural Beauty";
        }

        private enum ProfileEnum { BaseGame , NatureIsBeautiful, BeautifulOutdoors }

        private Dictionary<string, int> BaseBeauty = new Dictionary<string, int>();

        private static bool Extracted(string source, string key, out int value)
        {
            TaggedString taggedString;
            bool result = LanguageDatabase.AllLoadedLanguages.Where(x => x.folderName == source).FirstOrDefault().TryGetTextFromKey(key, out taggedString);
            int.TryParse(taggedString.RawText, out value);
            return result;
        }

        public override void DefsLoaded()
        {
            var plants = DefDatabase<ThingDef>.AllDefs.Where(x => x.plant != null).OrderBy(x => x.label);
            var chunks = DefDatabase<ThingDef>.AllDefs.Where(x => x.defName.StartsWith("Chunk")).OrderBy(x => x.label);
            var rocks = DefDatabase<ThingDef>.AllDefs.Where(x => x.mineable).OrderBy(x => x.label);
            var filth = DefDatabase<ThingDef>.AllDefs.Where(x => x.filth != null).OrderBy(x => x.label);
            var terrain = DefDatabase<TerrainDef>.AllDefs.OrderBy(x => x.label);
            IEnumerable<BuildableDef> affected = plants.Cast<BuildableDef>().Concat(chunks.Cast<BuildableDef>()).Concat(rocks.Cast<BuildableDef>()).Concat(filth.Cast<BuildableDef>()).Concat(terrain.Cast<BuildableDef>());

            var source = Settings.GetHandle("DefaultsFrom", "DefaultsFromTitle".Translate(), "DefaultsFromDesc".Translate(), ProfileEnum.BaseGame, null, "profile");
            source.ValueChanged += handle => SetNewDefaults(source.StringValue, source.Value);
            bool isCustom = source.Value == 0;

            var resetButton = Settings.GetHandle<bool>("ReloadButton", "", "ReloadDesc".Translate());
            resetButton.Unsaved = true;
            resetButton.CustomDrawer = rect => Button(rect, resetButton, "Reload".Translate());
            resetButton.ValueChanged += handle => ResetValues();

            var searchField =  Settings.GetHandle<string>("SearchField", "SearchField", "SearchField", "");
            searchField.Unsaved = true;
            searchField.CustomDrawer = rect => SearchField(rect, searchField, searchField.Value);
            searchField.ValueChanged += handle => MatchSearch(handle.StringValue);

            foreach (BuildableDef e in affected)
            {
                ProcessDef(source, isCustom, e);
            }
        }

        private void ProcessDef(SettingHandle<ProfileEnum> source, bool isCustom, BuildableDef e)
        {
            int presetValue = 0;
            bool isPreset = isCustom ? false : Extracted(source.StringValue, e.defName, out presetValue);
            int defaultValue;
            var stat = FindRelevantStat(e, out defaultValue, isPreset);
            var customBeauty = Settings.GetHandle<int>(e.defName, e.label, e.description, defaultValue, Validators.IntRangeValidator(-50, +50));
            customBeauty.ValueChanged += handle =>
            {
                e.SetStatBaseValue(stat, customBeauty.Value);
                ResetControl(customBeauty);
            };
            Matched.Add(customBeauty, true);
            customBeauty.VisibilityPredicate = delegate
            {
                return Matched[customBeauty];
            };
            e.SetStatBaseValue(stat, customBeauty.Value);
        }

        private StatDef FindRelevantStat(BuildableDef def, out int defaultValue, bool isPreset, int presetValue = 0)
        {
            if (def.statBases.NullOrEmpty())
            {
                Log.Error($"[CustomNaturalBeauty] Can't find any stats for {def}!");
                defaultValue = 0;
                return null;
            }
            bool outBeauty = def.statBases.StatListContains(StatDefOf.BeautyOutdoors);
            bool hasBeauty = outBeauty || def.statBases.StatListContains(StatDefOf.Beauty);
            StatDef stat = outBeauty ? StatDefOf.BeautyOutdoors : StatDefOf.Beauty;
            int defBeauty = hasBeauty ? (int)def.statBases.First((StatModifier s) => s.stat == stat).value : 0;
            defaultValue = isPreset ? presetValue : defBeauty;
            if (!hasBeauty)
            {
                def.statBases = new List<StatModifier>();
                def.statBases.Add(new StatModifier() { stat = StatDefOf.Beauty, value = defaultValue });
            }
            BaseBeauty.Add(def.defName, defBeauty);
            return stat;
        }

        private void MatchSearch(string word)
        {
            ResetSearch();
            if (word.Count() > 1)
            {
                foreach (var handle in Matched.Keys.Where(x => !x.Title.Contains(word)).ToList())
                {
                    Matched[handle] = false;
                }
            }
            else ResetSearch();
        }

        private void ResetSearch()
        {
            foreach (var handle in Matched.Keys.ToList())
            {
                Matched[handle] = true;
            }
        }

        private void SetNewDefaults(string key, ProfileEnum newvalue)
        {
            foreach(SettingHandle h in Settings.Handles)
            {
                if (h is SettingHandle<int> handle)
                {
                    if (newvalue > 0)
                    {
                        bool isPreset = Extracted(key, handle.Name, out int presetValue);
                        if (isPreset)
                        {
                            handle.DefaultValue = presetValue;
                        }
                    }
                    else
                    {
                        handle.DefaultValue = BaseBeauty[handle.Name];
                    }
                }
            }
        }

        private void ResetValues()
        {
            foreach (SettingHandle h in Settings.Handles)
            {
                if (h is SettingHandle<int> handle)
                {
                    handle.ResetToDefault();
                }
            }
        }

        public void ResetControl(SettingHandle hanlde)
        {
            MethodInfo ResetHandleControlInfo = AccessTools.Method("HugsLib.Settings.Dialog_ModSettings:ResetHandleControlInfo");
            ResetHandleControlInfo.Invoke(Find.WindowStack.currentlyDrawnWindow, new object[] { hanlde });
        }

        public static bool Button(Rect rect, SettingHandle<bool> setting, string text)
        {
            bool change = false;
            if (Widgets.ButtonText(rect, text))
            {
                setting.Value = !setting.Value;
                change = true;
            }
            return change;
        }

        public static bool SearchField(Rect rect, SettingHandle<string> setting, string text)
        {
            bool change = false;
            float height = rect.height;
            Rect field = rect;
            field.width -= height;
            string input = Widgets.TextField(field, text);
            Rect button = new Rect(field.xMax, rect.y, height, height);
            if (Widgets.ButtonText(button,"x"/*CloseButtonFor(button)*/))
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
    }
}
