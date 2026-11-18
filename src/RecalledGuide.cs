namespace KaleidoscopeEndless
{
    /// <summary>
    /// Where your current job is: the place the HUD's arrow line, the objective screen (J) and the map (M) point
    /// you to. Pure (no Unity types), so the headless tests can check every quest has somewhere to send you.
    /// </summary>
    public static class RcGuide
    {
        /// <summary>The place to go for the character's current quest, from where they stand. False when there's no quest left.</summary>
        public static bool Target(RcCharacter c, float px, float pz, out float x, out float y, out float z, out string name)
        {
            x = RcTown.LADDER_X; y = 0f; z = RcTown.LADDER_OFF_GROUND_Z; name = "The treehouse";
            RcQuest q = c.CurrentQuest;
            if (q == null) return false;
            if (q.World != RcWorlds.Now)
            {
                // The job is in the other world: the way there (or back) is the Recall Express.
                x = RcTown.StationX; z = RcTown.StationZ; name = "The Recall Express: the platform";
                return true;
            }
            if (c.QuestComplete) { name = RcWorlds.Egypt ? "Last Stop: hand it in" : "The treehouse: hand it in"; return true; }
            switch (q.Kind)
            {
                case RcQuestKind.Talk: name = "The treehouse: the quest board"; return true;
                case RcQuestKind.Craft: name = "The treehouse: the crafting bench"; return true;
                case RcQuestKind.Scene: name = "The treehouse: the quest board"; return true;
                case RcQuestKind.Station: x = RcTown.StationX; z = RcTown.StationZ; name = RcWorlds.Egypt ? "The Recall Express: the platform at Last Stop" : "The Recall Express: the junkyard station"; return true;
                case RcQuestKind.Boss:
                    x = RcTown.Arena[q.Zone, 0]; z = RcTown.Arena[q.Zone, 1]; name = RcData.BossNames[q.Zone];
                    Boss(q.Zone, ref y);
                    return true;
                case RcQuestKind.Reach:
                    x = RcTown.GateSides[q.Zone, 2]; z = RcTown.GateSides[q.Zone, 3]; name = RcData.ZoneNames[q.Zone];
                    return true;
            }
            // Toys to break (or to take parts from): the way in until you're inside, then the nearest of them.
            x = RcTown.GateSides[q.Zone, 2]; z = RcTown.GateSides[q.Zone, 3];
            name = RcData.ZoneNames[q.Zone] + ": the way in";
            bool inside = RcTown.ZoneAt(px, pz) == q.Zone, elite = q.Kind == RcQuestKind.Elite;
            if (!inside && !elite) return true;
            float best = float.MaxValue;
            for (int i = 0; i < RcTown.Spawns.Count; i++)
            {
                RcSpawn s = RcTown.Spawns[i];
                if (s.Zone != q.Zone) continue;
                RcEnemyDef def = RcData.Enemy(s.Enemy);
                if (def == null || def.Kind == RcEnemyKind.Boss || def.Kind == RcEnemyKind.Shadow) continue;
                if (elite && s.Enemy != q.Target) continue;
                float d = (s.X - px) * (s.X - px) + (s.Z - pz) * (s.Z - pz);
                if (d >= best) continue;
                best = d; x = s.X; y = s.Y; z = s.Z;
                name = elite ? def.Name : "Toys";
            }
            return true;
        }

        private static void Boss(int zone, ref float y)
        {
            for (int i = 0; i < RcTown.Spawns.Count; i++)
                if (RcTown.Spawns[i].Zone == zone && RcTown.Spawns[i].Enemy == RcData.E_BOSS0 + zone) y = RcTown.Spawns[i].Y;
        }

        private static readonly string[] Points = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        /// <summary>Which way (dx east, dz north) as a compass point.</summary>
        public static string Compass(float dx, float dz)
        {
            double a = System.Math.Atan2(dx, dz) * 180.0 / System.Math.PI; // 0 = north, 90 = east
            int i = (int)System.Math.Floor((a + 382.5) / 45.0) % 8;
            return Points[i];
        }

        /// <summary>"Patches: 310 m south-west", from where you stand (or "" when there's nothing left to do).</summary>
        public static string Where(RcCharacter c, float px, float py, float pz)
        {
            float x, y, z; string name;
            if (!Target(c, px, pz, out x, out y, out z, out name)) return "";
            float dx = x - px, dz = z - pz, d = (float)System.Math.Sqrt(dx * dx + dz * dz);
            if (d < 6f) return name + ": here" + (y - py > 2f ? ", upstairs" : py - y > 2f ? ", downstairs" : "");
            return name + ": " + (int)d + " m " + Compass(dx, dz) + (y - py > 2f ? ", upstairs" : py - y > 2f ? ", downstairs" : "");
        }
    }
}
