using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ResearchTotal
{
    public class GameComponent_ResearchTotal : GameComponent
    {
        public float spentApparent;
        public float spentAnomaly;
        public TechLevel lastTechLevel = TechLevel.Undefined;
        public bool ready;
        public Dictionary<ResearchProjectDef, float> assignedCosts = new Dictionary<ResearchProjectDef, float>();
        public List<ResearchProjectDef> finishedProjects = new List<ResearchProjectDef>();

        public GameComponent_ResearchTotal(Game game)
        {
            ResearchTotalEngine.Detach();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref spentApparent, "spentApparent", 0f);
            Scribe_Values.Look(ref spentAnomaly, "spentAnomaly", 0f);
            Scribe_Values.Look(ref lastTechLevel, "lastTechLevel", TechLevel.Undefined);
            Scribe_Collections.Look(ref assignedCosts, "assignedCosts", LookMode.Def, LookMode.Value);
            Scribe_Collections.Look(ref finishedProjects, "finishedProjects", LookMode.Def);

            if (assignedCosts == null)
            {
                assignedCosts = new Dictionary<ResearchProjectDef, float>();
            }

            if (finishedProjects == null)
            {
                finishedProjects = new List<ResearchProjectDef>();
            }

            List<ResearchProjectDef> drop = null;
            foreach (KeyValuePair<ResearchProjectDef, float> kv in assignedCosts)
            {
                if (kv.Key == null)
                {
                    if (drop == null)
                    {
                        drop = new List<ResearchProjectDef>();
                    }

                    drop.Add(kv.Key);
                }
            }

            if (drop != null)
            {
                for (int i = 0; i < drop.Count; i++)
                {
                    assignedCosts.Remove(drop[i]);
                }
            }

            finishedProjects.RemoveAll(proj => proj == null);
        }

        public override void FinalizeInit()
        {
            ResearchTotalEngine.InitializeForGame(this);
        }

        public override void GameComponentTick()
        {
            if (!ready || Find.TickManager.TicksGame % 60 != 0)
            {
                return;
            }

            ResearchTotalEngine.CheckTechLevel();
        }
    }
}
