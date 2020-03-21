using System.Linq;
using HugsLib;
using Verse;
using HugsLib.Settings;
using RimWorld;
using System.Collections.Generic;

namespace NatureIsBeautiful
{
    public class Base : ModBase
    {
        internal static Base Instance { get; private set; }

        public override string ModIdentifier
        {
            get { return "NatureIsBeautiful"; }
        }

        public Base()
        {
            Instance = this;
        }

        private enum HandleEnum { DefaultValue, ValueOne, ValueTwo }

        public override void DefsLoaded()
        {
            foreach (TerrainDef t in DefDatabase<TerrainDef>.AllDefs.Where(x => x.statBases != null && x.defName.EndsWith("_Rough"))) 
            {
                t.statBases.First((StatModifier s) => s.stat == StatDefOf.Beauty).value = 1f;
            }
            var plants = DefDatabase<ThingDef>.AllDefs.Where(x => x.plant != null).OrderBy(x => x.label);
            var chunks = DefDatabase<ThingDef>.AllDefs.Where(x => x.defName.StartsWith("Chunk")).OrderBy(x => x.label);
            var terrain = DefDatabase<TerrainDef>.AllDefs.OrderBy(x => x.label);
            IEnumerable<BuildableDef> affected = plants.Cast<BuildableDef>().Concat(chunks.Cast<BuildableDef>()).Concat(terrain.Cast<BuildableDef>());
            //var enumHandle = Settings.GetHandle("enumThing", "enumSetting_title".Translate(), "enumSetting_desc".Translate(), HandleEnum.DefaultValue, null, "enumSetting_");
            //enumHandle.OnValueChanged = newvalue =>
            //{
            //    SetDefaults(newvalue);
            //};
            //BeautySet.Add("Agrilux", 13);
            foreach (BuildableDef e in affected)
            {
                bool hasBeauty = e.statBases != null && e.statBases.StatListContains(StatDefOf.Beauty);
                int defBeauty = hasBeauty ? (int)e.statBases.First((StatModifier s) => s.stat == StatDefOf.Beauty).value : 0;
                if (!hasBeauty)
                {
                    if (e.statBases == null) e.statBases = new List<StatModifier>();
                    e.statBases.Add(new StatModifier() { stat = StatDefOf.Beauty, value = 0 });
                }
                var customBeauty = Settings.GetHandle<int>(e.defName, e.label, e.description, defBeauty, Validators.IntRangeValidator(-20, +20));
                customBeauty.OnValueChanged = newValue =>
                {
                    e.SetStatBaseValue(StatDefOf.Beauty, newValue);
                };
                //plant.statBases.First((StatModifier s) => s.stat == StatDefOf.Beauty).value = customBeauty;
                //if (BeautySet.ContainsKey(plant.defName))
                //{
                //    Log.Message(plant.defName + " found in dictionary");
                //    customBeauty.Value = BeautySet[plant.defName];
                //}
            }
        }

    }
}
