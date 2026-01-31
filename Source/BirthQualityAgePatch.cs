using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

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

            // 2 Reference Points that logic switches on
            const float HumanPeakStart = 20f; // End of growth curve
            const float HumanPeakEnd = 30f;   // Start of decay curve

            if (BirthQualityLifespanFix.Settings.AgelessAtPeakBirthQuality
                && pawn.genes?.BiologicalAgeTickFactor == 0f) // Ageless gene
            {
                return Math.Min(bioAge, HumanPeakEnd);
            }

            float bioPeakStart = HumanPeakStart * matureRatio;
            float bioPeakEnd = HumanPeakEnd * lifespanRatio;

            if (BirthQualityLifespanFix.Settings.preventShortLifespanPenalty && lifespanRatio < 1f)
            {
                float guaranteedEnd = bioPeakStart + (HumanPeakEnd - HumanPeakStart); // effectively Start + 10

                // Take whichever is better
                bioPeakEnd = Math.Max(bioPeakEnd, guaranteedEnd);
            }

            // Safety lock
            bioPeakEnd = Math.Max(bioPeakEnd, bioPeakStart);

            // The pawn is still growing (Equivalent < 20)
            if (bioAge <= bioPeakStart)
            {
                return bioAge / matureRatio;
            }

            // The pawn is past their prime (Equivalent > 30)
            if (bioAge >= bioPeakEnd)
            {
                return HumanPeakEnd + ((bioAge - bioPeakEnd) / lifespanRatio);
            }

            // The pawn is in their prime (Equivalent between 20 and 30)
            float range = bioPeakEnd - bioPeakStart;
            if (range <= 0.01f) return HumanPeakStart;

            float progress = (bioAge - bioPeakStart) / range;
            return HumanPeakStart + (progress * (HumanPeakEnd - HumanPeakStart));
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
            if (pawn == null || (pawn.def == ThingDefOf.Human && pawn.genes?.BiologicalAgeTickFactor == 1f))
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
            if (pawn == null || (pawn.def == ThingDefOf.Human && pawn.genes?.BiologicalAgeTickFactor == 1f))
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
            if (pawn == null || (pawn.def == ThingDefOf.Human && pawn.genes?.BiologicalAgeTickFactor == 1f))
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