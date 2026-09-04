using HarmonyLib;
using System;
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
        private static readonly Color ResetButtonColor = new Color(0.48f, 0.12f, 0.12f);
        private const string AnomalyNote = "Entity study uses knowledge, not bench research points. This total is separate from the tech tree above. Tech-level penalties do not apply.";
        private const string GravshipNote = "Gravtech is researched with gravdata, not the bench. These projects are pulled out of the main total when Vanilla Gravship Expanded is installed.";

        private string targetBuffer;
        private string roundBuffer;
        private string bufferAni;
        private string bufferNeo;
        private string bufferMed;
        private string bufferInd;
        private string bufferSpa;
        private string bufferUlt;
        private string anomalyTargetBuffer;
        private string anomalyRoundBuffer;
        private string bufferAnomalyBasic;
        private string bufferAnomalyAdvanced;
        private string gravTargetBuffer;
        private string gravRoundBuffer;
        private string bufferGravNeo;
        private string bufferGravMed;
        private string bufferGravInd;
        private string bufferGravSpa;
        private string bufferGravUlt;
        private Vector2 scrollPosition;
        private bool standardExpanded;
        private bool anomalyExpanded;
        private bool gravshipExpanded;
        private int lastSettingsFrame = -100;

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
            if (Time.frameCount > lastSettingsFrame + 1)
            {
                standardExpanded = false;
                anomalyExpanded = false;
                gravshipExpanded = false;
                scrollPosition = Vector2.zero;
            }

            lastSettingsFrame = Time.frameCount;

            bool anomalyActive = ResearchTotalEngine.AnomalyActive();
            bool gravshipActive = ResearchTotalEngine.GravshipActive();
            List<EraSlice> eraSlices = standardExpanded ? ResearchTotalEngine.BuildEraSlices() : null;
            List<EraSlice> anomalySlices = anomalyActive && anomalyExpanded ? ResearchTotalEngine.BuildAnomalySlices() : null;
            List<EraSlice> gravshipSlices = gravshipActive && gravshipExpanded ? ResearchTotalEngine.BuildGravshipSlices() : null;

            float contentWidth = inRect.width - 16f;
            float contentHeight = MeasureContentHeight(contentWidth, eraSlices, anomalySlices, gravshipSlices);
            Rect viewRect = new Rect(0f, 0f, contentWidth, Mathf.Max(contentHeight, inRect.height));
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            float y = 0f;
            float w = viewRect.width;
            y = DrawSectionHeader(y, w, "Standard Research", "Bench research. All standard techs scale to this total. Anomaly knowledge and Gravtech are separate when those mods are active.", "Reset Standard Research to defaults.", ref standardExpanded, ResetStandardSection);
            if (standardExpanded)
            {
                y = DrawStandardSection(new Rect(0f, y, w, 1f), eraSlices);
            }

            if (anomalyActive)
            {
                y += 8f;
                y = DrawSectionHeader(y, w, "Anomaly", "Dark study uses knowledge, not bench research points. This total is separate from the tech tree. Tech-level penalties do not apply.", "Reset Anomaly to defaults.", ref anomalyExpanded, ResetAnomalySection);
                if (anomalyExpanded)
                {
                    y = DrawAnomalySection(new Rect(0f, y, w, 1f), anomalySlices);
                }
            }

            if (gravshipActive)
            {
                y += 8f;
                y = DrawSectionHeader(y, w, "Vanilla Gravship Expanded", "Gravtech research from Vanilla Gravship Expanded. This total is separate from the main tech tree.", "Reset Vanilla Gravship Expanded to defaults.", ref gravshipExpanded, ResetGravshipSection);
                if (gravshipExpanded)
                {
                    y = DrawGravshipSection(new Rect(0f, y, w, 1f), gravshipSlices);
                }
            }

            Widgets.EndScrollView();
        }

        private float DrawSectionHeader(float y, float w, string label, string headerTip, string resetTip, ref bool expanded, Action onReset)
        {
            Text.Font = GameFont.Medium;
            float height = Text.LineHeight + 8f;
            Rect row = new Rect(0f, y, w, height);
            Rect resetRect = new Rect(row.xMax - 110f, row.y + (row.height - 30f) / 2f, 110f, 30f);
            Rect toggleRect = new Rect(row.x, row.y, resetRect.x - row.x - 8f, row.height);

            Widgets.DrawHighlightIfMouseover(toggleRect);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(toggleRect, (expanded ? "▼  " : "▶  ") + label);
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(toggleRect, headerTip);
            if (Widgets.ButtonInvisible(toggleRect))
            {
                expanded = !expanded;
            }

            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(resetRect, resetTip);
            if (DrawColoredButton(resetRect, "Reset".Translate(), ResetButtonColor, Color.white))
            {
                onReset?.Invoke();
            }

            y += height + 6f;
            Widgets.DrawLineHorizontal(0f, y, w, LineColor);
            y += 10f;
            return y;
        }

        private static bool DrawColoredButton(Rect rect, string label, Color background, Color textColor)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color fill = Mouse.IsOver(rect)
                    ? Color.Lerp(background, Color.white, 0.12f)
                    : background;
                Widgets.DrawBoxSolid(rect, new Color(fill.r, fill.g, fill.b, 0.90f));
                TextAnchor previousAnchor = Text.Anchor;
                Color previous = GUI.color;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = textColor;
                Widgets.Label(rect, label);
                GUI.color = previous;
                Text.Anchor = previousAnchor;
            }

            if (Event.current.type != EventType.MouseDown || Event.current.button != 0 || !Mouse.IsOver(rect))
            {
                return false;
            }

            Event.current.Use();
            return true;
        }

        private void ResetStandardSection()
        {
            settings.ResetStandard();
            targetBuffer = null;
            roundBuffer = null;
            bufferAni = null;
            bufferNeo = null;
            bufferMed = null;
            bufferInd = null;
            bufferSpa = null;
            bufferUlt = null;
            AfterSectionReset();
        }

        private void ResetAnomalySection()
        {
            settings.ResetAnomaly();
            anomalyTargetBuffer = null;
            anomalyRoundBuffer = null;
            bufferAnomalyBasic = null;
            bufferAnomalyAdvanced = null;
            AfterSectionReset();
        }

        private void ResetGravshipSection()
        {
            settings.ResetGravship();
            gravTargetBuffer = null;
            gravRoundBuffer = null;
            bufferGravNeo = null;
            bufferGravMed = null;
            bufferGravInd = null;
            bufferGravSpa = null;
            bufferGravUlt = null;
            AfterSectionReset();
        }

        private void AfterSectionReset()
        {
            ClampAll();
            if (ResearchTotalEngine.HasColony())
            {
                ResearchTotalEngine.RecalculateRemaining();
            }
        }

        private float DrawStandardSection(Rect start, List<EraSlice> eraSlices)
        {
            float y = start.y;
            float w = start.width;

            Rect roundRow = new Rect(start.x, y, w, 28f);
            DrawRoundRow(roundRow, "Round to nearest", ref settings.roundTo, ref roundBuffer);
            y += 36f;

            Widgets.DrawLineHorizontal(start.x, y, w, LineColor);
            y += 12f;

            GUI.color = LabelColor;
            Widgets.Label(new Rect(start.x, y, w, 22f), "Research point target");
            GUI.color = Color.white;
            y += 24f;

            DrawTargetField(new Rect(start.x, y, w, 30f), ref settings.targetPoints, ref targetBuffer, Floor(), ResearchTotalEngine.MaxBudget);
            y += 38f;

            DrawPresets(new Rect(start.x, y, w, 28f));
            y += 40f;

            GUI.color = LabelColor;
            Widgets.Label(new Rect(start.x, y, w, 22f), "Era weights  ·  later eras take more of the target because research gets faster");
            GUI.color = Color.white;
            y += 24f;

            DrawEraWeights(new Rect(start.x, y, w, 28f));
            y += 40f;

            float tableH = TableBlockHeight(eraSlices.Count);
            Rect eras = new Rect(start.x, y, w, tableH);
            Widgets.DrawMenuSection(eras);
            DrawEraTable(eras.ContractedBy(12f, 6f), eraSlices, "Era");
            y += tableH + 8f;

            float summaryH = ResearchTotalEngine.HasColony() ? 72f : 52f;
            Rect summary = new Rect(start.x, y, w, summaryH);
            Widgets.DrawMenuSection(summary);
            DrawStats(summary.ContractedBy(12f, 6f));
            y += summaryH;
            return y;
        }

        private float DrawAnomalySection(Rect start, List<EraSlice> anomalySlices)
        {
            float y = start.y;
            float w = start.width;
            y = DrawNote(start.x, y, w, AnomalyNote);

            Rect roundRow = new Rect(start.x, y, w, 28f);
            DrawRoundRow(roundRow, "Round knowledge to nearest", ref settings.roundAnomalyTo, ref anomalyRoundBuffer);
            y += 36f;

            GUI.color = LabelColor;
            Widgets.Label(new Rect(start.x, y, w, 22f), "Knowledge target");
            GUI.color = Color.white;
            y += 24f;

            DrawTargetField(new Rect(start.x, y, w, 30f), ref settings.targetAnomalyPoints, ref anomalyTargetBuffer, FloorAnomaly(), ResearchTotalEngine.MaxBudget);
            y += 38f;

            DrawAnomalyPresets(new Rect(start.x, y, w, 28f));
            y += 40f;

            GUI.color = LabelColor;
            Widgets.Label(new Rect(start.x, y, w, 22f), "Category weights  ·  Advanced takes more of the target by default");
            GUI.color = Color.white;
            y += 24f;

            DrawAnomalyWeights(new Rect(start.x, y, w, 28f));
            y += 40f;

            float tableH = TableBlockHeight(anomalySlices.Count);
            Rect table = new Rect(start.x, y, w, tableH);
            Widgets.DrawMenuSection(table);
            DrawEraTable(table.ContractedBy(12f, 6f), anomalySlices, "Category");
            y += tableH + 8f;

            float summaryH = ResearchTotalEngine.HasColony() ? 72f : 52f;
            Rect summary = new Rect(start.x, y, w, summaryH);
            Widgets.DrawMenuSection(summary);
            DrawAnomalyStats(summary.ContractedBy(12f, 6f));
            y += summaryH;
            return y;
        }

        private float DrawGravshipSection(Rect start, List<EraSlice> gravshipSlices)
        {
            float y = start.y;
            float w = start.width;
            y = DrawNote(start.x, y, w, GravshipNote);

            Rect roundRow = new Rect(start.x, y, w, 28f);
            DrawRoundRow(roundRow, "Round to nearest", ref settings.roundGravshipTo, ref gravRoundBuffer);
            y += 36f;

            Widgets.DrawLineHorizontal(start.x, y, w, LineColor);
            y += 12f;

            GUI.color = LabelColor;
            Widgets.Label(new Rect(start.x, y, w, 22f), "Research point target");
            GUI.color = Color.white;
            y += 24f;

            DrawTargetField(new Rect(start.x, y, w, 30f), ref settings.targetGravshipPoints, ref gravTargetBuffer, FloorGravship(), ResearchTotalEngine.MaxBudget);
            y += 38f;

            DrawGravshipPresets(new Rect(start.x, y, w, 28f));
            y += 40f;

            GUI.color = LabelColor;
            Widgets.Label(new Rect(start.x, y, w, 22f), "Era weights  ·  later eras take more of the target because research gets faster");
            GUI.color = Color.white;
            y += 24f;

            DrawGravshipWeights(new Rect(start.x, y, w, 28f));
            y += 40f;

            float tableH = TableBlockHeight(gravshipSlices.Count);
            Rect eras = new Rect(start.x, y, w, tableH);
            Widgets.DrawMenuSection(eras);
            DrawEraTable(eras.ContractedBy(12f, 6f), gravshipSlices, "Era");
            y += tableH + 8f;

            float summaryH = ResearchTotalEngine.HasColony() ? 72f : 52f;
            Rect summary = new Rect(start.x, y, w, summaryH);
            Widgets.DrawMenuSection(summary);
            DrawGravshipStats(summary.ContractedBy(12f, 6f));
            y += summaryH;
            return y;
        }

        private static float DrawNote(float x, float y, float w, string text)
        {
            Text.Font = GameFont.Tiny;
            Text.WordWrap = true;
            float h = Mathf.Ceil(Text.CalcHeight(text, w));
            GUI.color = LabelColor;
            Widgets.Label(new Rect(x, y, w, h), text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return y + h + 8f;
        }

        private static float NoteHeight(string text, float w)
        {
            Text.Font = GameFont.Tiny;
            Text.WordWrap = true;
            float h = Mathf.Ceil(Text.CalcHeight(text, w)) + 8f;
            Text.Font = GameFont.Small;
            return h;
        }

        private static void DrawRoundRow(Rect roundRow, string label, ref float value, ref string buffer)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = LabelColor;
            Widgets.Label(roundRow.LeftPart(0.32f), label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Rect roundField = new Rect(roundRow.x + roundRow.width * 0.32f, roundRow.y, 120f, roundRow.height);
            Widgets.TextFieldNumeric(roundField.ContractedBy(0f, 1f), ref value, ref buffer, 1f, 10000f);
        }

        private void DrawTargetField(Rect rect, ref float value, ref string buffer, float floor, float max)
        {
            if (string.IsNullOrEmpty(buffer) || buffer.IndexOf('E') >= 0 || buffer.IndexOf('e') >= 0)
            {
                buffer = FormatTarget(value);
            }

            string text = Widgets.TextField(rect, buffer);
            if (text == buffer)
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

            buffer = digits.ToString();
            if (buffer.Length == 0)
            {
                return;
            }

            if (float.TryParse(buffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out float parsed))
            {
                value = Mathf.Clamp(parsed, floor, max);
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

            DrawPreset(new Rect(rect.x + (bw + gap), rect.y, bw, rect.height), "1M", 1000000f);
            DrawPreset(new Rect(rect.x + (bw + gap) * 2f, rect.y, bw, rect.height), "2.5M", 2500000f);
            DrawPreset(new Rect(rect.x + (bw + gap) * 3f, rect.y, bw, rect.height), "5M", 5000000f);
            DrawPreset(new Rect(rect.x + (bw + gap) * 4f, rect.y, bw, rect.height), "10M", 10000000f);
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

        private void DrawAnomalyPresets(Rect rect)
        {
            float gap = 6f;
            float bw = (rect.width - gap * 4f) / 5f;
            Rect vanilla = new Rect(rect.x, rect.y, bw, rect.height);
            if (Widgets.ButtonText(vanilla, "Vanilla"))
            {
                settings.targetAnomalyPoints = Mathf.Max(1f, ResearchTotalEngine.VanillaAnomalyTotal());
                anomalyTargetBuffer = FormatTarget(settings.targetAnomalyPoints);
                if (ResearchTotalEngine.HasColony())
                {
                    ResearchTotalEngine.RecalculateRemaining();
                }
            }

            DrawAnomalyPreset(new Rect(rect.x + (bw + gap), rect.y, bw, rect.height), "2.5k", 2500f);
            DrawAnomalyPreset(new Rect(rect.x + (bw + gap) * 2f, rect.y, bw, rect.height), "5k", 5000f);
            DrawAnomalyPreset(new Rect(rect.x + (bw + gap) * 3f, rect.y, bw, rect.height), "10k", 10000f);
            DrawAnomalyPreset(new Rect(rect.x + (bw + gap) * 4f, rect.y, bw, rect.height), "25k", 25000f);
        }

        private void DrawAnomalyPreset(Rect rect, string label, float amount)
        {
            if (!Widgets.ButtonText(rect, label))
            {
                return;
            }

            settings.targetAnomalyPoints = Mathf.Clamp(amount, FloorAnomaly(), ResearchTotalEngine.MaxBudget);
            anomalyTargetBuffer = FormatTarget(settings.targetAnomalyPoints);
            if (ResearchTotalEngine.HasColony())
            {
                ResearchTotalEngine.RecalculateRemaining();
            }
        }

        private void DrawAnomalyWeights(Rect rect)
        {
            float gap = 6f;
            float w = (rect.width - gap) / 2f;
            DrawEraField(new Rect(rect.x, rect.y, w, rect.height), "Basic", ref settings.anomalyBasic, ref bufferAnomalyBasic);
            DrawEraField(new Rect(rect.x + w + gap, rect.y, w, rect.height), "Advanced", ref settings.anomalyAdvanced, ref bufferAnomalyAdvanced);
        }

        private void DrawGravshipPresets(Rect rect)
        {
            float gap = 6f;
            float bw = (rect.width - gap * 4f) / 5f;
            Rect vanilla = new Rect(rect.x, rect.y, bw, rect.height);
            if (Widgets.ButtonText(vanilla, "Vanilla"))
            {
                settings.targetGravshipPoints = Mathf.Max(1f, ResearchTotalEngine.VanillaGravshipTotal());
                gravTargetBuffer = FormatTarget(settings.targetGravshipPoints);
                if (ResearchTotalEngine.HasColony())
                {
                    ResearchTotalEngine.RecalculateRemaining();
                }
            }

            DrawGravshipPreset(new Rect(rect.x + (bw + gap), rect.y, bw, rect.height), "5k", 5000f);
            DrawGravshipPreset(new Rect(rect.x + (bw + gap) * 2f, rect.y, bw, rect.height), "10k", 10000f);
            DrawGravshipPreset(new Rect(rect.x + (bw + gap) * 3f, rect.y, bw, rect.height), "15k", 15000f);
            DrawGravshipPreset(new Rect(rect.x + (bw + gap) * 4f, rect.y, bw, rect.height), "20k", 20000f);
        }

        private void DrawGravshipPreset(Rect rect, string label, float amount)
        {
            if (!Widgets.ButtonText(rect, label))
            {
                return;
            }

            settings.targetGravshipPoints = Mathf.Clamp(amount, FloorGravship(), ResearchTotalEngine.MaxBudget);
            gravTargetBuffer = FormatTarget(settings.targetGravshipPoints);
            if (ResearchTotalEngine.HasColony())
            {
                ResearchTotalEngine.RecalculateRemaining();
            }
        }

        private void DrawGravshipWeights(Rect rect)
        {
            float gap = 6f;
            float w = (rect.width - gap * 4f) / 5f;
            DrawEraField(new Rect(rect.x, rect.y, w, rect.height), "Neo", ref settings.gravNeolithic, ref bufferGravNeo);
            DrawEraField(new Rect(rect.x + (w + gap), rect.y, w, rect.height), "Med", ref settings.gravMedieval, ref bufferGravMed);
            DrawEraField(new Rect(rect.x + (w + gap) * 2f, rect.y, w, rect.height), "Ind", ref settings.gravIndustrial, ref bufferGravInd);
            DrawEraField(new Rect(rect.x + (w + gap) * 3f, rect.y, w, rect.height), "Spa", ref settings.gravSpacer, ref bufferGravSpa);
            DrawEraField(new Rect(rect.x + (w + gap) * 4f, rect.y, w, rect.height), "Ultra", ref settings.gravUltra, ref bufferGravUlt);
        }

        private void DrawEraWeights(Rect rect)
        {
            bool tribals = ResearchTotalEngine.TribalsActive();
            int count = tribals ? 6 : 5;
            float gap = 6f;
            float w = (rect.width - gap * (count - 1)) / count;
            float x = rect.x;
            if (tribals)
            {
                DrawEraField(new Rect(x, rect.y, w, rect.height), "Ani", ref settings.eraAnimal, ref bufferAni);
                x += w + gap;
            }

            DrawEraField(new Rect(x, rect.y, w, rect.height), "Neo", ref settings.eraNeolithic, ref bufferNeo);
            x += w + gap;
            DrawEraField(new Rect(x, rect.y, w, rect.height), "Med", ref settings.eraMedieval, ref bufferMed);
            x += w + gap;
            DrawEraField(new Rect(x, rect.y, w, rect.height), "Ind", ref settings.eraIndustrial, ref bufferInd);
            x += w + gap;
            DrawEraField(new Rect(x, rect.y, w, rect.height), "Spa", ref settings.eraSpacer, ref bufferSpa);
            x += w + gap;
            DrawEraField(new Rect(x, rect.y, w, rect.height), "Ultra", ref settings.eraUltra, ref bufferUlt);
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

        private static void DrawAnomalyStats(Rect rect)
        {
            float vanilla = ResearchTotalEngine.VanillaAnomalyTotal();
            float target = ResearchTotalEngine.EffectiveAnomalyTarget();
            int count = ResearchTotalEngine.AnomalyCount();
            float ratio = vanilla > 0f ? target / vanilla : 0f;
            bool inColony = ResearchTotalEngine.HasColony();
            float row = rect.height / (inColony ? 3f : 2f);

            DrawStatRow(new Rect(rect.x, rect.y, rect.width, row), "Vanilla Anomaly", Pts(vanilla) + "   ·   " + count + " projects", false);
            DrawStatRow(new Rect(rect.x, rect.y + row, rect.width, row), "Scaled total", Pts(target) + "   ·   " + ratio.ToString("0.00") + "x", true);
            if (!inColony)
            {
                return;
            }

            float spent = ResearchTotalEngine.CurrentSpentAnomaly();
            DrawStatRow(new Rect(rect.x, rect.y + row * 2f, rect.width, row), "Completed and Total", Pts(spent) + " / " + Pts(target), false);
        }

        private static void DrawGravshipStats(Rect rect)
        {
            float vanilla = ResearchTotalEngine.VanillaGravshipTotal();
            float target = ResearchTotalEngine.EffectiveGravshipTarget();
            int count = ResearchTotalEngine.GravshipCount();
            float ratio = vanilla > 0f ? target / vanilla : 0f;
            bool inColony = ResearchTotalEngine.HasColony();
            float row = rect.height / (inColony ? 3f : 2f);

            DrawStatRow(new Rect(rect.x, rect.y, rect.width, row), "Vanilla Gravtech", Pts(vanilla) + "   ·   " + count + " projects", false);
            DrawStatRow(new Rect(rect.x, rect.y + row, rect.width, row), "Scaled total", Pts(target) + "   ·   " + ratio.ToString("0.00") + "x", true);
            if (!inColony)
            {
                return;
            }

            float spent = ResearchTotalEngine.CurrentSpentGravship();
            DrawStatRow(new Rect(rect.x, rect.y + row * 2f, rect.width, row), "Completed and Total", Pts(spent) + " / " + Pts(target), false);
        }

        private static void DrawEraTable(Rect rect, List<EraSlice> slices, string groupLabel)
        {
            float headerH = 22f;
            DrawEraHeader(new Rect(rect.x, rect.y, rect.width, headerH), groupLabel);
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

        private static void DrawEraHeader(Rect rect, string groupLabel)
        {
            GUI.color = HeaderColor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(Col(rect, 0f, 0.28f), groupLabel);
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
            settings.eraAnimal = ResearchTotalSettings.ClampEra(settings.eraAnimal, 1.00f);
            settings.eraNeolithic = ResearchTotalSettings.ClampEra(settings.eraNeolithic, 1.00f);
            settings.eraMedieval = ResearchTotalSettings.ClampEra(settings.eraMedieval, 1.15f);
            settings.eraIndustrial = ResearchTotalSettings.ClampEra(settings.eraIndustrial, 1.50f);
            settings.eraSpacer = ResearchTotalSettings.ClampEra(settings.eraSpacer, 2.00f);
            settings.eraUltra = ResearchTotalSettings.ClampEra(settings.eraUltra, 2.50f);
            settings.targetAnomalyPoints = Mathf.Clamp(settings.targetAnomalyPoints, FloorAnomaly(), ResearchTotalEngine.MaxBudget);
            settings.roundAnomalyTo = Mathf.Clamp(Mathf.Round(settings.roundAnomalyTo), 1f, 10000f);
            settings.anomalyBasic = ResearchTotalSettings.ClampEra(settings.anomalyBasic, 1.00f);
            settings.anomalyAdvanced = ResearchTotalSettings.ClampEra(settings.anomalyAdvanced, 1.50f);
            settings.targetGravshipPoints = Mathf.Clamp(settings.targetGravshipPoints, FloorGravship(), ResearchTotalEngine.MaxBudget);
            settings.roundGravshipTo = Mathf.Clamp(Mathf.Round(settings.roundGravshipTo), 1f, 10000f);
            settings.gravNeolithic = ResearchTotalSettings.ClampEra(settings.gravNeolithic, 1.00f);
            settings.gravMedieval = ResearchTotalSettings.ClampEra(settings.gravMedieval, 1.15f);
            settings.gravIndustrial = ResearchTotalSettings.ClampEra(settings.gravIndustrial, 1.50f);
            settings.gravSpacer = ResearchTotalSettings.ClampEra(settings.gravSpacer, 2.00f);
            settings.gravUltra = ResearchTotalSettings.ClampEra(settings.gravUltra, 2.50f);
        }

        private static float Floor()
        {
            return Mathf.Max(1f, ResearchTotalEngine.VanillaApparentTotal());
        }

        private static float FloorAnomaly()
        {
            return Mathf.Max(1f, ResearchTotalEngine.VanillaAnomalyTotal());
        }

        private static float FloorGravship()
        {
            return Mathf.Max(1f, ResearchTotalEngine.VanillaGravshipTotal());
        }

        private static float TableBlockHeight(int rows)
        {
            int n = Mathf.Max(1, rows);
            return 12f + 22f + (n + 1) * 24f + 12f;
        }

        private static float HeaderBlockHeight()
        {
            Text.Font = GameFont.Medium;
            float h = Text.LineHeight + 8f + 16f;
            Text.Font = GameFont.Small;
            return h;
        }

        private float MeasureContentHeight(float width, List<EraSlice> eraSlices, List<EraSlice> anomalySlices, List<EraSlice> gravshipSlices)
        {
            float h = HeaderBlockHeight();
            if (standardExpanded)
            {
                h += 36f + 12f + 24f + 38f + 40f + 24f + 40f;
                h += TableBlockHeight(eraSlices.Count) + 8f;
                h += ResearchTotalEngine.HasColony() ? 72f : 52f;
            }

            if (ResearchTotalEngine.AnomalyActive())
            {
                h += 8f + HeaderBlockHeight();
                if (anomalyExpanded)
                {
                    h += NoteHeight(AnomalyNote, width);
                    h += 36f + 24f + 38f + 40f + 24f + 40f;
                    h += TableBlockHeight(anomalySlices.Count) + 8f;
                    h += ResearchTotalEngine.HasColony() ? 72f : 52f;
                }
            }

            if (ResearchTotalEngine.GravshipActive())
            {
                h += 8f + HeaderBlockHeight();
                if (gravshipExpanded)
                {
                    h += NoteHeight(GravshipNote, width);
                    h += 36f + 12f + 24f + 38f + 40f + 24f + 40f;
                    h += TableBlockHeight(gravshipSlices.Count) + 8f;
                    h += ResearchTotalEngine.HasColony() ? 72f : 52f;
                }
            }

            h += 16f;
            return h;
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
