using UnityEngine;
using Verse;

namespace BirthQualityLifespanFix
{
    public class BirthQualitySettings : ModSettings
    {
        /// <summary>
        /// When true, short-lifespan races (lifespan < 80) will have their peak end
        /// scaled to at least human-equivalent 30, preventing the shrinking penalty.
        /// </summary>
        public bool preventShortLifespanPenalty = false;
        public bool AgelessAtPeakBirthQuality = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref preventShortLifespanPenalty, "preventShortLifespanPenalty", false);
            Scribe_Values.Look(ref AgelessAtPeakBirthQuality, "AgelessAtPeakBirthQuality", false);
            base.ExposeData();
        }
    }

    public class BirthQualityLifespanFix : Mod
    {
        public static BirthQualitySettings Settings { get; private set; }

        public BirthQualityLifespanFix(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BirthQualitySettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled(
                "BirthQualityLifespanFix_PreventShortLifespanPenalty".Translate(),
                ref Settings.preventShortLifespanPenalty
            );

            listing.SubLabel(
                "BirthQualityLifespanFix_PreventShortLifespanPenaltyDesc".Translate(), 1f);

            listing.CheckboxLabeled(
                "BirthQualityLifespanFix_AgelessAtPeakBirthQuality".Translate(),
                ref Settings.AgelessAtPeakBirthQuality
            );

            listing.SubLabel(
                "BirthQualityLifespanFix_AgelessAtPeakBirthQualityDesc".Translate(), 1f);

            listing.End();
        }

        public override string SettingsCategory()
        {
            return "Birth Quality Lifespan Fix";
        }
    }
}