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
using UnityEngine.UI;
using System.Text;

namespace CustomNaturalBeauty
{
    public class Base : ModBase
    {
        public static Dictionary<SettingHandle, bool> Matched = new Dictionary<SettingHandle, bool>();
        public static Dictionary<SettingHandle, int> Saved = new Dictionary<SettingHandle, int>();
        private static readonly int boxPadding = 2;
        private Dictionary<string, int> BaseBeauty = new Dictionary<string, int>();
        public Base()
        {
            Instance = this;
            Settings.EntryName = "Custom Natural Beauty";
        }

        private enum ProfileEnum
        { BaseGame, NatureIsBeautiful, BeautifulOutdoors, LastSaved }

        private ProfileEnum PresetSelected;

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

        public static Rect SplitRect(Rect rect, out Rect second, int trim = 3)
        {
            var trimWidht = trim * rect.height;
            Rect first = rect;
            first.width = rect.width - trimWidht;
            second = first;
            second.x = first.xMax + boxPadding;
            second.width = trimWidht - boxPadding;
            return first;
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
            source.ValueChanged += handle => PresetSelected = source.Value;//SetNewDefaults(source.StringValue, source.Value);
            source.CanBeReset = true;
            source.Unsaved = true;
            source.CustomDrawer = rect => DrawSettingDropDown(DrawWithGoButton(rect, SetFromPreset/*ResetValues*/, "Apply".Translate()), source);
            bool isCustom = source.Value == ProfileEnum.BaseGame;
            var searchField = Settings.GetHandle<string>("SearchField", "SearchField".Translate(), "SearchFieldDesc".Translate(), "");
            searchField.ValueChanged += handle => MatchSearch(handle.StringValue);
            searchField.Unsaved = true;
            searchField.CustomDrawer = rect => DrawSettingSearchField(rect, searchField, searchField.Value);
            //List<string> log = new List<string>();
            foreach (BuildableDef e in affected)
            {
                //string text;
                ProcessDef(source, e/*, out text*/);
                //log.AddItem(text);
            }
            //if (Prefs.LogVerbose)
            //{
            //    StringBuilder report = new StringBuilder();
            //    report.Append("[NaturalBeauty] Processed BuildableDefs: ");
            //    foreach (string i in log) report.AppendInNewLine(i);
            //    Log.Message(report.ToString());
            //}
        }

        public void ResetValues()
        {
            foreach (SettingHandle h in Settings.Handles)
            {
                if (h is SettingHandle<int> handle)
                {
                    Log.Message($"resetting {h.Name} to {handle.Value}");
                    handle.ResetToDefault();
                    handle.ForceSaveChanges();
                }
            }
        }

        private static bool DrawSettingDropDown(Rect rect, SettingHandle<ProfileEnum> setting)
        {
            bool change = false;
            if (Widgets.ButtonText(rect, setting.Value.ToString().Translate()))
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                foreach (ProfileEnum p in Enum.GetValues(typeof(ProfileEnum)))
                {
                    list.Add(new FloatMenuOption(p.ToString().Translate(), delegate ()
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
            if (def.statBases.NullOrEmpty())
            {
                Log.Error($"[CustomNaturalBeauty] Can't find any stats for {def}!");
                return null;
            }
            bool outBeauty = def.statBases.StatListContains(StatDefOf.BeautyOutdoors);
            bool hasBeauty = outBeauty || def.statBases.StatListContains(StatDefOf.Beauty);
            StatDef stat = outBeauty ? StatDefOf.BeautyOutdoors : StatDefOf.Beauty;
            defBeauty = hasBeauty ? (int)def.statBases.First((StatModifier s) => s.stat == stat).value : 0;
            if (!hasBeauty)
            {
                def.statBases = new List<StatModifier>();
                def.statBases.Add(new StatModifier() { stat = StatDefOf.Beauty, value = defBeauty });
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

        private void ProcessDef(SettingHandle<ProfileEnum> source, BuildableDef def/*, out string log*/)
        {
            string name = def.defName;
            //int presetValue = 0;
            int defValue;            
            bool saved = Settings.ValueExists(name);
            //bool ShouldDefaulthFromBase = source.Value == ProfileEnum.BaseGame || source.Value == ProfileEnum.LastSaved;
            //if (!ShouldDefaulthFromBase) Extracted(source.StringValue, e.defName, out presetValue);
            var tempstat = FindRelevantStat(def, out defValue);
            //int fallBackValue = ShouldDefaulthFromBase ? defValue : presetValue;
            var stat = (tempstat != null) ? tempstat : new StatDef();
            var customBeauty = Settings.GetHandle<int>(def.defName, def.label, def.description, defValue/*fallBackValue*/, Validators.IntRangeValidator(-50, +50));
            customBeauty.ValueChanged += handle =>
            {
                def.SetStatBaseValue(stat, customBeauty.Value);
                customBeauty.HasUnsavedChanges = true;
            };
            Matched.Add(customBeauty, true);
            customBeauty.VisibilityPredicate = delegate
            {
                return Matched[customBeauty];
            };
            def.SetStatBaseValue(stat, customBeauty.Value);
            if (saved) Saved.AddDistinct(customBeauty, customBeauty.Value);
            //log = "";
            //if (Prefs.LogVerbose)
            //{
            //    string origin = saved ? "saved" : (ShouldDefaulthFromBase ? "from def" : "from preset");
            //    log = $"{name}: {customBeauty.Value.ToString()}, {origin}";
            //}
        }

        //private void SetNewDefaults(string key, ProfileEnum source)
        //{
        //    foreach (SettingHandle h in Settings.Handles)
        //    {
        //        if (h is SettingHandle<int> handle)
        //        {
        //            string name = h.Name;
        //            if (source == ProfileEnum.LastSaved)
        //            {
        //                if (Saved.ContainsKey(handle))
        //                {
        //                    handle.DefaultValue = Saved[handle];
        //                }
        //            }
        //            else if (source > 0 && Extracted(key, name, out int presetValue)) // as in, not the base game
        //            {
        //                handle.DefaultValue = presetValue;
        //            }
        //            else
        //            {
        //                handle.DefaultValue = BaseBeauty.ContainsKey(name) ? BaseBeauty[name] : 0;
        //            }
        //        }
        //    }
        //}

        private void ResetSearch()
        {
            foreach (var handle in Matched.Keys.ToList())
            {
                Matched[handle] = true;
            }
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
                        if (Saved.ContainsKey(handle))
                        {
                            handle.Value = Saved[handle];
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