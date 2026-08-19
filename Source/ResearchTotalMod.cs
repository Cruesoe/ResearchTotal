using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Verse;

namespace ResearchTotal
{
    public class ResearchTotalMod : Mod
    {
        public static ResearchTotalSettings settings;

        private static readonly Color LabelColor = new Color(0.72f, 0.72f, 0.72f);
        private static readonly Color ValueColor = new Color(0.92f, 0.92f, 0.88f);
        private static readonly Color AccentColor = new Color(1f, 0.85f, 0.45f);
        private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color HeaderColor = new Color(0.55f, 0.55f, 0.55f);

        private string targetBuffer;
        private string roundBuffer;
        private string bufferNeo;
        private string bufferMed;
        private string bufferInd;
        private string bufferSpa;
        private string bufferUlt;

        public ResearchTotalMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<ResearchTotalSettings>();
            new Harmony("cruesoe.research.total").PatchAll();
        }

        public override string SettingsCategory()
        {
            return "Research: Total";
        }

        public override void WriteSettings()
        {
            ClampAll();
            base.WriteSettings();
            if (ResearchTotalEngine.HasColony())
            {
                ResearchTotalEngine.RecalculateRemaining();
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ClampAll();

            float y = inRect.y;
            float w = inRect.width;

            Rect roundRow = new Rect(inRect.x, y, w, 28f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = LabelColor;
            Widgets.Label(roundRow.LeftPart(0.32f), "Round to nearest");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Rect roundField = new Rect(roundRow.x + roundRow.width * 0.32f, roundRow.y, 120f, roundRow.height);
            Widgets.TextFieldNumeric(roundField.ContractedBy(0f, 1f), ref settings.roundTo, ref roundBuffer, 1f, 10000f);
            y += 36f;

            Widgets.DrawLineHorizontal(inRect.x, y, w, LineColor);
            y += 12f;

            GUI.color = LabelColor;
            Widgets.Label(new Rect(inRect.x, y, w, 22f), "Research point target");
            GUI.color = Color.white;
            y += 24f;

            DrawTargetField(new Rect(inRect.x, y, w, 30f));
            y += 38f;

            DrawPresets(new Rect(inRect.x, y, w, 28f));
            y += 40f;

            GUI.color = LabelColor;
            Widgets.Label(new Rect(inRect.x, y, w, 22f), "Era weights  ·  later eras take more of the target because research gets faster");
            GUI.color = Color.white;
            y += 24f;

            DrawEraWeights(new Rect(inRect.x, y, w, 28f));
            y += 40f;

            Widgets.DrawLineHorizontal(inRect.x, y, w, LineColor);
            y += 12f;

            float resetH = 28f;
            float remain = inRect.yMax - resetH - 8f - y;
            bool inColony = ResearchTotalEngine.HasColony();
            float summaryH = inColony ? 72f : 52f;
            if (summaryH > remain * 0.4f)
            {
                summaryH = remain * 0.4f;
            }

            Rect eras = new Rect(inRect.x, y, w, remain - summaryH - 8f);
            if (eras.height > 40f)
            {
                Widgets.DrawMenuSection(eras);
                DrawEraTable(eras.ContractedBy(12f, 6f));
            }

            Rect summary = new Rect(inRect.x, inRect.yMax - resetH - 8f - summaryH, w, summaryH);
            Widgets.DrawMenuSection(summary);
            DrawStats(summary.ContractedBy(12f, 6f));

            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - resetH, 180f, resetH), "Reset to defaults"))
            {
                settings.ResetToDefaults();
                targetBuffer = null;
                roundBuffer = null;
                bufferNeo = null;
                bufferMed = null;
                bufferInd = null;
                bufferSpa = null;
                bufferUlt = null;
                ClampAll();
                if (ResearchTotalEngine.HasColony())
                {
                    ResearchTotalEngine.RecalculateRemaining();
                }
            }
        }

        private void DrawTargetField(Rect rect)
        {
            if (string.IsNullOrEmpty(targetBuffer) || targetBuffer.IndexOf('E') >= 0 || targetBuffer.IndexOf('e') >= 0)
            {
                targetBuffer = FormatTarget(settings.targetPoints);
            }

            string text = Widgets.TextField(rect, targetBuffer);
            if (text == targetBuffer)
            {
                return;
            }

            StringBuilder digits = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    digits.Append(text[i]);
                }
            }

            targetBuffer = digits.ToString();
            if (targetBuffer.Length == 0)
            {
                return;
            }

            if (float.TryParse(targetBuffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out float parsed))
            {
                settings.targetPoints = Mathf.Clamp(parsed, Floor(), ResearchTotalEngine.MaxBudget);
            }
        }

        private void DrawPresets(Rect rect)
        {
            float gap = 6f;
            float bw = (rect.width - gap * 4f) / 5f;
            Rect vanilla = new Rect(rect.x, rect.y, bw, rect.height);
            if (Widgets.ButtonText(vanilla, "Vanilla"))
            {
                settings.targetPoints = Mathf.Max(1f, ResearchTotalEngine.VanillaApparentTotal());
                targetBuffer = FormatTarget(settings.targetPoints);
                if (ResearchTotalEngine.HasColony())
                {
                    ResearchTotalEngine.RecalculateRemaining();
                }
            }

            DrawPreset(new Rect(rect.x + (bw + gap), rect.y, bw, rect.height), "500k", 500000f);
            DrawPreset(new Rect(rect.x + (bw + gap) * 2f, rect.y, bw, rect.height), "1M", 1000000f);
            DrawPreset(new Rect(rect.x + (bw + gap) * 3f, rect.y, bw, rect.height), "2.5M", 2500000f);
            DrawPreset(new Rect(rect.x + (bw + gap) * 4f, rect.y, bw, rect.height), "5M", 5000000f);
        }

        private void DrawPreset(Rect rect, string label, float amount)
        {
            if (!Widgets.ButtonText(rect, label))
            {
                return;
            }

            settings.targetPoints = Mathf.Clamp(amount, Floor(), ResearchTotalEngine.MaxBudget);
            targetBuffer = FormatTarget(settings.targetPoints);
            if (ResearchTotalEngine.HasColony())
            {
                ResearchTotalEngine.RecalculateRemaining();
            }
        }

        private void DrawEraWeights(Rect rect)
        {
            float gap = 6f;
            float w = (rect.width - gap * 4f) / 5f;
            DrawEraField(new Rect(rect.x, rect.y, w, rect.height), "Neo", ref settings.eraNeolithic, ref bufferNeo);
            DrawEraField(new Rect(rect.x + (w + gap), rect.y, w, rect.height), "Med", ref settings.eraMedieval, ref bufferMed);
            DrawEraField(new Rect(rect.x + (w + gap) * 2f, rect.y, w, rect.height), "Ind", ref settings.eraIndustrial, ref bufferInd);
            DrawEraField(new Rect(rect.x + (w + gap) * 3f, rect.y, w, rect.height), "Spa", ref settings.eraSpacer, ref bufferSpa);
            DrawEraField(new Rect(rect.x + (w + gap) * 4f, rect.y, w, rect.height), "Ultra", ref settings.eraUltra, ref bufferUlt);
        }

        private static void DrawEraField(Rect rect, string label, ref float value, ref string buffer)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = LabelColor;
            Widgets.Label(rect.LeftPart(0.42f), label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(rect.RightPart(0.58f).ContractedBy(0f, 2f), ref value, ref buffer, 0.25f, 10f);
        }

        private static void DrawStats(Rect rect)
        {
            float vanilla = ResearchTotalEngine.VanillaApparentTotal();
            float target = ResearchTotalEngine.EffectiveTarget();
            int count = ResearchTotalEngine.IncludedCount();
            float ratio = vanilla > 0f ? target / vanilla : 0f;
            bool inColony = ResearchTotalEngine.HasColony();
            float row = rect.height / (inColony ? 3f : 2f);

            DrawStatRow(new Rect(rect.x, rect.y, rect.width, row), "Vanilla tree", Pts(vanilla) + "   ·   " + count + " projects", false);
            DrawStatRow(new Rect(rect.x, rect.y + row, rect.width, row), "Scaled total", Pts(target) + "   ·   " + ratio.ToString("0.00") + "x", true);
            if (!inColony)
            {
                return;
            }

            float spent = ResearchTotalEngine.CurrentSpent();
            DrawStatRow(new Rect(rect.x, rect.y + row * 2f, rect.width, row), "Completed and Total", Pts(spent) + " / " + Pts(target), false);
        }

        private static void DrawEraTable(Rect rect)
        {
            List<EraSlice> slices = ResearchTotalEngine.BuildEraSlices();
            float headerH = 22f;
            DrawEraHeader(new Rect(rect.x, rect.y, rect.width, headerH));
            if (slices.Count == 0)
            {
                return;
            }

            float rowH = Mathf.Min(24f, (rect.height - headerH) / (slices.Count + 1));
            int techs = 0;
            float vanilla = 0f;
            float scaled = 0f;
            for (int i = 0; i < slices.Count; i++)
            {
                EraSlice slice = slices[i];
                techs += slice.count;
                vanilla += slice.vanilla;
                scaled += slice.scaled;
                DrawEraRow(new Rect(rect.x, rect.y + headerH + rowH * i, rect.width, rowH), slice, i % 2 == 0);
            }

            EraSlice total = new EraSlice("Total");
            total.count = techs;
            total.vanilla = vanilla;
            total.scaled = scaled;
            DrawEraTotal(new Rect(rect.x, rect.y + headerH + rowH * slices.Count, rect.width, rowH), total);
        }

        private static void DrawEraHeader(Rect rect)
        {
            GUI.color = HeaderColor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(Col(rect, 0f, 0.28f), "Era");
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(Col(rect, 0.28f, 0.14f), "Techs");
            Widgets.Label(Col(rect, 0.42f, 0.29f), "Vanilla");
            Widgets.Label(Col(rect, 0.71f, 0.29f), "Scaled");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private static void DrawEraRow(Rect rect, EraSlice slice, bool stripe)
        {
            if (stripe)
            {
                Widgets.DrawHighlight(rect);
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = LabelColor;
            Widgets.Label(Col(rect, 0f, 0.28f), slice.label);
            GUI.color = ValueColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(Col(rect, 0.28f, 0.14f), slice.count.ToString());
            Widgets.Label(Col(rect, 0.42f, 0.29f), Pts(slice.vanilla));
            GUI.color = AccentColor;
            Widgets.Label(Col(rect, 0.71f, 0.29f), Pts(slice.scaled));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private static void DrawEraTotal(Rect rect, EraSlice slice)
        {
            Widgets.DrawTitleBG(rect);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = AccentColor;
            Widgets.Label(Col(rect, 0f, 0.28f), slice.label);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(Col(rect, 0.28f, 0.14f), slice.count.ToString());
            Widgets.Label(Col(rect, 0.42f, 0.29f), Pts(slice.vanilla));
            Widgets.Label(Col(rect, 0.71f, 0.29f), Pts(slice.scaled));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private static Rect Col(Rect rect, float start, float width)
        {
            return new Rect(rect.x + rect.width * start, rect.y, rect.width * width, rect.height);
        }

        private static void DrawStatRow(Rect rect, string label, string value, bool accent)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = LabelColor;
            Widgets.Label(rect.LeftPart(0.32f), label);
            GUI.color = accent ? AccentColor : ValueColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(rect.RightPart(0.68f), value);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void ClampAll()
        {
            settings.targetPoints = Mathf.Clamp(settings.targetPoints, Floor(), ResearchTotalEngine.MaxBudget);
            settings.roundTo = Mathf.Clamp(Mathf.Round(settings.roundTo), 1f, 10000f);
            settings.eraNeolithic = ResearchTotalSettings.ClampEra(settings.eraNeolithic, 1.00f);
            settings.eraMedieval = ResearchTotalSettings.ClampEra(settings.eraMedieval, 1.15f);
            settings.eraIndustrial = ResearchTotalSettings.ClampEra(settings.eraIndustrial, 1.50f);
            settings.eraSpacer = ResearchTotalSettings.ClampEra(settings.eraSpacer, 2.00f);
            settings.eraUltra = ResearchTotalSettings.ClampEra(settings.eraUltra, 2.50f);
        }

        private static float Floor()
        {
            return Mathf.Max(1f, ResearchTotalEngine.VanillaApparentTotal());
        }

        private static string FormatTarget(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp(value, 0f, ResearchTotalEngine.MaxBudget)).ToString(CultureInfo.InvariantCulture);
        }

        private static string Pts(float value)
        {
            return Mathf.RoundToInt(value).ToString("N0");
        }
    }
}
