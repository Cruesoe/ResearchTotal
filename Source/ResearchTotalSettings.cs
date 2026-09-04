using Verse;

namespace ResearchTotal
{
    public class ResearchTotalSettings : ModSettings
    {
        public float targetPoints = 1000000f;
        public float roundTo = 10f;

        public float eraAnimal = 1.00f;
        public float eraNeolithic = 1.00f;
        public float eraMedieval = 1.15f;
        public float eraIndustrial = 1.50f;
        public float eraSpacer = 2.00f;
        public float eraUltra = 2.50f;

        public float targetAnomalyPoints = 5000f;
        public float roundAnomalyTo = 1f;
        public float anomalyBasic = 1.00f;
        public float anomalyAdvanced = 1.50f;

        public float targetGravshipPoints = 5000f;
        public float roundGravshipTo = 10f;
        public float gravNeolithic = 1.00f;
        public float gravMedieval = 1.15f;
        public float gravIndustrial = 1.50f;
        public float gravSpacer = 2.00f;
        public float gravUltra = 2.50f;

        public void ResetStandard()
        {
            targetPoints = 1000000f;
            roundTo = 10f;
            eraAnimal = 1.00f;
            eraNeolithic = 1.00f;
            eraMedieval = 1.15f;
            eraIndustrial = 1.50f;
            eraSpacer = 2.00f;
            eraUltra = 2.50f;
        }

        public void ResetAnomaly()
        {
            targetAnomalyPoints = 5000f;
            roundAnomalyTo = 1f;
            anomalyBasic = 1.00f;
            anomalyAdvanced = 1.50f;
        }

        public void ResetGravship()
        {
            targetGravshipPoints = 5000f;
            roundGravshipTo = 10f;
            gravNeolithic = 1.00f;
            gravMedieval = 1.15f;
            gravIndustrial = 1.50f;
            gravSpacer = 2.00f;
            gravUltra = 2.50f;
        }

        public void ResetToDefaults()
        {
            ResetStandard();
            ResetAnomaly();
            ResetGravship();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref targetPoints, "targetPoints", -1f);
            Scribe_Values.Look(ref roundTo, "roundTo", -1f);
            Scribe_Values.Look(ref eraAnimal, "eraAnimal", 1.00f);
            Scribe_Values.Look(ref eraNeolithic, "eraNeolithic", 1.00f);
            Scribe_Values.Look(ref eraMedieval, "eraMedieval", 1.15f);
            Scribe_Values.Look(ref eraIndustrial, "eraIndustrial", 1.50f);
            Scribe_Values.Look(ref eraSpacer, "eraSpacer", 2.00f);
            Scribe_Values.Look(ref eraUltra, "eraUltra", 2.50f);
            Scribe_Values.Look(ref targetAnomalyPoints, "targetAnomalyPoints", 5000f);
            Scribe_Values.Look(ref roundAnomalyTo, "roundAnomalyTo", 1f);
            Scribe_Values.Look(ref anomalyBasic, "anomalyBasic", 1.00f);
            Scribe_Values.Look(ref anomalyAdvanced, "anomalyAdvanced", 1.50f);
            Scribe_Values.Look(ref targetGravshipPoints, "targetGravshipPoints", 5000f);
            Scribe_Values.Look(ref roundGravshipTo, "roundGravshipTo", 10f);
            Scribe_Values.Look(ref gravNeolithic, "gravNeolithic", 1.00f);
            Scribe_Values.Look(ref gravMedieval, "gravMedieval", 1.15f);
            Scribe_Values.Look(ref gravIndustrial, "gravIndustrial", 1.50f);
            Scribe_Values.Look(ref gravSpacer, "gravSpacer", 2.00f);
            Scribe_Values.Look(ref gravUltra, "gravUltra", 2.50f);

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

            eraAnimal = ClampEra(eraAnimal, 1.00f);
            eraNeolithic = ClampEra(eraNeolithic, 1.00f);
            eraMedieval = ClampEra(eraMedieval, 1.15f);
            eraIndustrial = ClampEra(eraIndustrial, 1.50f);
            eraSpacer = ClampEra(eraSpacer, 2.00f);
            eraUltra = ClampEra(eraUltra, 2.50f);
            anomalyBasic = ClampEra(anomalyBasic, 1.00f);
            anomalyAdvanced = ClampEra(anomalyAdvanced, 1.50f);
            gravNeolithic = ClampEra(gravNeolithic, 1.00f);
            gravMedieval = ClampEra(gravMedieval, 1.15f);
            gravIndustrial = ClampEra(gravIndustrial, 1.50f);
            gravSpacer = ClampEra(gravSpacer, 2.00f);
            gravUltra = ClampEra(gravUltra, 2.50f);

            if (targetAnomalyPoints < 1f)
            {
                targetAnomalyPoints = 5000f;
            }

            if (roundAnomalyTo < 1f)
            {
                roundAnomalyTo = 1f;
            }

            if (roundAnomalyTo > 10000f)
            {
                roundAnomalyTo = 10000f;
            }

            if (targetGravshipPoints < 1f)
            {
                targetGravshipPoints = 5000f;
            }

            if (roundGravshipTo < 1f)
            {
                roundGravshipTo = 10f;
            }

            if (roundGravshipTo > 10000f)
            {
                roundGravshipTo = 10000f;
            }
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
