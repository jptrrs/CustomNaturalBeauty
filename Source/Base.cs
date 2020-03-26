using System.Linq;
using HugsLib;
using Verse;
using HugsLib.Settings;
using RimWorld;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace CustomNaturalBeauty
{
    public class Base : ModBase
    {
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
            var terrain = DefDatabase<TerrainDef>.AllDefs.OrderBy(x => x.label);
            IEnumerable<BuildableDef> affected = plants.Cast<BuildableDef>().Concat(chunks.Cast<BuildableDef>()).Concat(terrain.Cast<BuildableDef>());

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

            foreach (BuildableDef e in affected)
            {
                bool hasBeauty = e.statBases != null && e.statBases.StatListContains(StatDefOf.Beauty);
                int presetValue = 0;
                bool isPreset = isCustom ? false : Extracted(source.StringValue, e.defName, out presetValue);
                int defBeauty = hasBeauty ? (int)e.statBases.First((StatModifier s) => s.stat == StatDefOf.Beauty).value : 0;
                BaseBeauty.Add(e.defName, defBeauty);
                int defaultValue = isPreset ? presetValue : defBeauty;
                //if (isPreset) Log.Message(e.defName + " is preset, presetValue is "+presetValue+", defBeauty is "+defBeauty+", assigned " + defaultValue);
                if (!hasBeauty)
                {
                    if (e.statBases == null) e.statBases = new List<StatModifier>();
                    e.statBases.Add(new StatModifier() { stat = StatDefOf.Beauty, value = defaultValue });
                }
                var customBeauty = Settings.GetHandle<int>(e.defName, e.label, e.description, defaultValue, Validators.IntRangeValidator(-50, +50));
                //RegisteredBeauty.Add(e.defName, customBeauty.Value);
                customBeauty.OnValueChanged = newValue =>
                {
                    //RegisteredBeauty[e.defName] = newValue;
                    e.SetStatBaseValue(StatDefOf.Beauty, newValue);
                    //Log.Message(e.defName + " beauty has changed to " + newValue);
                };
                e.SetStatBaseValue(StatDefOf.Beauty, customBeauty.Value);
            }
        }

        private void SetNewDefaults(string key, ProfileEnum newvalue)
        {
            foreach(SettingHandle h in Settings.Handles)
            {
                SettingHandle<int> handle = h as SettingHandle<int>;
                if (handle != null)
                {
                    if (newvalue > 0)
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

        //public static bool CustomDrawer_Button(Rect rect, SettingHandle<bool> setting, String activateText, String deactivateText, int xOffset = 0, int yOffset = 0)
        //{
        //    //int labelWidth = (int)rect.width - 20;
        //    //int horizontalOffset = 0;
        //    //int verticalOffset = 0;
        //    //bool change = false;
        //    //Rect buttonRect = new Rect(rect);
        //    //buttonRect.width = labelWidth;
        //    //buttonRect.position = new Vector2(buttonRect.position.x + horizontalOffset + xOffset, buttonRect.position.y + verticalOffset + yOffset);
        //    //Color activeColor = GUI.color;
        //    //bool isSelected = setting.Value;
        //    //String text = setting ? deactivateText : activateText;

        //    //if (isSelected)
        //    //    GUI.color = SelectedOptionColor;
        //    bool clicked = Widgets.ButtonText(buttonRect, text);
        //    //if (isSelected)
        //    //    GUI.color = activeColor;

        //    if (clicked)
        //    {
        //        setting.Value = !setting.Value;
        //        change = true;
        //    }
        //    return change;
        //}


    }
}
