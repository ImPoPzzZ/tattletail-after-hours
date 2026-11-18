using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Tattletail Air Hockey's table as flat 2D physics: the puck gliding on air, two mallets, the rails (with cut
    /// corners so the puck never sits in one) and a goal slot in each end. Table space, meters: x along the table
    /// (seat 0 plays from the +x end, seat 1 from the -x end), y across. Stepped at 1 ms.
    /// </summary>
    public class AirHockeyTable
    {
        public const float L = 1.9f, W = 0.95f;           // the playing surface
        public const float PUCK = 0.04f, MALLET = 0.05f;
        public const float GOAL_HALF = 0.15f, CORNER = 0.09f;
        public const float MAX_PUCK = 5f;
        private const float STEP = 0.001f;

        private struct Wall { public Vector2 A, B, N; public bool End; } // N: the side the puck belongs on; End: an end rail (open at the goal)
        private readonly Wall[] _walls;
        private readonly Vector2[] _posts;                 // the goal mouths' corners (round, so the puck glances off)

        public Vector2 Puck, PuckVel;
        public readonly Vector2[] Mallet = new Vector2[2];
        public readonly Vector2[] MalletVel = new Vector2[2];
        public readonly Vector2[] Target = new Vector2[2];  // where each mallet is being moved to
        public readonly float[] MaxSpeed = { 6f, 6f };
        public bool PuckHit;                               // a mallet or rail was hit hard this frame (for sounds)
        public bool RailHit;

        public AirHockeyTable()
        {
            float x = L / 2f, y = W / 2f, c = CORNER;
            _walls = new[]
            {
                Seg(-x + c, y, x - c, y), Seg(x - c, -y, -x + c, -y),                 // the long sides
                Seg(x - c, y, x, y - c), Seg(x, -y + c, x - c, -y),                   // corners, +x end
                Seg(-x, y - c, -x + c, y), Seg(-x + c, -y, -x, -y + c),               // corners, -x end
                Seg(x, y - c, x, GOAL_HALF), Seg(x, -GOAL_HALF, x, -y + c),           // +x end, either side of its goal
                Seg(-x, GOAL_HALF, -x, y - c), Seg(-x, -y + c, -x, -GOAL_HALF),       // -x end
            };
            _posts = new[] { new Vector2(x, GOAL_HALF), new Vector2(x, -GOAL_HALF), new Vector2(-x, GOAL_HALF), new Vector2(-x, -GOAL_HALF) };
            Reset(0);
        }

        // A rail from a to b, facing the middle of the table (it's convex, so that's always the inside).
        private static Wall Seg(float ax, float ay, float bx, float by)
        {
            Vector2 a = new Vector2(ax, ay), b = new Vector2(bx, by), d = (b - a).normalized;
            Vector2 n = new Vector2(-d.y, d.x);
            if (Vector2.Dot(n, -a) < 0f) n = -n;
            return new Wall { A = a, B = b, N = n, End = Mathf.Abs(ax - bx) < 1e-4f && Mathf.Abs(Mathf.Abs(ax) - L / 2f) < 1e-4f };
        }

        /// <summary>Mallets home, the puck resting on the side of the seat that serves.</summary>
        public void Reset(int serve)
        {
            Mallet[0] = Target[0] = new Vector2(L / 2f - 0.2f, 0f);
            Mallet[1] = Target[1] = new Vector2(-L / 2f + 0.2f, 0f);
            MalletVel[0] = MalletVel[1] = Vector2.zero;
            Puck = new Vector2(serve == 0 ? 0.3f : -0.3f, 0f);
            PuckVel = Vector2.zero;
        }

        /// <summary>A mallet can't leave its own half or pass through a rail.</summary>
        public static Vector2 Clamp(int seat, Vector2 p)
        {
            float lo = seat == 0 ? MALLET * 0.6f : -L / 2f + MALLET, hi = seat == 0 ? L / 2f - MALLET : -MALLET * 0.6f;
            return new Vector2(Mathf.Clamp(p.x, lo, hi), Mathf.Clamp(p.y, -W / 2f + MALLET, W / 2f - MALLET));
        }

        /// <summary>0 or 1: which seat just let one in (the puck went through its end), or -1.</summary>
        public int Conceded
        {
            get
            {
                if (Puck.x > L / 2f + PUCK) return 0;
                if (Puck.x < -L / 2f - PUCK) return 1;
                return -1;
            }
        }

        /// <summary>Runs the mallets (and the puck, if simulate) for dt seconds.</summary>
        public void Tick(float dt, bool simulate)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / STEP), 1, 60);
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                for (int s = 0; s < 2; s++)
                {
                    Vector2 goal = Clamp(s, Target[s]);
                    Vector2 prev = Mallet[s];
                    Mallet[s] = Vector2.MoveTowards(Mallet[s], goal, MaxSpeed[s] * h);
                    MalletVel[s] = (Mallet[s] - prev) / h;
                }
                if (simulate) StepPuck(h);
            }
        }

        private void StepPuck(float h)
        {
            Puck += PuckVel * h;
            PuckVel *= 1f - 0.3f * h; // it glides (a little air drag, so rallies don't only ever speed up)
            // The table is convex, so every rail's whole line is a wall: no gap at the joints for a fast puck to slip
            // through. Only the end lines open up, in front of the goals.
            for (int s = 0; s < 2; s++) if (CollideCircle(Mallet[s], MALLET, MalletVel[s], 0.65f)) PuckHit = true;
            for (int i = 0; i < _posts.Length; i++) CollideCircle(_posts[i], 0.008f, Vector2.zero, 0.85f);
            bool inGoalMouth = Mathf.Abs(Puck.y) < GOAL_HALF;
            for (int i = 0; i < _walls.Length; i++) // (last: a mallet can pin the puck against a rail, never push it through)
                if (!(_walls[i].End && inGoalMouth)) CollideWall(_walls[i]);
            if (PuckVel.sqrMagnitude > MAX_PUCK * MAX_PUCK) PuckVel = PuckVel.normalized * MAX_PUCK;
        }

        private void CollideWall(Wall w)
        {
            float d = Vector2.Dot(Puck - w.A, w.N); // signed: past the rail counts too (it can't tunnel out)
            if (d >= PUCK) return;
            Puck += w.N * (PUCK - d);
            float vn = Vector2.Dot(PuckVel, w.N);
            if (vn < 0f)
            {
                PuckVel -= 1.8f * vn * w.N;
                if (vn < -0.4f) RailHit = true;
            }
        }

        private bool CollideCircle(Vector2 c, float r, Vector2 vel, float bounce)
        {
            Vector2 d = Puck - c;
            float R = PUCK + r, dist = d.magnitude;
            if (dist >= R) return false;
            Vector2 n = dist > 1e-6f ? d / dist : Vector2.right;
            Puck = c + n * R;
            Vector2 rel = PuckVel - vel;
            float vn = Vector2.Dot(rel, n);
            if (vn < 0f) PuckVel = vel + rel - (1f + bounce) * vn * n;
            return vn < -0.3f;
        }

        // ── The Tattletail AI ────────────────────────────────────────────────
        /// <summary>
        /// Where the AI in this seat wants its mallet (and how fast it moves: baseSpeed, faster when it strikes).
        /// Guards the goal while the puck's away; on its side, lines up behind the puck and hits it: straight at the
        /// goal, or banked off a side rail (it mixes them up every few seconds).
        /// </summary>
        public Vector2 AiTarget(int seat, float t, float baseSpeed)
        {
            float s = seat == 0 ? 1f : -1f;
            MaxSpeed[seat] = baseSpeed;
            Vector2 own = new Vector2(s * L / 2f, 0f);
            bool mySide = s * Puck.x > 0f;
            bool incoming = s * PuckVel.x > 0.3f && PuckVel.magnitude > 1.5f;
            if (mySide && !(incoming && s * (Puck.x - Mallet[seat].x) < 0f))
            {
                int mode = (int)(t / 3f) % 3;
                float gy = Mathf.Sin(t * 1.7f) * 0.1f;
                Vector2 aim = new Vector2(-s * L / 2f, mode == 0 ? gy : mode == 1 ? W - gy : -W - gy); // banks: the goal mirrored in a rail
                Vector2 dir = (Puck - aim).normalized;
                Vector2 behind = Puck + dir * (PUCK + MALLET + 0.02f);
                bool lined = (Mallet[seat] - behind).magnitude < 0.05f || Vector2.Dot((Mallet[seat] - Puck).normalized, dir) > 0.85f;
                if (!lined) return behind;
                MaxSpeed[seat] = baseSpeed * 1.8f; // strike
                return Puck - dir * 0.12f;
            }
            // Defend: between the puck and the goal, on a line in front of it.
            float y = Mathf.Clamp(Puck.y * 0.6f, -GOAL_HALF - 0.05f, GOAL_HALF + 0.05f);
            return new Vector2(own.x - s * 0.18f, y);
        }
    }
}
