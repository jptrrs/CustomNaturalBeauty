using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace CustomNaturalBeauty
{
    public class StatPart_PlantBeauty : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            Plant plant = req.Thing as Plant;
            if (req.HasThing && plant != null)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("plantGrowth".Translate() + ": x" + plant.Growth.ToStringByStyle(ToStringStyle.PercentOne));
                return stringBuilder.ToString().TrimEndNewlines();
            }
            return null;
        }

        public override void TransformValue(StatRequest req, ref float value)
        {
            Plant plant = req.Thing as Plant;
            if (req.HasThing && plant != null)
            {
                Log.Warning($"{plant} beaty is {value}");
                value *= plant.Growth;
            }
        }
            
    }
}