using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace ResearchTotal
{
    public class EraSlice
    {
        public string label;
        public int count;
        public float vanilla;
        public float scaled;

        public EraSlice(string label)
        {
            this.label = label;
        }
    }

    public enum ResearchPool
    {
        Standard,
        Anomaly,
        Gravship
    }

    [StaticConstructorOnStartup]
    public static class ResearchTotalEngine
    {
        public const float MaxBudget = 100000000f;
        public const string GravshipPackageId = "vanillaexpanded.gravship";
        public const string TribalsPackageId = "OskarPotocki.VFE.Tribals";

        private static readonly FieldInfo ProgressField = AccessTools.Field(typeof(ResearchManager), "progress");
        private static readonly Dictionary<ResearchProjectDef, float> originalCosts = new Dictionary<ResearchProjectDef, float>();
        private static readonly Dictionary<ResearchProjectDef, ResearchPool> poolByProject = new Dictionary<ResearchProjectDef, ResearchPool>();
        private static readonly List<ResearchProjectDef> includedProjects = new List<ResearchProjectDef>();
        private static readonly List<ResearchProjectDef> standardProjects = new List<ResearchProjectDef>();
        private static readonly List<ResearchProjectDef> anomalyProjects = new List<ResearchProjectDef>();
        private static readonly List<ResearchProjectDef> gravshipProjects = new List<ResearchProjectDef>();
        private static FieldInfo gravtechProjectField;
        private static bool gravtechFieldResolved;

        private static GameComponent_ResearchTotal current;
        private static bool snapping;
        private static HashSet<ResearchProjectDef> finishedSet = new HashSet<ResearchProjectDef>();

        static ResearchTotalEngine()
        {
            List<ResearchProjectDef> all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                if (proj == null)
                {
                    continue;
                }

                if (IsAnomalyProject(proj))
                {
                    if (proj.knowledgeCost > 0f)
                    {
                        originalCosts[proj] = proj.knowledgeCost;
                        RegisterIncluded(proj, ResearchPool.Anomaly);
                    }
                }
                else if (proj.baseCost > 0f)
                {
                    originalCosts[proj] = proj.baseCost;
                    RegisterIncluded(proj, IsGravshipProject(proj) ? ResearchPool.Gravship : ResearchPool.Standard);
                }
            }
        }

        private static void RegisterIncluded(ResearchProjectDef proj, ResearchPool pool)
        {
            poolByProject[proj] = pool;
            includedProjects.Add(proj);
            switch (pool)
            {
                case ResearchPool.Anomaly:
                    anomalyProjects.Add(proj);
                    break;
                case ResearchPool.Gravship:
                    gravshipProjects.Add(proj);
                    break;
                default:
                    standardProjects.Add(proj);
                    break;
            }
        }

        public static bool IsAnomalyProject(ResearchProjectDef proj)
        {
            if (proj == null || !ModsConfig.AnomalyActive || proj.knowledgeCategory == null)
            {
                return false;
            }

            return proj.knowledgeCategory == KnowledgeCategoryDefOf.Basic
                || proj.knowledgeCategory == KnowledgeCategoryDefOf.Advanced;
        }

        public static bool AnomalyActive()
        {
            return ModsConfig.AnomalyActive;
        }

        public static bool GravshipActive()
        {
            return ModsConfig.IsActive(GravshipPackageId);
        }

        public static bool TribalsActive()
        {
            return ModsConfig.IsActive(TribalsPackageId);
        }

        public static bool IsGravshipProject(ResearchProjectDef proj)
        {
            if (proj == null || !GravshipActive() || IsAnomalyProject(proj))
            {
                return false;
            }

            if (proj.tab != null && proj.tab.defName == "VGE_Gravtech")
            {
                return true;
            }

            if (proj.modExtensions == null)
            {
                return false;
            }

            for (int i = 0; i < proj.modExtensions.Count; i++)
            {
                DefModExtension ext = proj.modExtensions[i];
                if (ext != null && ext.GetType().Name == "GravtechResearchExtension")
                {
                    return true;
                }
            }

            return false;
        }

        public static ResearchPool PoolOf(ResearchProjectDef proj)
        {
            if (proj != null && poolByProject.TryGetValue(proj, out ResearchPool pool))
            {
                return pool;
            }

            if (IsAnomalyProject(proj))
            {
                return ResearchPool.Anomaly;
            }

            if (IsGravshipProject(proj))
            {
                return ResearchPool.Gravship;
            }

            return ResearchPool.Standard;
        }

        private static ResearchProjectDef CurrentGravtechProject()
        {
            if (!GravshipActive())
            {
                return null;
            }

            if (!gravtechFieldResolved)
            {
                gravtechFieldResolved = true;
                System.Type type = AccessTools.TypeByName("VanillaGravshipExpanded.World_ExposeData_Patch");
                if (type != null)
                {
                    gravtechProjectField = AccessTools.Field(type, "currentGravtechProject");
                }
            }

            if (gravtechProjectField == null)
            {
                return null;
            }

            return gravtechProjectField.GetValue(null) as ResearchProjectDef;
        }

        public static void Detach()
        {
            current = null;
            finishedSet.Clear();
        }

        public static bool IsIncluded(ResearchProjectDef proj)
        {
            return proj != null && originalCosts.ContainsKey(proj);
        }

        public static float OriginalCost(ResearchProjectDef proj)
        {
            if (proj != null && originalCosts.TryGetValue(proj, out float cost))
            {
                return cost;
            }

            if (proj == null)
            {
                return 0f;
            }

            return IsAnomalyProject(proj) ? proj.knowledgeCost : proj.baseCost;
        }

        public static float GetScaledCost(ResearchProjectDef proj)
        {
            if (proj == null || current == null || !current.ready)
            {
                return -1f;
            }

            if (!originalCosts.TryGetValue(proj, out float original))
            {
                return -1f;
            }

            if (current.assignedCosts.TryGetValue(proj, out float assigned) && assigned > 0f)
            {
                return assigned;
            }

            return original;
        }

        public static void InitializeForGame(GameComponent_ResearchTotal comp)
        {
            current = comp;
            if (comp.assignedCosts == null)
            {
                comp.assignedCosts = new Dictionary<ResearchProjectDef, float>();
            }

            if (comp.finishedProjects == null)
            {
                comp.finishedProjects = new List<ResearchProjectDef>();
            }

            RebuildFinishedSet();
            DiscoverFinishedFromProgress();

            if (comp.lastTechLevel == TechLevel.Undefined)
            {
                comp.lastTechLevel = PlayerTechLevel();
            }

            if (!HasAssignedForPool(ResearchPool.Standard))
            {
                Allocate(CollectStandard(), EffectiveTarget(), null);
            }

            if (AnomalyActive() && !HasAssignedForPool(ResearchPool.Anomaly))
            {
                Allocate(CollectAnomaly(), EffectiveAnomalyTarget(), null);
            }

            if (GravshipActive() && !HasAssignedForPool(ResearchPool.Gravship))
            {
                Allocate(CollectGravship(), EffectiveGravshipTarget(), null);
            }

            RecomputeSpent();
            comp.ready = true;
            RecalculateRemaining();
            SnapFinishedProgress();
        }

        public static void RecalculateRemaining()
        {
            if (current == null || !current.ready)
            {
                return;
            }

            RecalculatePool(ResearchPool.Standard);
            if (AnomalyActive())
            {
                RecalculatePool(ResearchPool.Anomaly);
            }

            if (GravshipActive())
            {
                RecalculatePool(ResearchPool.Gravship);
            }

            SnapFinishedProgress();
        }

        public static void CheckTechLevel()
        {
            if (current == null || !current.ready)
            {
                return;
            }

            TechLevel now = PlayerTechLevel();
            if (now == current.lastTechLevel)
            {
                return;
            }

            current.lastTechLevel = now;
            RecalculatePool(ResearchPool.Standard);
            if (GravshipActive())
            {
                RecalculatePool(ResearchPool.Gravship);
            }

            SnapFinishedProgress();
        }

        public static void NotifyProjectFinished(ResearchProjectDef proj)
        {
            if (current == null || !IsIncluded(proj) || finishedSet.Contains(proj))
            {
                return;
            }

            finishedSet.Add(proj);
            if (!current.finishedProjects.Contains(proj))
            {
                current.finishedProjects.Add(proj);
            }

            if (IsAnomalyProject(proj))
            {
                current.spentAnomaly += ApparentOf(proj);
            }
            else if (IsGravshipProject(proj))
            {
                current.spentGravship += ApparentOf(proj);
            }
            else
            {
                current.spentApparent += ApparentOf(proj);
            }
        }

        public static void NotifyAllProjectsFinished()
        {
            if (current == null)
            {
                return;
            }

            List<ResearchProjectDef> all = CollectIncluded();
            current.spentApparent = 0f;
            current.spentAnomaly = 0f;
            current.spentGravship = 0f;
            finishedSet.Clear();
            current.finishedProjects.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                finishedSet.Add(proj);
                current.finishedProjects.Add(proj);
                if (IsAnomalyProject(proj))
                {
                    current.spentAnomaly += ApparentOf(proj);
                }
                else if (IsGravshipProject(proj))
                {
                    current.spentGravship += ApparentOf(proj);
                }
                else
                {
                    current.spentApparent += ApparentOf(proj);
                }
            }
        }

        public static void NotifyReset()
        {
            if (current == null)
            {
                return;
            }

            finishedSet.Clear();
            current.finishedProjects.Clear();
            current.spentApparent = 0f;
            current.spentAnomaly = 0f;
            current.spentGravship = 0f;
            current.assignedCosts.Clear();
            Allocate(CollectStandard(), EffectiveTarget(), null);
            if (AnomalyActive())
            {
                Allocate(CollectAnomaly(), EffectiveAnomalyTarget(), null);
            }

            if (GravshipActive())
            {
                Allocate(CollectGravship(), EffectiveGravshipTarget(), null);
            }

            SnapFinishedProgress();
        }

        public static void SnapFinishedProgress()
        {
            if (current == null || !current.ready || snapping || Find.ResearchManager == null)
            {
                return;
            }

            Dictionary<ResearchProjectDef, float> progress = GetProgressDict();
            if (progress == null)
            {
                return;
            }

            snapping = true;
            try
            {
                foreach (ResearchProjectDef proj in finishedSet)
                {
                    if (!IsIncluded(proj))
                    {
                        continue;
                    }

                    progress[proj] = GetAssignedOrOriginal(proj);
                }
            }
            finally
            {
                snapping = false;
            }
        }

        public static float VanillaApparentTotal()
        {
            TechLevel tech = PlayerTechLevel();
            float total = 0f;
            for (int i = 0; i < standardProjects.Count; i++)
            {
                ResearchProjectDef proj = standardProjects[i];
                total += OriginalCost(proj) * proj.CostFactor(tech);
            }

            return total;
        }

        public static int IncludedCount()
        {
            return standardProjects.Count;
        }

        public static float CurrentTarget()
        {
            if (ResearchTotalMod.settings == null)
            {
                return 1000000f;
            }

            return Mathf.Clamp(ResearchTotalMod.settings.targetPoints, 1f, MaxBudget);
        }

        public static float EffectiveTarget()
        {
            return Mathf.Clamp(CurrentTarget(), Mathf.Max(1f, VanillaApparentTotal()), MaxBudget);
        }

        public static float CurrentAnomalyTarget()
        {
            if (ResearchTotalMod.settings == null)
            {
                return 5000f;
            }

            return Mathf.Clamp(ResearchTotalMod.settings.targetAnomalyPoints, 1f, MaxBudget);
        }

        public static float EffectiveAnomalyTarget()
        {
            return Mathf.Clamp(CurrentAnomalyTarget(), Mathf.Max(1f, VanillaAnomalyTotal()), MaxBudget);
        }

        public static float CurrentGravshipTarget()
        {
            if (ResearchTotalMod.settings == null)
            {
                return 5000f;
            }

            return Mathf.Clamp(ResearchTotalMod.settings.targetGravshipPoints, 1f, MaxBudget);
        }

        public static float EffectiveGravshipTarget()
        {
            return Mathf.Clamp(CurrentGravshipTarget(), Mathf.Max(1f, VanillaGravshipTotal()), MaxBudget);
        }

        public static float VanillaGravshipTotal()
        {
            if (!GravshipActive())
            {
                return 0f;
            }

            TechLevel tech = PlayerTechLevel();
            float total = 0f;
            for (int i = 0; i < gravshipProjects.Count; i++)
            {
                ResearchProjectDef proj = gravshipProjects[i];
                total += OriginalCost(proj) * proj.CostFactor(tech);
            }

            return total;
        }

        public static int GravshipCount()
        {
            return GravshipActive() ? gravshipProjects.Count : 0;
        }

        public static float CurrentSpentGravship()
        {
            return current != null ? current.spentGravship : 0f;
        }

        public static float VanillaAnomalyTotal()
        {
            if (!AnomalyActive())
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < anomalyProjects.Count; i++)
            {
                total += OriginalCost(anomalyProjects[i]);
            }

            return total;
        }

        public static int AnomalyCount()
        {
            return AnomalyActive() ? anomalyProjects.Count : 0;
        }

        public static float CurrentSpentAnomaly()
        {
            return current != null ? current.spentAnomaly : 0f;
        }

        public static float EraWeight(ResearchProjectDef proj)
        {
            ResearchTotalSettings s = ResearchTotalMod.settings;
            if (proj == null || s == null)
            {
                return 1f;
            }

            switch (proj.techLevel)
            {
                case TechLevel.Animal:
                    if (TribalsActive() && !IsGravshipProject(proj))
                    {
                        return s.eraAnimal;
                    }

                    return IsGravshipProject(proj) ? s.gravNeolithic : s.eraNeolithic;
                case TechLevel.Neolithic:
                    return IsGravshipProject(proj) ? s.gravNeolithic : s.eraNeolithic;
                case TechLevel.Medieval:
                    return IsGravshipProject(proj) ? s.gravMedieval : s.eraMedieval;
                case TechLevel.Industrial:
                    return IsGravshipProject(proj) ? s.gravIndustrial : s.eraIndustrial;
                case TechLevel.Spacer:
                    return IsGravshipProject(proj) ? s.gravSpacer : s.eraSpacer;
                case TechLevel.Ultra:
                case TechLevel.Archotech:
                    return IsGravshipProject(proj) ? s.gravUltra : s.eraUltra;
                default:
                    return 1f;
            }
        }

        public static float CategoryWeight(ResearchProjectDef proj)
        {
            ResearchTotalSettings s = ResearchTotalMod.settings;
            if (proj == null || s == null || !AnomalyActive())
            {
                return 1f;
            }

            if (proj.knowledgeCategory == KnowledgeCategoryDefOf.Advanced)
            {
                return s.anomalyAdvanced;
            }

            return s.anomalyBasic;
        }

        public static List<EraSlice> BuildAnomalySlices()
        {
            EraSlice[] buckets =
            {
                new EraSlice("Basic"),
                new EraSlice("Advanced")
            };

            if (!AnomalyActive())
            {
                return new List<EraSlice>();
            }

            float weightSum = 0f;
            for (int i = 0; i < anomalyProjects.Count; i++)
            {
                weightSum += AllocationWeight(anomalyProjects[i], TechLevel.Undefined);
            }

            float target = EffectiveAnomalyTarget();
            float step = AnomalyRoundStep();
            for (int i = 0; i < anomalyProjects.Count; i++)
            {
                ResearchProjectDef proj = anomalyProjects[i];
                EraSlice slice = buckets[AnomalyIndex(proj)];
                slice.count++;
                slice.vanilla += OriginalCost(proj);
                if (weightSum > 0f)
                {
                    float share = target * AllocationWeight(proj, TechLevel.Undefined) / weightSum;
                    float cost = ApplyRounding(share, step);
                    cost = Mathf.Max(OriginalCost(proj), cost);
                    slice.scaled += cost;
                }
            }

            List<EraSlice> list = new List<EraSlice>(2);
            for (int i = 0; i < buckets.Length; i++)
            {
                if (buckets[i].count > 0)
                {
                    list.Add(buckets[i]);
                }
            }

            return list;
        }

        public static List<EraSlice> BuildEraSlices()
        {
            bool tribals = TribalsActive();
            EraSlice[] buckets = tribals
                ? new[]
                {
                    new EraSlice("Animal"),
                    new EraSlice("Neolithic"),
                    new EraSlice("Medieval"),
                    new EraSlice("Industrial"),
                    new EraSlice("Spacer"),
                    new EraSlice("Ultra")
                }
                : new[]
                {
                    new EraSlice("Neolithic"),
                    new EraSlice("Medieval"),
                    new EraSlice("Industrial"),
                    new EraSlice("Spacer"),
                    new EraSlice("Ultra")
                };

            TechLevel tech = PlayerTechLevel();
            float weightSum = 0f;
            for (int i = 0; i < standardProjects.Count; i++)
            {
                weightSum += AllocationWeight(standardProjects[i], tech);
            }

            float target = EffectiveTarget();
            for (int i = 0; i < standardProjects.Count; i++)
            {
                ResearchProjectDef proj = standardProjects[i];
                EraSlice slice = buckets[StandardEraIndex(proj.techLevel)];
                slice.count++;
                float factor = proj.CostFactor(tech);
                slice.vanilla += OriginalCost(proj) * factor;
                if (weightSum > 0f)
                {
                    float share = target * AllocationWeight(proj, tech) / weightSum;
                    float cost = factor > 0f ? share / factor : share;
                    cost = ApplyRounding(cost);
                    cost = Mathf.Max(OriginalCost(proj), cost);
                    slice.scaled += cost * factor;
                }
            }

            List<EraSlice> list = new List<EraSlice>(buckets.Length);
            for (int i = 0; i < buckets.Length; i++)
            {
                if (buckets[i].count > 0)
                {
                    list.Add(buckets[i]);
                }
            }

            return list;
        }

        public static List<EraSlice> BuildGravshipSlices()
        {
            EraSlice[] buckets =
            {
                new EraSlice("Neolithic"),
                new EraSlice("Medieval"),
                new EraSlice("Industrial"),
                new EraSlice("Spacer"),
                new EraSlice("Ultra")
            };

            if (!GravshipActive())
            {
                return new List<EraSlice>();
            }

            TechLevel tech = PlayerTechLevel();
            float weightSum = 0f;
            for (int i = 0; i < gravshipProjects.Count; i++)
            {
                weightSum += AllocationWeight(gravshipProjects[i], tech);
            }

            float target = EffectiveGravshipTarget();
            float step = GravshipRoundStep();
            for (int i = 0; i < gravshipProjects.Count; i++)
            {
                ResearchProjectDef proj = gravshipProjects[i];
                EraSlice slice = buckets[EraIndex(proj.techLevel)];
                slice.count++;
                float factor = proj.CostFactor(tech);
                slice.vanilla += OriginalCost(proj) * factor;
                if (weightSum > 0f)
                {
                    float share = target * AllocationWeight(proj, tech) / weightSum;
                    float cost = factor > 0f ? share / factor : share;
                    cost = ApplyRounding(cost, step);
                    cost = Mathf.Max(OriginalCost(proj), cost);
                    slice.scaled += cost * factor;
                }
            }

            List<EraSlice> list = new List<EraSlice>(5);
            for (int i = 0; i < buckets.Length; i++)
            {
                if (buckets[i].count > 0)
                {
                    list.Add(buckets[i]);
                }
            }

            return list;
        }

        public static float CurrentSpent()
        {
            return current != null ? current.spentApparent : 0f;
        }

        public static bool HasColony()
        {
            return current != null && current.ready;
        }

        public static GameComponent_ResearchTotal Comp => current;

        private static float AllocationWeight(ResearchProjectDef proj, TechLevel colonyTech)
        {
            if (IsAnomalyProject(proj))
            {
                return OriginalCost(proj) * CategoryWeight(proj);
            }

            return OriginalCost(proj) * proj.CostFactor(colonyTech) * EraWeight(proj);
        }

        private static int StandardEraIndex(TechLevel level)
        {
            if (TribalsActive())
            {
                switch (level)
                {
                    case TechLevel.Animal:
                        return 0;
                    case TechLevel.Neolithic:
                        return 1;
                    case TechLevel.Medieval:
                        return 2;
                    case TechLevel.Industrial:
                        return 3;
                    case TechLevel.Spacer:
                        return 4;
                    case TechLevel.Ultra:
                    case TechLevel.Archotech:
                        return 5;
                    default:
                        return 1;
                }
            }

            return EraIndex(level);
        }

        private static int EraIndex(TechLevel level)
        {
            switch (level)
            {
                case TechLevel.Medieval:
                    return 1;
                case TechLevel.Industrial:
                    return 2;
                case TechLevel.Spacer:
                    return 3;
                case TechLevel.Ultra:
                case TechLevel.Archotech:
                    return 4;
                default:
                    return 0;
            }
        }

        private static int AnomalyIndex(ResearchProjectDef proj)
        {
            if (AnomalyActive() && proj != null && proj.knowledgeCategory == KnowledgeCategoryDefOf.Advanced)
            {
                return 1;
            }

            return 0;
        }

        private static float RoundStep()
        {
            if (ResearchTotalMod.settings == null || ResearchTotalMod.settings.roundTo < 1f)
            {
                return 10f;
            }

            return Mathf.Clamp(Mathf.Round(ResearchTotalMod.settings.roundTo), 1f, 10000f);
        }

        private static float AnomalyRoundStep()
        {
            if (ResearchTotalMod.settings == null || ResearchTotalMod.settings.roundAnomalyTo < 1f)
            {
                return 1f;
            }

            return Mathf.Clamp(Mathf.Round(ResearchTotalMod.settings.roundAnomalyTo), 1f, 10000f);
        }

        private static float GravshipRoundStep()
        {
            if (ResearchTotalMod.settings == null || ResearchTotalMod.settings.roundGravshipTo < 1f)
            {
                return 10f;
            }

            return Mathf.Clamp(Mathf.Round(ResearchTotalMod.settings.roundGravshipTo), 1f, 10000f);
        }

        private static float FactorOf(ResearchProjectDef proj, TechLevel tech)
        {
            if (IsAnomalyProject(proj))
            {
                return 1f;
            }

            return proj.CostFactor(tech);
        }

        private static float StepOf(ResearchProjectDef proj)
        {
            if (IsAnomalyProject(proj))
            {
                return AnomalyRoundStep();
            }

            if (IsGravshipProject(proj))
            {
                return GravshipRoundStep();
            }

            return RoundStep();
        }

        private static void RecalculatePool(ResearchPool pool)
        {
            List<ResearchProjectDef> frozen = new List<ResearchProjectDef>();
            CollectFrozen(frozen, pool);

            List<ResearchProjectDef> remaining = new List<ResearchProjectDef>();
            CollectRemaining(remaining, frozen, pool);

            float spent = SpentOf(pool);
            float target = TargetOf(pool);
            float remainingBudget = Mathf.Max(0f, target - spent);
            for (int i = 0; i < frozen.Count; i++)
            {
                remainingBudget = Mathf.Max(0f, remainingBudget - ApparentOf(frozen[i]));
            }

            Allocate(remaining, remainingBudget, frozen);
        }

        private static float SpentOf(ResearchPool pool)
        {
            if (current == null)
            {
                return 0f;
            }

            if (pool == ResearchPool.Anomaly)
            {
                return current.spentAnomaly;
            }

            if (pool == ResearchPool.Gravship)
            {
                return current.spentGravship;
            }

            return current.spentApparent;
        }

        private static float TargetOf(ResearchPool pool)
        {
            if (pool == ResearchPool.Anomaly)
            {
                return EffectiveAnomalyTarget();
            }

            if (pool == ResearchPool.Gravship)
            {
                return EffectiveGravshipTarget();
            }

            return EffectiveTarget();
        }

        private static bool HasAssignedForPool(ResearchPool pool)
        {
            if (current == null || current.assignedCosts == null)
            {
                return false;
            }

            foreach (KeyValuePair<ResearchProjectDef, float> kv in current.assignedCosts)
            {
                if (kv.Key != null && PoolOf(kv.Key) == pool)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Allocate(List<ResearchProjectDef> nodes, float budgetApparent, List<ResearchProjectDef> frozen)
        {
            if (current == null)
            {
                return;
            }

            if (frozen != null)
            {
                for (int i = 0; i < frozen.Count; i++)
                {
                    ResearchProjectDef frozenProj = frozen[i];
                    if (!current.assignedCosts.ContainsKey(frozenProj))
                    {
                        current.assignedCosts[frozenProj] = OriginalCost(frozenProj);
                    }
                }
            }

            if (nodes.Count == 0)
            {
                return;
            }

            TechLevel tech = PlayerTechLevel();
            float weightSum = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                weightSum += AllocationWeight(nodes[i], tech);
            }

            if (weightSum <= 0f)
            {
                return;
            }

            Dictionary<ResearchProjectDef, float> progress = GetProgressDict();
            for (int i = 0; i < nodes.Count; i++)
            {
                ResearchProjectDef proj = nodes[i];
                float factor = FactorOf(proj, tech);
                float share = budgetApparent * AllocationWeight(proj, tech) / weightSum;
                float cost = factor > 0f ? share / factor : share;
                cost = ApplyRounding(cost, StepOf(proj));
                cost = Mathf.Max(OriginalCost(proj), cost);
                float progressNow = ProgressOf(proj, progress);
                if (progressNow > cost)
                {
                    cost = progressNow;
                }

                current.assignedCosts[proj] = cost;
            }

            RemainderPass(nodes, budgetApparent, tech, progress);
        }

        private static void RemainderPass(List<ResearchProjectDef> nodes, float budgetApparent, TechLevel tech, Dictionary<ResearchProjectDef, float> progress)
        {
            nodes.Sort(delegate (ResearchProjectDef a, ResearchProjectDef b)
            {
                return AllocationWeight(b, tech).CompareTo(AllocationWeight(a, tech));
            });

            for (int pass = 0; pass < 8; pass++)
            {
                float sum = 0f;
                for (int i = 0; i < nodes.Count; i++)
                {
                    sum += GetAssignedOrOriginal(nodes[i]) * FactorOf(nodes[i], tech);
                }

                float diff = budgetApparent - sum;
                if (Mathf.Abs(diff) < 1f)
                {
                    return;
                }

                bool changed = false;
                for (int i = 0; i < nodes.Count && Mathf.Abs(diff) >= 1f; i++)
                {
                    ResearchProjectDef proj = nodes[i];
                    float factor = FactorOf(proj, tech);
                    if (factor <= 0f)
                    {
                        continue;
                    }

                    float step = StepOf(proj);
                    float deltaCost = ApplyRounding(diff / factor, step);
                    if (Mathf.Abs(deltaCost) < step)
                    {
                        deltaCost = diff > 0f ? step : -step;
                    }

                    float oldCost = GetAssignedOrOriginal(proj);
                    float newCost = Mathf.Max(OriginalCost(proj), oldCost + deltaCost);
                    float progressNow = ProgressOf(proj, progress);
                    if (progressNow > newCost)
                    {
                        newCost = progressNow;
                    }

                    if (Mathf.Abs(newCost - oldCost) < 0.5f)
                    {
                        continue;
                    }

                    current.assignedCosts[proj] = newCost;
                    diff -= (newCost - oldCost) * factor;
                    changed = true;
                }

                if (!changed)
                {
                    return;
                }
            }
        }

        private static void DiscoverFinishedFromProgress()
        {
            ResearchManager manager = Find.ResearchManager;
            if (manager == null)
            {
                return;
            }

            List<ResearchProjectDef> all = CollectIncluded();
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                if (IsFinishedTracked(proj))
                {
                    continue;
                }

                float progress = manager.GetProgress(proj);
                if (progress >= OriginalCost(proj) - 0.01f || progress >= GetAssignedOrOriginal(proj) - 0.01f)
                {
                    finishedSet.Add(proj);
                    if (!current.finishedProjects.Contains(proj))
                    {
                        current.finishedProjects.Add(proj);
                    }
                }
            }
        }

        private static void RecomputeSpent()
        {
            current.spentApparent = 0f;
            current.spentAnomaly = 0f;
            current.spentGravship = 0f;
            foreach (ResearchProjectDef proj in finishedSet)
            {
                if (!IsIncluded(proj))
                {
                    continue;
                }

                if (IsAnomalyProject(proj))
                {
                    current.spentAnomaly += ApparentOf(proj);
                }
                else if (IsGravshipProject(proj))
                {
                    current.spentGravship += ApparentOf(proj);
                }
                else
                {
                    current.spentApparent += ApparentOf(proj);
                }
            }
        }

        private static void RebuildFinishedSet()
        {
            finishedSet.Clear();
            for (int i = 0; i < current.finishedProjects.Count; i++)
            {
                ResearchProjectDef proj = current.finishedProjects[i];
                if (proj != null)
                {
                    finishedSet.Add(proj);
                }
            }
        }

        private static void CollectRemaining(List<ResearchProjectDef> remaining, List<ResearchProjectDef> frozen, ResearchPool pool)
        {
            List<ResearchProjectDef> all = CollectPool(pool);
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                if (IsFinishedTracked(proj) || (frozen != null && frozen.Contains(proj)))
                {
                    continue;
                }

                remaining.Add(proj);
            }
        }

        private static List<ResearchProjectDef> CollectPool(ResearchPool pool)
        {
            if (pool == ResearchPool.Anomaly)
            {
                return CollectAnomaly();
            }

            if (pool == ResearchPool.Gravship)
            {
                return CollectGravship();
            }

            return CollectStandard();
        }

        private static List<ResearchProjectDef> CollectIncluded()
        {
            return new List<ResearchProjectDef>(includedProjects);
        }

        private static List<ResearchProjectDef> CollectStandard()
        {
            return new List<ResearchProjectDef>(standardProjects);
        }

        private static List<ResearchProjectDef> CollectAnomaly()
        {
            return AnomalyActive() ? new List<ResearchProjectDef>(anomalyProjects) : new List<ResearchProjectDef>();
        }

        private static List<ResearchProjectDef> CollectGravship()
        {
            return GravshipActive() ? new List<ResearchProjectDef>(gravshipProjects) : new List<ResearchProjectDef>();
        }

        private static void CollectFrozen(List<ResearchProjectDef> frozen, ResearchPool pool)
        {
            if (Find.ResearchManager == null)
            {
                return;
            }

            TryAddFrozen(frozen, Find.ResearchManager.GetProject(null), pool);
            if (AnomalyActive())
            {
                TryAddFrozen(frozen, Find.ResearchManager.GetProject(KnowledgeCategoryDefOf.Basic), pool);
                TryAddFrozen(frozen, Find.ResearchManager.GetProject(KnowledgeCategoryDefOf.Advanced), pool);
            }

            TryAddFrozen(frozen, CurrentGravtechProject(), pool);
        }

        private static void TryAddFrozen(List<ResearchProjectDef> frozen, ResearchProjectDef proj, ResearchPool pool)
        {
            if (proj == null || !IsIncluded(proj) || IsFinishedTracked(proj) || PoolOf(proj) != pool)
            {
                return;
            }

            if (Find.ResearchManager.GetProgress(proj) <= 0f)
            {
                return;
            }

            if (!frozen.Contains(proj))
            {
                frozen.Add(proj);
            }
        }

        private static bool IsFinishedTracked(ResearchProjectDef proj)
        {
            return proj != null && finishedSet.Contains(proj);
        }

        private static float ApparentOf(ResearchProjectDef proj)
        {
            return GetAssignedOrOriginal(proj) * FactorOf(proj, PlayerTechLevel());
        }

        private static float GetAssignedOrOriginal(ResearchProjectDef proj)
        {
            if (current != null && current.assignedCosts.TryGetValue(proj, out float assigned) && assigned > 0f)
            {
                return assigned;
            }

            return OriginalCost(proj);
        }

        private static float ApplyRounding(float value)
        {
            return ApplyRounding(value, RoundStep());
        }

        private static float ApplyRounding(float value, float step)
        {
            if (value < 0f)
            {
                value = 0f;
            }

            if (step < 1f)
            {
                step = 1f;
            }

            return Mathf.Round(value / step) * step;
        }

        private static TechLevel PlayerTechLevel()
        {
            if (Faction.OfPlayer != null && Faction.OfPlayer.def != null)
            {
                return Faction.OfPlayer.def.techLevel;
            }

            return TechLevel.Industrial;
        }

        private static Dictionary<ResearchProjectDef, float> GetProgressDict()
        {
            if (Find.ResearchManager == null || ProgressField == null)
            {
                return null;
            }

            return ProgressField.GetValue(Find.ResearchManager) as Dictionary<ResearchProjectDef, float>;
        }

        private static float ProgressOf(ResearchProjectDef proj, Dictionary<ResearchProjectDef, float> progress)
        {
            if (progress != null && progress.TryGetValue(proj, out float value))
            {
                return value;
            }

            if (Find.ResearchManager != null)
            {
                return Find.ResearchManager.GetProgress(proj);
            }

            return 0f;
        }
    }
}
