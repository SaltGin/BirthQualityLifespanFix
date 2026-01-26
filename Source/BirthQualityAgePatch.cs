using System;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;

namespace BirthQualityLifespanFix
{
    [StaticConstructorOnStartup]
    public static class BirthQualityAgePatch
    {
        static BirthQualityAgePatch()
        {
            var harmony = new Harmony("saltgin.birthqualitylifespanfix");
            harmony.PatchAll();
            //Log.Message("[BirthQualityLifespanFix] Patches applied.");
        }

        /// <summary>
        /// Transforms a pawn's biological age to human-equivalent age for the birth quality curve.
        /// 
        /// Vanilla curve from XML: (14, 0.0), (15, 0.3), (20, 0.5), (30, 0.5), (40, 0.3), (65, 0.0)
        /// 
        /// - Points 14, 15, 20: Scale by adult lifestage age ratio
        /// - Points 30, 40, 65: Scale by lifespan ratio
        /// This produces a peak quality plateau between human-equivalent ages 20 and 30.
        /// </summary>
        public static float GetHumanEquivalentAge(Pawn pawn)
        {
            if (pawn == null)
                return 0f;

            float bioAge = pawn.ageTracker.AgeBiologicalYearsFloat;

            // Get race mature age (fallback to 18 if not defined)
            float raceMatureAge = pawn.ageTracker.AdultMinAge;
            if (raceMatureAge <= 0f)
                raceMatureAge = 18f;

            // Get human mature age
            float humanMatureAge = ThingDefOf.Human.race.lifeStageAges
                .FirstOrDefault(lsa => lsa.def.developmentalStage.Adult())?.minAge ?? 18f;

            float raceLifespan = pawn.RaceProps.lifeExpectancy;
            float humanLifespan = ThingDefOf.Human.race.lifeExpectancy;

            // Edge cases
            if (raceMatureAge <= 0f || raceLifespan <= 0f || humanMatureAge <= 0f || humanLifespan <= 0f)
                return bioAge;

            // Calculate ratios
            float matureRatio = raceMatureAge / humanMatureAge;
            float lifespanRatio = raceLifespan / humanLifespan;

            // Calculate scaled turning points
            float scaled14 = 14f * matureRatio;
            float scaled15 = 15f * matureRatio;
            float scaled20 = 20f * matureRatio;
            float scaled30 = 30f * lifespanRatio;
            float scaled40 = 40f * lifespanRatio;
            float scaled65 = 65f * lifespanRatio;

            if (bioAge <= scaled14)
                return bioAge / matureRatio;

            if (bioAge <= scaled15)
                return 14f + (bioAge - scaled14) / (scaled15 - scaled14) * 1f;

            if (bioAge <= scaled20)
                return 15f + (bioAge - scaled15) / (scaled20 - scaled15) * 5f;

            if (bioAge <= scaled30)
                return 20f + (bioAge - scaled20) / (scaled30 - scaled20) * 10f;

            if (bioAge <= scaled40)
                return 30f + (bioAge - scaled30) / (scaled40 - scaled30) * 10f;

            if (bioAge <= scaled65)
                return 40f + (bioAge - scaled40) / (scaled65 - scaled40) * 25f;

            // Beyond scaled65: extrapolate
            return 65f + (bioAge - scaled65) / lifespanRatio;
        }
    }

    [HarmonyPatch(typeof(RitualOutcomeComp_PawnAge), nameof(RitualOutcomeComp_PawnAge.Count))]
    public static class Patch_Count
    {
        public static void Postfix(ref float __result, LordJob_Ritual ritual, RitualOutcomeComp_PawnAge __instance)
        {
            if (__instance.roleId != "mother")
                return;

            Pawn pawn = ritual?.PawnWithRole("mother");
            if (pawn == null || pawn.def == ThingDefOf.Human)
                return;

            __result = BirthQualityAgePatch.GetHumanEquivalentAge(pawn);
        }
    }

    [HarmonyPatch(typeof(RitualOutcomeComp_PawnAge), nameof(RitualOutcomeComp_PawnAge.GetDesc))]
    public static class Patch_GetDesc
    {
        public static void Postfix(
            ref string __result,
            LordJob_Ritual ritual,
            RitualOutcomeComp_PawnAge __instance,
            string ___label)
        {
            if (__instance.roleId != "mother")
                return;

            Pawn pawn = ritual.PawnWithRole("mother");
            if (pawn == null || pawn.def == ThingDefOf.Human)
                return;

            float equivalentAge = BirthQualityAgePatch.GetHumanEquivalentAge(pawn);
            float quality = __instance.curve.Evaluate(equivalentAge);
            string sign = quality < 0f ? "" : "+";

            __result = ___label.CapitalizeFirst().Formatted(pawn.Named("PAWN")) + ": " +
                       "OutcomeBonusDesc_QualitySingleOffset".Translate(sign + quality.ToStringPercent()) + ".";
        }
    }

    [HarmonyPatch(typeof(RitualOutcomeComp_PawnAge), nameof(RitualOutcomeComp_PawnAge.GetQualityFactor),
        new Type[] { typeof(Precept_Ritual), typeof(TargetInfo), typeof(RitualObligation), typeof(RitualRoleAssignments), typeof(RitualOutcomeComp_Data) })]
    public static class Patch_GetQualityFactor
    {
        public static void Postfix(
            ref QualityFactor __result,
            RitualRoleAssignments assignments,
            RitualOutcomeComp_PawnAge __instance,
            string ___label)
        {
            if (__result == null || __instance.roleId != "mother")
                return;

            Pawn pawn = assignments?.FirstAssignedPawn("mother");
            if (pawn == null || pawn.def == ThingDefOf.Human)
                return;

            float equivalentAge = BirthQualityAgePatch.GetHumanEquivalentAge(pawn);
            float quality = __instance.curve.Evaluate(equivalentAge);

            __result.label = ___label.Formatted(pawn.Named("PAWN"));
            __result.quality = quality;
            __result.positive = quality > 0f;
            __result.count = string.Format("{0:F0} (actual: {1})", equivalentAge, pawn.ageTracker.AgeBiologicalYears);

            if (quality > 0f)
                __result.qualityChange = quality.ToStringWithSign("0.#%");
            else
                __result.qualityChange = "QualityOutOf".Translate("+0", quality.ToStringWithSign("0.#%"));
        }
    }
}
