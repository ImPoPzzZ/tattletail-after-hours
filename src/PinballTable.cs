using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Tattletail Pinball's table as flat 2D physics (the 3D machine just draws it): walls, slingshots, the star
    /// targets, three pop bumpers, the House's door, the ramp lane, the shooter lane with its plunger, and two flippers.
    /// Table space, in meters: x across (the player's right is +x), y up the table (0 = the front edge); the same space
    /// as tools/pinball_art.py's playfield. Stepped at 1 ms, so a fast ball never slips through a wall or a flipper.
    /// </summary>
    public class PinballTable
    {
        public const float BALL = 0.0135f;                  // ball radius (a real 27 mm pinball)
        public const float XC = -0.0225f;                   // the middle of the playfield (the shooter lane takes the right edge)
        public const float LANE_X = 0.2575f;                // the shooter lane's middle
        public const float GRAVITY = 1.9f;                  // down the slope (a real 6.5° table is ~1.1; a little livelier here)
        private const float STEP = 0.001f;

        public enum Kind { Wall, Sling, Target, Gate, Plunger }

        public class Seg
        {
            public Vector2 A, B;
            public float Radius = 0.004f, Bounce = 0.45f, Kick;
            public Kind Kind;
            public int Id;
            public bool Hidden; // physics only (the House draws its own walls)
        }

        public class Bumper { public Vector2 C; public float R = 0.03f; public int Id; }

        public class Flipper
        {
            public Vector2 Pivot;
            public float Length = 0.08f, R0 = 0.012f, R1 = 0.006f;
            public float Rest = -30f, Up = 28f;               // degrees, measured as a left flipper (the right one is mirrored)
            public bool Left, Pressed;
            public float Angle = -30f, Omega;                 // Omega: rad/s in table space (+ = counter-clockwise)
            public float Dir { get { return Left ? Angle : 180f - Angle; } } // the direction it points, table degrees
            public Vector2 Tip { get { float a = Dir * Mathf.Deg2Rad; return Pivot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Length; } }
        }

        public readonly List<Seg> Segs = new List<Seg>();
        public readonly List<Bumper> Bumpers = new List<Bumper>();
        public readonly Flipper LeftFlipper, RightFlipper;
        public readonly Seg Plunger;

        // The House's door (a scoop), the ramp's entrance, the ramp's path (x, y, height above the table).
        public const float DOOR_HALF = 0.024f, DOOR_Y = 0.93f;
        public static readonly Vector2 TARGET_FROM = new Vector2(0.11f, 0.56f), TARGET_TO = new Vector2(0.2f, 0.74f); // the star target bank
        public static readonly Vector3[] RampPath =
        {
            new Vector3(-0.2475f, 0.80f, 0f), new Vector3(-0.245f, 0.95f, 0.05f), new Vector3(-0.17f, 1.07f, 0.1f),
            new Vector3(-0.02f, 1.1f, 0.13f), new Vector3(0.14f, 1.04f, 0.12f), new Vector3(0.2f, 0.9f, 0.09f),
            new Vector3(0.2f, 0.66f, 0.05f), new Vector3(0.2f, 0.47f, 0f),
        };

        // The ball.
        public Vector2 Pos, Vel;
        public float Pull;               // plunger, 0..1
        public bool PlungerReleased;     // set for one step by Launch()

        // What the ball hit this frame (the game reads and clears these).
        public readonly List<Seg> HitSegs = new List<Seg>();
        public readonly List<Bumper> HitBumpers = new List<Bumper>();
        public bool FlipperHit;

        public PinballTable()
        {
            // The outline: left side, the round top, the right side.
            Wall(-0.28f, -0.05f, -0.28f, 0.88f);
            const int ARC = 20;
            for (int i = 0; i < ARC; i++)
            {
                float a0 = Mathf.PI * i / ARC, a1 = Mathf.PI * (i + 1) / ARC;
                Wall(-Mathf.Cos(a0) * 0.28f, 0.88f + Mathf.Sin(a0) * 0.28f, -Mathf.Cos(a1) * 0.28f, 0.88f + Mathf.Sin(a1) * 0.28f);
            }
            Wall(0.28f, 0.88f, 0.28f, -0.05f);
            // The shooter lane's inner wall, and its one-way gate (the ball goes out, never back down).
            Wall(0.235f, -0.05f, 0.235f, 0.80f);
            Segs.Add(new Seg { A = new Vector2(0.235f, 0.80f), B = new Vector2(0.28f, 0.835f), Kind = Kind.Gate }); // sloped: a weak launch rolls off it onto the table
            Plunger = new Seg { A = new Vector2(0.235f, 0.016f), B = new Vector2(0.28f, 0.016f), Kind = Kind.Plunger, Bounce = 0.1f };
            Segs.Add(Plunger);
            // The funnels down to the flippers.
            Wall(-0.28f, 0.32f, -0.135f, 0.187f);
            Wall(0.235f, 0.32f, 0.09f, 0.187f);
            // Slingshots: the side facing in kicks.
            // (they sit well above the funnels: the inlane runs under each one, down to its flipper)
            Sling(-0.195f, 0.40f, -0.195f, 0.315f, -0.14f, 0.265f, true);
            Sling(2f * XC + 0.195f, 0.40f, 2f * XC + 0.195f, 0.315f, 2f * XC + 0.14f, 0.265f, false);
            // The ramp lane along the left wall.
            Wall(-0.215f, 0.55f, -0.215f, 0.80f);
            Wall(-0.28f, 0.87f, -0.215f, 0.83f); // (a sloped backstop over the ramp's mouth: nothing drops in from above, nothing rests on it)
            // The House: its front wall with the door in it, the sides and back, and the door's little channel.
            int house = Segs.Count;
            Wall(XC - 0.07f, DOOR_Y, XC - DOOR_HALF, DOOR_Y);
            Wall(XC + DOOR_HALF, DOOR_Y, XC + 0.07f, DOOR_Y);
            Wall(XC - 0.07f, DOOR_Y, XC - 0.07f, 1.03f);
            Wall(XC + 0.07f, DOOR_Y, XC + 0.07f, 1.03f);
            Wall(XC - 0.07f, 1.03f, XC, 1.06f); // a peaked back: nothing comes to rest behind the House
            Wall(XC, 1.06f, XC + 0.07f, 1.03f);
            Wall(XC - DOOR_HALF, DOOR_Y, XC - DOOR_HALF, DOOR_Y + 0.03f);
            Wall(XC + DOOR_HALF, DOOR_Y, XC + DOOR_HALF, DOOR_Y + 0.03f);
            for (int i = house; i < Segs.Count; i++) Segs[i].Hidden = true;
            // The star targets: four faces on one slanted bank on the right.
            Vector2 t0 = TARGET_FROM, t1 = TARGET_TO;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = Vector2.Lerp(t0, t1, i / 4f), b = Vector2.Lerp(t0, t1, (i + 1) / 4f);
                Segs.Add(new Seg { A = a, B = b, Kind = Kind.Target, Id = i, Bounce = 0.3f });
            }
            Wall(0.11f, 0.56f, 0.235f, 0.50f);   // seals the pocket behind the bank
            Wall(TARGET_TO.x, TARGET_TO.y, 0.235f, 0.78f); // and caps its top, sloping back down toward the table
            // Pop bumpers.
            Bumpers.Add(new Bumper { C = new Vector2(XC - 0.075f, 0.76f), Id = 0 });
            Bumpers.Add(new Bumper { C = new Vector2(XC + 0.075f, 0.76f), Id = 1 });
            Bumpers.Add(new Bumper { C = new Vector2(XC, 0.66f), Id = 2 });
            // Flippers.
            LeftFlipper = new Flipper { Pivot = new Vector2(XC - 0.1f, 0.17f), Left = true };
            RightFlipper = new Flipper { Pivot = new Vector2(XC + 0.1f, 0.17f), Left = false };
        }

        private void Wall(float x0, float y0, float x1, float y1)
        {
            Segs.Add(new Seg { A = new Vector2(x0, y0), B = new Vector2(x1, y1) });
        }

        // A triangle; the edge from (x0,y0) to (x2,y2) faces the middle and kicks.
        private void Sling(float x0, float y0, float x1, float y1, float x2, float y2, bool left)
        {
            Wall(x0, y0, x1, y1);
            Wall(x1, y1, x2, y2);
            Segs.Add(new Seg { A = new Vector2(x0, y0), B = new Vector2(x2, y2), Kind = Kind.Sling, Kick = 0.9f, Bounce = 0.5f, Id = left ? 0 : 1 });
        }

        /// <summary>Puts the ball on the plunger.</summary>
        public void Serve() { Pos = new Vector2(LANE_X, Plunger.A.y + BALL + Plunger.Radius + 0.001f); Vel = Vector2.zero; Pull = 0f; }

        public bool InLane { get { return Pos.x > 0.235f && Pos.y < 0.8f; } }

        /// <summary>Lets the plunger go: the ball flies up the lane as hard as it was pulled.</summary>
        public void Launch()
        {
            if (InLane && Pos.y < 0.1f)
            {
                Pos.y = Mathf.Max(Pos.y, 0.016f + BALL + Plunger.Radius + 0.001f); // the plunger springs back up under the ball (not through it)
                Vel = new Vector2(0f, 1.2f + 2.3f * Pull);
            }
            Pull = 0f;
        }

        /// <summary>Runs the flippers and (if live) the ball for dt seconds.</summary>
        public void Tick(float dt, bool ballLive)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / STEP), 1, 50);
            float h = dt / n;
            float plungerY = 0.016f - Pull * 0.04f;
            Plunger.A.y = Plunger.B.y = plungerY;
            for (int i = 0; i < n; i++)
            {
                MoveFlipper(LeftFlipper, h);
                MoveFlipper(RightFlipper, h);
                if (ballLive) StepBall(h);
            }
        }

        private static void MoveFlipper(Flipper f, float h)
        {
            float prev = f.Angle;
            f.Angle = Mathf.MoveTowards(f.Angle, f.Pressed ? f.Up : f.Rest, (f.Pressed ? 1500f : 700f) * h);
            float w = (f.Angle - prev) * Mathf.Deg2Rad / h;
            f.Omega = f.Left ? w : -w;
        }

        private void StepBall(float h)
        {
            Vel.y -= GRAVITY * h;
            Vel *= 1f - 0.12f * h;                                    // rolling drag
            if (Vel.sqrMagnitude > 16f) Vel = Vel.normalized * 4f;    // nothing flies off the table
            Pos += Vel * h;

            for (int i = 0; i < Segs.Count; i++) Collide(Segs[i]);
            for (int i = 0; i < Bumpers.Count; i++) CollideBumper(Bumpers[i]);
            CollideFlipper(LeftFlipper);
            CollideFlipper(RightFlipper);
        }

        private void Collide(Seg s)
        {
            Vector2 ab = s.B - s.A;
            float t = Mathf.Clamp01(Vector2.Dot(Pos - s.A, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
            Vector2 c = s.A + ab * t;
            Vector2 d = Pos - c;
            float R = BALL + s.Radius, dist = d.magnitude;
            if (dist >= R || dist < 1e-6f) return;
            Vector2 n = d / dist;
            if (s.Kind == Kind.Gate && (n.y < 0.5f || Vel.y > 0f)) return; // only stops a ball coming down onto it
            Pos = c + n * R;
            float vn = Vector2.Dot(Vel, n);
            if (vn >= 0f) return;
            Vel -= (1f + s.Bounce) * vn * n;
            if (s.Kick > 0f && vn < -0.12f) Vel += n * s.Kick;
            if (vn < -0.12f && (s.Kind == Kind.Sling || s.Kind == Kind.Target) && !HitSegs.Contains(s)) HitSegs.Add(s);
        }

        private void CollideBumper(Bumper b)
        {
            Vector2 d = Pos - b.C;
            float R = BALL + b.R, dist = d.magnitude;
            if (dist >= R || dist < 1e-6f) return;
            Vector2 n = d / dist;
            Pos = b.C + n * R;
            float vn = Vector2.Dot(Vel, n);
            if (vn < 0f) Vel -= 1.5f * vn * n;
            Vel += n * 0.85f; // pop
            if (!HitBumpers.Contains(b)) HitBumpers.Add(b);
        }

        private void CollideFlipper(Flipper f)
        {
            Vector2 tip = f.Tip, ab = tip - f.Pivot;
            float t = Mathf.Clamp01(Vector2.Dot(Pos - f.Pivot, ab) / ab.sqrMagnitude);
            Vector2 c = f.Pivot + ab * t;
            float r = Mathf.Lerp(f.R0, f.R1, t);
            Vector2 d = Pos - c;
            float R = BALL + r, dist = d.magnitude;
            if (dist >= R || dist < 1e-6f) return;
            Vector2 n = d / dist;
            Pos = c + n * R;
            Vector2 arm = c + n * r - f.Pivot;
            Vector2 surface = new Vector2(-arm.y, arm.x) * f.Omega;   // how fast that bit of flipper is moving
            Vector2 rel = Vel - surface;
            float vn = Vector2.Dot(rel, n);
            if (vn >= 0f) return;
            Vel = surface + rel - (1f + 0.5f) * vn * n;
            if (Mathf.Abs(f.Omega) > 1f) FlipperHit = true;
        }

        public const float RAMP_X = -0.215f, RAMP_Y = 0.80f;

        /// <summary>True when the ball has just climbed up through the ramp's mouth (from below, going up).</summary>
        public bool EnteredRamp(float prevY) { return Pos.x < RAMP_X && prevY <= RAMP_Y && Pos.y > RAMP_Y && Vel.y > 0f; }

        /// <summary>True when the ball has rolled in through the House's door.</summary>
        public bool InHouseDoor { get { return Mathf.Abs(Pos.x - XC) < 0.07f && Pos.y > DOOR_Y + 0.006f && Pos.y < 1.03f; } } // anywhere inside: a fast ball can be past the door by the next frame

        /// <summary>Where a ramp ride is at t (0..1): x, y on the table and the height above it.</summary>
        public static Vector3 RampPoint(float t)
        {
            int segs = RampPath.Length - 1;
            float f = Mathf.Clamp01(t) * segs;
            int i = Mathf.Min((int)f, segs - 1);
            float u = f - i;
            Vector3 p0 = RampPath[Mathf.Max(i - 1, 0)], p1 = RampPath[i], p2 = RampPath[i + 1], p3 = RampPath[Mathf.Min(i + 2, segs)];
            return 0.5f * ((2f * p1) + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
        }
    }
}
