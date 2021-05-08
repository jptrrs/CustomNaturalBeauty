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
        public static string SearchField = "";
        public static Dictionary<SettingHandle, bool> Matched = new Dictionary<SettingHandle, bool>();
        private static List<SettingHandle> allHandles = new List<SettingHandle>();
        internal static Base Instance { get; private set; }
        public override string ModIdentifier
        {
            get { return "CustomNaturalBeauty"; }
        }

        public Base()
        {
            Instance = this;
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
            var terrain = DefDatabase<TerrainDef>.AllDefs.OrderBy(x => x.label);
            IEnumerable<BuildableDef> affected = plants.Cast<BuildableDef>().Concat(chunks.Cast<BuildableDef>()).Concat(rocks.Cast<BuildableDef>()).Concat(terrain.Cast<BuildableDef>());

            var source = Settings.GetHandle("DefaultsFrom", "DefaultsFromTitle".Translate(), "DefaultsFromDesc".Translate(), ProfileEnum.BaseGame, null, "profile");
            source.OnValueChanged = newvalue =>
            {
                SetNewDefaults(source.StringValue, newvalue);
            };
            bool isCustom = source.Value == 0;

            var resetButton = Settings.GetHandle<bool>("ResetButton", "", "ResetToDefaultDesc".Translate());
            resetButton.Unsaved = true;
            resetButton.CustomDrawer = rect => Button(rect,resetButton, "ResetToDefault".Translate());
            resetButton.OnValueChanged = delegate 
            {
                ResetValues();
            };

            var searchField =  Settings.GetHandle<string>("SearchField", "SearchField", "SearchField", "");
            searchField.OnValueChanged = searchWord =>
            {
                MatchSearch(searchWord);
            };
            SearchField = searchField;

            foreach (BuildableDef e in affected)
            {
                bool hasBeauty = e.statBases != null && e.statBases.StatListContains(StatDefOf.Beauty);
                int presetValue = 0;
                bool isPreset = isCustom ? false : Extracted(source.StringValue, e.defName, out presetValue);
                int defBeauty = hasBeauty ? (int)e.statBases.First((StatModifier s) => s.stat == StatDefOf.Beauty).value : 0;
                BaseBeauty.Add(e.defName, defBeauty);
                int defaultValue = isPreset ? presetValue : defBeauty;
                if (!hasBeauty)
                {
                    if (e.statBases == null) e.statBases = new List<StatModifier>();
                    e.statBases.Add(new StatModifier() { stat = StatDefOf.Beauty, value = defaultValue });
                }
                var customBeauty = Settings.GetHandle<int>(e.defName, e.label, e.description, defaultValue, Validators.IntRangeValidator(-50, +50));
                customBeauty.OnValueChanged = newValue =>
                {
                    e.SetStatBaseValue(StatDefOf.Beauty, newValue);
                    ResetControl(customBeauty);
                };
                allHandles.Add(customBeauty);
                e.SetStatBaseValue(StatDefOf.Beauty, customBeauty.Value);
            }
        }

        private void MatchSearch(string word)
        {
            Log.Warning($"Searching {word}");
            foreach (var handle in allHandles)
            {
                handle.VisibilityPredicate = () => handle.Title.Contains(word);
                ResetControl(handle);
            }


            //if (SearchField.Count() > 1)
            //{
            //    int length = Matched.EnumerableCount();
            //    for (int i = 0; i < length; i++)
            //    {
            //        var key = Matched.ElementAt(i).Key;
            //        Matched[key] = key.Contains(SearchField);
            //    }
            //}
            //else
            //{
            //    foreach (var e in Matched.Keys) Matched[e] = true;
            //}
        }

        private void SetNewDefaults(string key, ProfileEnum newvalue)
        {
            foreach(SettingHandle h in Settings.Handles)
            {
                SettingHandle<int> handle = h as SettingHandle<int>;
                if (handle != null)
                {
                    if (newvalue  > 0)
                    {
                        int presetValue = 0;
                        bool isPreset = Extracted(key, handle.Name, out presetValue);
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
                SettingHandle<int> handle = h as SettingHandle<int>;
                if (handle != null)
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
            bool clicked = Widgets.ButtonText(rect, text);
            if (clicked)
            {
                setting.Value = !setting.Value;
                change = true;
            }
            return change;
        }
    }
}
