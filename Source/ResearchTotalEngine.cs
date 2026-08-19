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

    [StaticConstructorOnStartup]
    public static class ResearchTotalEngine
    {
        public const float MaxBudget = 100000000f;

        private static readonly FieldInfo ProgressField = AccessTools.Field(typeof(ResearchManager), "progress");
        private static readonly Dictionary<ResearchProjectDef, float> originalCosts = new Dictionary<ResearchProjectDef, float>();

        private static GameComponent_ResearchTotal current;
        private static bool snapping;
        private static HashSet<ResearchProjectDef> finishedSet = new HashSet<ResearchProjectDef>();

        static ResearchTotalEngine()
        {
            List<ResearchProjectDef> all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                if (proj != null && proj.baseCost > 0f)
                {
                    originalCosts[proj] = proj.baseCost;
                }
            }
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

            return proj != null ? proj.baseCost : 0f;
        }

        public static float GetScaledCost(ResearchProjectDef proj)
        {
            if (proj == null || !IsIncluded(proj) || current == null || !current.ready)
            {
                return -1f;
            }

            if (current.assignedCosts.TryGetValue(proj, out float assigned) && assigned > 0f)
            {
                return assigned;
            }

            return OriginalCost(proj);
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

            if (comp.assignedCosts.Count > 0)
            {
                comp.ready = true;
                if (comp.lastTechLevel == TechLevel.Undefined)
                {
                    comp.lastTechLevel = PlayerTechLevel();
                }

                SnapFinishedProgress();
                return;
            }

            DiscoverFinishedFromProgress();
            comp.spentApparent = 0f;
            Allocate(CollectIncluded(), EffectiveTarget(), null);
            CreditExistingFinished();
            comp.lastTechLevel = PlayerTechLevel();
            comp.ready = true;
            SnapFinishedProgress();
        }

        public static void RecalculateRemaining()
        {
            if (current == null || !current.ready)
            {
                return;
            }

            List<ResearchProjectDef> remaining = new List<ResearchProjectDef>();
            ResearchProjectDef frozen = FrozenProject();
            CollectRemaining(remaining, frozen);

            float remainingBudget = Mathf.Max(0f, EffectiveTarget() - current.spentApparent);
            if (frozen != null)
            {
                remainingBudget = Mathf.Max(0f, remainingBudget - ApparentOf(frozen));
            }

            Allocate(remaining, remainingBudget, frozen);
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
            RecalculateRemaining();
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

            current.spentApparent += ApparentOf(proj);
        }

        public static void NotifyAllProjectsFinished()
        {
            if (current == null)
            {
                return;
            }

            List<ResearchProjectDef> all = CollectIncluded();
            current.spentApparent = 0f;
            finishedSet.Clear();
            current.finishedProjects.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                finishedSet.Add(proj);
                current.finishedProjects.Add(proj);
                current.spentApparent += ApparentOf(proj);
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
            current.assignedCosts.Clear();
            Allocate(CollectIncluded(), EffectiveTarget(), null);
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
            List<ResearchProjectDef> all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                if (IsIncluded(proj))
                {
                    total += OriginalCost(proj) * proj.CostFactor(tech);
                }
            }

            return total;
        }

        public static int IncludedCount()
        {
            int count = 0;
            List<ResearchProjectDef> all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                if (IsIncluded(all[i]))
                {
                    count++;
                }
            }

            return count;
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
                case TechLevel.Neolithic:
                    return s.eraNeolithic;
                case TechLevel.Medieval:
                    return s.eraMedieval;
                case TechLevel.Industrial:
                    return s.eraIndustrial;
                case TechLevel.Spacer:
                    return s.eraSpacer;
                case TechLevel.Ultra:
                case TechLevel.Archotech:
                    return s.eraUltra;
                default:
                    return 1f;
            }
        }

        public static List<EraSlice> BuildEraSlices()
        {
            EraSlice[] buckets =
            {
                new EraSlice("Neolithic"),
                new EraSlice("Medieval"),
                new EraSlice("Industrial"),
                new EraSlice("Spacer"),
                new EraSlice("Ultra")
            };

            TechLevel tech = PlayerTechLevel();
            float weightSum = 0f;
            List<ResearchProjectDef> all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            List<ResearchProjectDef> included = new List<ResearchProjectDef>();
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                if (!IsIncluded(proj))
                {
                    continue;
                }

                included.Add(proj);
                weightSum += AllocationWeight(proj, tech);
            }

            float target = EffectiveTarget();
            for (int i = 0; i < included.Count; i++)
            {
                ResearchProjectDef proj = included[i];
                EraSlice slice = buckets[EraIndex(proj.techLevel)];
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
            return OriginalCost(proj) * proj.CostFactor(colonyTech) * EraWeight(proj);
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

        private static float RoundStep()
        {
            if (ResearchTotalMod.settings == null || ResearchTotalMod.settings.roundTo < 1f)
            {
                return 10f;
            }

            return Mathf.Clamp(Mathf.Round(ResearchTotalMod.settings.roundTo), 1f, 10000f);
        }

        private static void Allocate(List<ResearchProjectDef> nodes, float budgetApparent, ResearchProjectDef frozen)
        {
            if (current == null)
            {
                return;
            }

            if (frozen != null && !current.assignedCosts.ContainsKey(frozen))
            {
                current.assignedCosts[frozen] = OriginalCost(frozen);
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
                float factor = proj.CostFactor(tech);
                float share = budgetApparent * AllocationWeight(proj, tech) / weightSum;
                float cost = factor > 0f ? share / factor : share;
                cost = ApplyRounding(cost);
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
                    sum += GetAssignedOrOriginal(nodes[i]) * nodes[i].CostFactor(tech);
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
                    float factor = proj.CostFactor(tech);
                    if (factor <= 0f)
                    {
                        continue;
                    }

                    float step = RoundStep();
                    float deltaCost = ApplyRounding(diff / factor);
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
            finishedSet.Clear();
            current.finishedProjects.Clear();
            ResearchManager manager = Find.ResearchManager;
            if (manager == null)
            {
                return;
            }

            List<ResearchProjectDef> all = CollectIncluded();
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                if (manager.GetProgress(proj) >= OriginalCost(proj) - 0.01f)
                {
                    finishedSet.Add(proj);
                    current.finishedProjects.Add(proj);
                }
            }
        }

        private static void CreditExistingFinished()
        {
            current.spentApparent = 0f;
            foreach (ResearchProjectDef proj in finishedSet)
            {
                current.spentApparent += ApparentOf(proj);
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

        private static void CollectRemaining(List<ResearchProjectDef> remaining, ResearchProjectDef frozen)
        {
            List<ResearchProjectDef> all = CollectIncluded();
            for (int i = 0; i < all.Count; i++)
            {
                ResearchProjectDef proj = all[i];
                if (IsFinishedTracked(proj) || proj == frozen)
                {
                    continue;
                }

                remaining.Add(proj);
            }
        }

        private static List<ResearchProjectDef> CollectIncluded()
        {
            List<ResearchProjectDef> list = new List<ResearchProjectDef>();
            List<ResearchProjectDef> all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                if (IsIncluded(all[i]))
                {
                    list.Add(all[i]);
                }
            }

            return list;
        }

        private static ResearchProjectDef FrozenProject()
        {
            if (Find.ResearchManager == null)
            {
                return null;
            }

            ResearchProjectDef currentProj = Find.ResearchManager.GetProject(null);
            if (currentProj == null || !IsIncluded(currentProj) || IsFinishedTracked(currentProj))
            {
                return null;
            }

            float progress = Find.ResearchManager.GetProgress(currentProj);
            if (progress <= 0f)
            {
                return null;
            }

            return currentProj;
        }

        private static bool IsFinishedTracked(ResearchProjectDef proj)
        {
            return proj != null && finishedSet.Contains(proj);
        }

        private static float ApparentOf(ResearchProjectDef proj)
        {
            return GetAssignedOrOriginal(proj) * proj.CostFactor(PlayerTechLevel());
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
            if (value < 0f)
            {
                value = 0f;
            }

            float step = RoundStep();
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
