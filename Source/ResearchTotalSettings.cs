using Verse;

namespace ResearchTotal
{
    public class ResearchTotalSettings : ModSettings
    {
        public float targetPoints = 1000000f;
        public float roundTo = 10f;

        public float eraNeolithic = 1.00f;
        public float eraMedieval = 1.15f;
        public float eraIndustrial = 1.50f;
        public float eraSpacer = 2.00f;
        public float eraUltra = 2.50f;

        public void ResetToDefaults()
        {
            targetPoints = 1000000f;
            roundTo = 10f;
            eraNeolithic = 1.00f;
            eraMedieval = 1.15f;
            eraIndustrial = 1.50f;
            eraSpacer = 2.00f;
            eraUltra = 2.50f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref targetPoints, "targetPoints", -1f);
            Scribe_Values.Look(ref roundTo, "roundTo", -1f);
            Scribe_Values.Look(ref eraNeolithic, "eraNeolithic", 1.00f);
            Scribe_Values.Look(ref eraMedieval, "eraMedieval", 1.15f);
            Scribe_Values.Look(ref eraIndustrial, "eraIndustrial", 1.50f);
            Scribe_Values.Look(ref eraSpacer, "eraSpacer", 2.00f);
            Scribe_Values.Look(ref eraUltra, "eraUltra", 2.50f);

            if (Scribe.mode == LoadSaveMode.LoadingVars && targetPoints < 0f)
            {
                float legacy = 1000000f;
                Scribe_Values.Look(ref legacy, "defaultTargetPoints", 1000000f);
                targetPoints = legacy;
            }

            if (targetPoints < 1f)
            {
                targetPoints = 1000000f;
            }

            if (Scribe.mode == LoadSaveMode.LoadingVars && roundTo < 1f)
            {
                bool legacyTens = true;
                Scribe_Values.Look(ref legacyTens, "roundToTens", true);
                roundTo = legacyTens ? 10f : 1f;
            }

            if (roundTo < 1f)
            {
                roundTo = 1f;
            }

            if (roundTo > 10000f)
            {
                roundTo = 10000f;
            }

            eraNeolithic = ClampEra(eraNeolithic, 1.00f);
            eraMedieval = ClampEra(eraMedieval, 1.15f);
            eraIndustrial = ClampEra(eraIndustrial, 1.50f);
            eraSpacer = ClampEra(eraSpacer, 2.00f);
            eraUltra = ClampEra(eraUltra, 2.50f);
        }

        public static float ClampEra(float value, float fallback)
        {
            if (value < 0.25f || value > 10f)
            {
                return fallback;
            }

            return value;
        }
    }
}
