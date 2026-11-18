using System.Collections.Generic;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The town's dressing: the small things that make a place look lived in and then abandoned: mailboxes, toys left on
    /// lawns, Waygetter's parcels on porches with a Tattletail sitting on them, recall posters, puddles, tires, drums,
    /// shopping carts, trucks. Every prop is a handful of boxes; the scatter never puts one inside something solid (it
    /// remembers what the part has built so far), and almost nothing here is solid itself, so it can't close a path.
    /// </summary>
    public static partial class RcTown
    {
        // ── What's been built so far in this part (so the scatter can look for room) ──
        private struct Foot { public float X0, Z0, X1, Z1; public bool Paved; }
        private static readonly List<Foot> _feet = new List<Foot>();

        /// <summary>Passes everything through to the real sink, and remembers where the solid things and the paving are.</summary>
        private sealed class RcRecord : RcSink
        {
            private readonly RcSink _to;
            public RcRecord(RcSink to) { _to = to; _feet.Clear(); }

            public override void Box(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1, bool collide)
            {
                if (collide && y0 < 2.6f && y1 - y0 > 0.15f) _feet.Add(new Foot { X0 = x0, Z0 = z0, X1 = x1, Z1 = z1 });
                else if (!collide && y0 < 0.2f && (m == RcMat.Road || m == RcMat.Walk || m == RcMat.Path || m == RcMat.Packed)) _feet.Add(new Foot { X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Paved = true });
                _to.Box(m, x0, y0, z0, x1, y1, z1, collide);
            }

            public override void Walk(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1)
            {
                if (y1 > 0.12f) _feet.Add(new Foot { X0 = x0, Z0 = z0, X1 = x1, Z1 = z1 });   // (a floor or a step: not the ground)
                _to.Walk(m, x0, y0, z0, x1, y1, z1);
            }

            public override void Cyl(RcMat m, float x, float y0, float z, float radius, float height, bool collide)
            {
                if (collide && y0 < 2.6f) _feet.Add(new Foot { X0 = x - radius, Z0 = z - radius, X1 = x + radius, Z1 = z + radius });
                _to.Cyl(m, x, y0, z, radius, height, collide);
            }

            public override void Ball(RcMat m, float x, float y, float z, float rx, float ry, float rz) { _to.Ball(m, x, y, z, rx, ry, rz); }
            public override void Light(float x, float y, float z, int color, float range, float intensity) { _to.Light(x, y, z, color, range, intensity); }
        }

        /// <summary>Nothing solid within r of here (and, if onGrass, no road or paving either).</summary>
        private static bool Room(float x, float z, float r, bool onGrass)
        {
            for (int i = 0; i < _feet.Count; i++)
            {
                Foot f = _feet[i];
                if (f.Paved && !onGrass) continue;
                if (x > f.X0 - r && x < f.X1 + r && z > f.Z0 - r && z < f.Z1 + r) return false;
            }
            return true;
        }

        private static void Keep(float x0, float z0, float x1, float z1) { _feet.Add(new Foot { X0 = x0, Z0 = z0, X1 = x1, Z1 = z1 }); }

        private delegate void Prop(float x, float z);

        /// <summary>Tries 'tries' random spots in a rectangle and puts a prop at each one with room (a margin 'r' clear of everything solid).</summary>
        private static int Scatter(float x0, float z0, float x1, float z1, int tries, float r, bool onGrass, Prop prop)
        {
            int placed = 0;
            for (int i = 0; i < tries; i++)
            {
                float x = x0 + Rnd() * (x1 - x0), z = z0 + Rnd() * (z1 - z0);
                if (!Room(x, z, r, onGrass)) continue;
                prop(x, z);
                Keep(x - r * 0.5f, z - r * 0.5f, x + r * 0.5f, z + r * 0.5f);
                placed++;
            }
            return placed;
        }

        // ── The props ────────────────────────────────────────────────────────
        private static void PTrash(float x, float z)
        {
            S.Cyl(RcMat.Metal, x, 0f, z, 0.3f, 0.85f, true);
            S.Cyl(RcMat.Metal, x, 0.85f, z, 0.33f, 0.06f, false);
            Vis(RcMat.Black, x + 0.3f, 0f, z - 0.2f, x + 0.62f, 0.38f, z + 0.12f);                // a bag beside it
            if (Vary(x, z, 3) < 0.4f) Vis(RcMat.Paper, x + 0.5f, 0f, z + 0.3f, x + 0.62f, 0.03f, z + 0.46f);   // and a paper
        }

        private static void PHydrant(float x, float z)
        {
            S.Cyl(RcMat.CarC, x, 0f, z, 0.17f, 0.62f, true);
            S.Cyl(RcMat.CarC, x, 0.62f, z, 0.2f, 0.07f, false);
            S.Cyl(RcMat.Metal, x, 0.69f, z, 0.07f, 0.1f, false);
            Vis(RcMat.Metal, x - 0.27f, 0.32f, z - 0.06f, x + 0.27f, 0.42f, z + 0.06f);
        }

        private static void PMailbox(float x, float z)
        {
            Vis(RcMat.Wood, x - 0.05f, 0f, z - 0.05f, x + 0.05f, 0.95f, z + 0.05f);
            Vis(RcMat.Metal, x - 0.2f, 0.95f, z - 0.12f, x + 0.2f, 1.2f, z + 0.12f);
            Vis(RcMat.CarC, x + 0.2f, 1.08f, z - 0.03f, x + 0.24f, 1.3f, z + 0.03f);              // the flag
        }

        private static void PBush(float x, float z, float s)
        {
            S.Ball(RcMat.Leaf, x, 0.45f * s, z, 0.7f * s, 0.55f * s, 0.7f * s);
            S.Ball(RcMat.Leaf, x + 0.55f * s, 0.35f * s, z + 0.2f * s, 0.5f * s, 0.4f * s, 0.5f * s);
            S.Ball(RcMat.Leaf, x - 0.45f * s, 0.32f * s, z - 0.25f * s, 0.45f * s, 0.38f * s, 0.45f * s);
        }

        private static void PCone(float x, float z)
        {
            S.Cyl(RcMat.Orange, x, 0f, z, 0.17f, 0.55f, false);
            S.Cyl(RcMat.Paper, x, 0.28f, z, 0.125f, 0.09f, false);
            Vis(RcMat.Black, x - 0.22f, 0f, z - 0.22f, x + 0.22f, 0.04f, z + 0.22f);
        }

        private static void PPuddle(float x, float z, float w, float d, bool road)
        {
            float y = road ? 0.05f : 0f;
            Vis(RcMat.Glass, x - w * 0.5f, y, z - d * 0.5f, x + w * 0.5f, y + 0.012f, z + d * 0.5f);
            Vis(RcMat.Glass, x - w * 0.3f, y, z - d * 0.5f - 0.18f, x + w * 0.3f, y + 0.012f, z + d * 0.5f + 0.18f);
        }

        private static void PManhole(float x, float z)
        {
            S.Cyl(RcMat.Metal, x, 0.05f, z, 0.5f, 0.02f, false);
            Vis(RcMat.Black, x - 0.35f, 0.07f, z - 0.025f, x + 0.35f, 0.075f, z + 0.025f);
            Vis(RcMat.Black, x - 0.025f, 0.07f, z - 0.35f, x + 0.025f, 0.075f, z + 0.35f);
        }

        private static void PLitter(float x, float z)
        {
            for (int i = 0; i < 4; i++)
            {
                float ox = (Vary(x, z, i) - 0.5f) * 1.6f, oz = (Vary(x, z, i + 7) - 0.5f) * 1.6f;
                RcMat m = i % 3 == 0 ? RcMat.Paper : i % 3 == 1 ? RcMat.Cardboard : RcMat.Paper;
                Vis(m, x + ox, 0f, z + oz, x + ox + 0.22f, 0.012f, z + oz + 0.3f);
            }
        }

        private static void PBall(float x, float z, RcMat m) { S.Ball(m, x, 0.2f, z, 0.2f, 0.2f, 0.2f); }

        private static void PBlocks(float x, float z)
        {
            Vis(RcMat.CarA, x, 0f, z, x + 0.22f, 0.22f, z + 0.22f);
            Vis(RcMat.CarB, x + 0.26f, 0f, z + 0.05f, x + 0.48f, 0.22f, z + 0.27f);
            Vis(RcMat.CarC, x + 0.1f, 0.22f, z + 0.03f, x + 0.32f, 0.44f, z + 0.25f);
            Vis(RcMat.Pink, x - 0.3f, 0f, z + 0.1f, x - 0.08f, 0.22f, z + 0.32f);
        }

        private static void PTricycle(float x, float z)
        {
            Vis(RcMat.Black, x - 0.04f, 0f, z + 0.3f, x + 0.04f, 0.36f, z + 0.66f);                 // the big front wheel
            Vis(RcMat.Black, x - 0.3f, 0f, z - 0.3f, x - 0.22f, 0.24f, z - 0.06f);                  // two small ones behind
            Vis(RcMat.Black, x + 0.22f, 0f, z - 0.3f, x + 0.3f, 0.24f, z - 0.06f);
            Vis(RcMat.CarC, x - 0.05f, 0.3f, z - 0.3f, x + 0.05f, 0.4f, z + 0.5f);                   // the frame
            Vis(RcMat.Black, x - 0.14f, 0.4f, z - 0.3f, x + 0.14f, 0.46f, z - 0.05f);               // the seat
            Vis(RcMat.Metal, x - 0.2f, 0.62f, z + 0.46f, x + 0.2f, 0.66f, z + 0.5f);                // the handlebar
            Vis(RcMat.Metal, x - 0.025f, 0.4f, z + 0.46f, x + 0.025f, 0.66f, z + 0.5f);
        }

        private static void PTeddy(float x, float z)
        {
            S.Ball(RcMat.Cardboard, x, 0.2f, z, 0.2f, 0.22f, 0.17f);
            S.Ball(RcMat.Cardboard, x, 0.5f, z, 0.15f, 0.14f, 0.14f);
            S.Ball(RcMat.Cardboard, x - 0.11f, 0.63f, z, 0.06f, 0.06f, 0.05f);
            S.Ball(RcMat.Cardboard, x + 0.11f, 0.63f, z, 0.06f, 0.06f, 0.05f);
            S.Ball(RcMat.Cardboard, x - 0.2f, 0.26f, z + 0.1f, 0.07f, 0.12f, 0.07f);
            S.Ball(RcMat.Cardboard, x + 0.2f, 0.26f, z + 0.1f, 0.07f, 0.12f, 0.07f);
            Vis(RcMat.Black, x - 0.07f, 0.52f, z + 0.12f, x - 0.04f, 0.56f, z + 0.15f);
            Vis(RcMat.Black, x + 0.04f, 0.52f, z + 0.12f, x + 0.07f, 0.56f, z + 0.15f);
        }

        /// <summary>One of the recalled Tattletails, sitting where it was told to wait (egg body, tall ears, glowing eyes).</summary>
        private static void PTattletail(float x, float y, float z, RcMat fur)
        {
            S.Ball(fur, x, y + 0.2f, z, 0.17f, 0.22f, 0.15f);
            S.Ball(RcMat.Paper, x, y + 0.17f, z + 0.07f, 0.1f, 0.13f, 0.09f);
            Vis(fur, x - 0.15f, y + 0.4f, z - 0.03f, x - 0.08f, y + 0.66f, z + 0.03f);
            Vis(fur, x + 0.08f, y + 0.4f, z - 0.03f, x + 0.15f, y + 0.66f, z + 0.03f);
            Vis(RcMat.BlueGlow, x - 0.1f, y + 0.31f, z + 0.12f, x - 0.05f, y + 0.35f, z + 0.15f);
            Vis(RcMat.BlueGlow, x + 0.05f, y + 0.31f, z + 0.12f, x + 0.1f, y + 0.35f, z + 0.15f);
        }

        /// <summary>"Please leave your Tattletail on the porch": a parcel with recall tape on it, and the toy waiting on top.</summary>
        private static void PParcel(float x, float z, RcMat fur)
        {
            Vis(RcMat.Cardboard, x - 0.32f, 0.16f, z - 0.25f, x + 0.32f, 0.62f, z + 0.25f);
            Vis(RcMat.Tape, x - 0.05f, 0.62f, z - 0.25f, x + 0.05f, 0.64f, z + 0.25f);
            Vis(RcMat.Tape, x - 0.32f, 0.3f, z - 0.26f, x + 0.32f, 0.38f, z - 0.25f);
            Vis(RcMat.Paper, x + 0.1f, 0.4f, z - 0.26f, x + 0.28f, 0.55f, z - 0.25f);
            PTattletail(x, 0.64f, z, fur);
        }

        private static void PPoster(float x, float y, float z, bool faceX)
        {
            if (faceX)
            {
                Vis(RcMat.Paper, x, y, z - 0.22f, x + 0.025f, y + 0.6f, z + 0.22f);
                Vis(RcMat.CarC, x + 0.025f, y + 0.46f, z - 0.2f, x + 0.04f, y + 0.58f, z + 0.2f);
                Vis(RcMat.Black, x + 0.025f, y + 0.22f, z - 0.16f, x + 0.04f, y + 0.26f, z + 0.16f);
                Vis(RcMat.Black, x + 0.025f, y + 0.1f, z - 0.12f, x + 0.04f, y + 0.14f, z + 0.12f);
            }
            else
            {
                Vis(RcMat.Paper, x - 0.22f, y, z, x + 0.22f, y + 0.6f, z + 0.025f);
                Vis(RcMat.CarC, x - 0.2f, y + 0.46f, z + 0.025f, x + 0.2f, y + 0.58f, z + 0.04f);
                Vis(RcMat.Black, x - 0.16f, y + 0.22f, z + 0.025f, x + 0.16f, y + 0.26f, z + 0.04f);
                Vis(RcMat.Black, x - 0.12f, y + 0.1f, z + 0.025f, x + 0.12f, y + 0.14f, z + 0.04f);
            }
        }

        private static void PBarrel(float x, float z, RcMat m, bool solid)
        {
            S.Cyl(m, x, 0f, z, 0.3f, 0.9f, solid);
            S.Cyl(RcMat.Metal, x, 0.2f, z, 0.315f, 0.05f, false);
            S.Cyl(RcMat.Metal, x, 0.62f, z, 0.315f, 0.05f, false);
        }

        private static void PFireBarrel(float x, float z)
        {
            PBarrel(x, z, RcMat.Rust, true);
            Vis(RcMat.RedGlow, x - 0.18f, 0.9f, z - 0.18f, x + 0.18f, 1.05f, z + 0.18f);
            Vis(RcMat.Glow, x - 0.08f, 1.05f, z - 0.08f, x + 0.08f, 1.3f, z + 0.08f);
            S.Light(x, 1.5f, z, 0, 9.5f, 1.0f);
        }

        private static void PTires(float x, float z, int n)
        {
            for (int i = 0; i < n; i++) S.Cyl(RcMat.Black, x + (i % 2) * 0.04f, i * 0.24f, z, 0.45f, 0.22f, false);
            Keep(x - 0.5f, z - 0.5f, x + 0.5f, z + 0.5f);
            B(RcMat.None, x - 0.4f, z - 0.4f, x + 0.4f, z + 0.4f, 0f, n * 0.24f, true);
        }

        private static void PCart(float x, float z, bool alongX)
        {
            float a = alongX ? 1f : 0f, b = alongX ? 0f : 1f;
            Vis(RcMat.Metal, x - 0.45f * a - 0.3f * b, 0.5f, z - 0.3f * a - 0.45f * b, x + 0.45f * a + 0.3f * b, 0.56f, z + 0.3f * a + 0.45f * b);
            Vis(RcMat.Metal, x - 0.45f * a - 0.3f * b, 0.56f, z - 0.3f * a - 0.45f * b, x + 0.45f * a + 0.3f * b, 0.95f, z + 0.3f * a + 0.45f * b);
            Vis(RcMat.Black, x - 0.4f * a - 0.28f * b, 0.05f, z - 0.28f * a - 0.4f * b, x - 0.3f * a - 0.18f * b, 0.2f, z - 0.18f * a - 0.3f * b);
            Vis(RcMat.Black, x + 0.3f * a + 0.18f * b, 0.05f, z + 0.18f * a + 0.3f * b, x + 0.4f * a + 0.28f * b, 0.2f, z + 0.28f * a + 0.4f * b);
            Vis(RcMat.Metal, x + 0.45f * a - 0.28f * b, 0.9f, z - 0.28f * a + 0.45f * b, x + 0.55f * a + 0.28f * b, 1.05f, z + 0.28f * a + 0.55f * b);
        }

        private static void PRock(float x, float z, float s)
        {
            S.Ball(RcMat.Stone, x, 0.2f * s, z, 0.55f * s, 0.38f * s, 0.45f * s);
            S.Ball(RcMat.Stone, x + 0.5f * s, 0.12f * s, z + 0.2f * s, 0.3f * s, 0.22f * s, 0.28f * s);
        }

        private static void PPine(float x, float z, float s)
        {
            S.Cyl(RcMat.Bark, x, 0f, z, 0.28f * s, 5.2f * s, true);
            for (int i = 0; i < 5; i++)
            {
                float r = (2.3f - i * 0.4f) * s;
                S.Ball(RcMat.Leaf, x, (1.8f + i * 1.15f) * s, z, r, 0.75f * s, r);
            }
        }

        private static void PFlowers(float x, float z, RcMat bloom)
        {
            for (int i = 0; i < 6; i++)
            {
                float ox = (Vary(x, z, i) - 0.5f) * 1.4f, oz = (Vary(x, z, i + 5) - 0.5f) * 1.0f;
                S.Cyl(RcMat.Leaf, x + ox, 0f, z + oz, 0.02f, 0.3f, false);
                S.Ball(bloom, x + ox, 0.34f, z + oz, 0.07f, 0.07f, 0.07f);
            }
            S.Ball(RcMat.Leaf, x, 0.12f, z, 0.7f, 0.14f, 0.5f);
        }

        private static void PBench(float x, float z, bool alongX)
        {
            if (alongX)
            {
                Vis(RcMat.Wood, x - 0.85f, 0.38f, z - 0.25f, x + 0.85f, 0.46f, z + 0.25f);
                Vis(RcMat.Wood, x - 0.85f, 0.46f, z + 0.2f, x + 0.85f, 0.95f, z + 0.28f);
                Vis(RcMat.Metal, x - 0.75f, 0f, z - 0.22f, x - 0.68f, 0.38f, z + 0.22f);
                Vis(RcMat.Metal, x + 0.68f, 0f, z - 0.22f, x + 0.75f, 0.38f, z + 0.22f);
            }
            else
            {
                Vis(RcMat.Wood, x - 0.25f, 0.38f, z - 0.85f, x + 0.25f, 0.46f, z + 0.85f);
                Vis(RcMat.Wood, x + 0.2f, 0.46f, z - 0.85f, x + 0.28f, 0.95f, z + 0.85f);
                Vis(RcMat.Metal, x - 0.22f, 0f, z - 0.75f, x + 0.22f, 0.38f, z - 0.68f);
                Vis(RcMat.Metal, x - 0.22f, 0f, z + 0.68f, x + 0.22f, 0.38f, z + 0.75f);
            }
        }

        private static void PPicnic(float x, float z)
        {
            Vis(RcMat.Plank, x - 1.0f, 0.74f, z - 0.45f, x + 1.0f, 0.8f, z + 0.45f);
            Vis(RcMat.Plank, x - 1.0f, 0.42f, z - 0.95f, x + 1.0f, 0.47f, z - 0.55f);
            Vis(RcMat.Plank, x - 1.0f, 0.42f, z + 0.55f, x + 1.0f, 0.47f, z + 0.95f);
            Vis(RcMat.Wood, x - 0.8f, 0f, z - 0.4f, x - 0.7f, 0.74f, z + 0.4f);
            Vis(RcMat.Wood, x + 0.7f, 0f, z - 0.4f, x + 0.8f, 0.74f, z + 0.4f);
            B(RcMat.None, x - 1f, z - 0.95f, x + 1f, z + 0.95f, 0f, 0.8f, true);
        }

        private static void PPlanter(float x, float z, float w, float d, RcMat bloom)
        {
            B(RcMat.Stone, x - w * 0.5f, z - d * 0.5f, x + w * 0.5f, z + d * 0.5f, 0f, 0.5f, true);
            S.Ball(RcMat.Leaf, x, 0.55f, z, w * 0.45f, 0.3f, d * 0.45f);
            for (int i = 0; i < 5; i++) S.Ball(bloom, x + (Vary(x, z, i) - 0.5f) * w * 0.7f, 0.82f, z + (Vary(x, z, i + 4) - 0.5f) * d * 0.6f, 0.07f, 0.07f, 0.07f);
        }

        private static void PSwings(float x, float z)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                Vis(RcMat.Metal, x + s * 1.6f - 0.05f, 0f, z - 0.55f, x + s * 1.6f + 0.05f, 2.4f, z - 0.45f);
                Vis(RcMat.Metal, x + s * 1.6f - 0.05f, 0f, z + 0.45f, x + s * 1.6f + 0.05f, 2.4f, z + 0.55f);
            }
            Vis(RcMat.Metal, x - 1.7f, 2.35f, z - 0.05f, x + 1.7f, 2.45f, z + 0.05f);
            foreach (float s in new[] { -0.6f, 0.6f })
            {
                Vis(RcMat.Metal, x + s - 0.01f, 0.5f, z - 0.01f, x + s + 0.01f, 2.35f, z + 0.01f);
                Vis(RcMat.Plank, x + s - 0.25f, 0.45f, z - 0.1f, x + s + 0.25f, 0.5f, z + 0.1f);
            }
        }

        private static void PTrampoline(float x, float z)
        {
            S.Cyl(RcMat.Black, x, 0.8f, z, 1.5f, 0.1f, false);
            S.Cyl(RcMat.CarB, x, 0.9f, z, 1.35f, 0.03f, false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.5708f + 0.7f;
                Vis(RcMat.Metal, x + (float)System.Math.Cos(a) * 1.3f - 0.04f, 0f, z + (float)System.Math.Sin(a) * 1.3f - 0.04f, x + (float)System.Math.Cos(a) * 1.3f + 0.04f, 0.8f, z + (float)System.Math.Sin(a) * 1.3f + 0.04f);
            }
        }

        private static void PClothesline(float x, float z)
        {
            Vis(RcMat.Metal, x - 2.2f, 0f, z - 0.03f, x - 2.14f, 2.0f, z + 0.03f);
            Vis(RcMat.Metal, x + 2.14f, 0f, z - 0.03f, x + 2.2f, 2.0f, z + 0.03f);
            Vis(RcMat.Black, x - 2.2f, 1.95f, z - 0.005f, x + 2.2f, 1.97f, z + 0.005f);
            RcMat[] cloth = { RcMat.Pink, RcMat.CarB, RcMat.Paper, RcMat.Orange, RcMat.CarA };
            for (int i = 0; i < 5; i++) Vis(cloth[i], x - 1.9f + i * 0.85f, 1.2f, z - 0.02f, x - 1.45f + i * 0.85f, 1.95f, z + 0.02f);
        }

        private static void PShed(float x, float z)
        {
            B(RcMat.Plank, x - 1.3f, z - 1.1f, x + 1.3f, z + 1.1f, 0f, 2.1f, true);
            B(RcMat.Roof, x - 1.5f, z - 1.3f, x + 1.5f, z + 1.3f, 2.1f, 2.25f, false);
            B(RcMat.Roof, x - 1.2f, z - 1.0f, x + 1.2f, z + 1.0f, 2.25f, 2.5f, false);
            Vis(RcMat.Door, x - 0.4f, 0f, z - 1.14f, x + 0.4f, 1.8f, z - 1.1f);
            Vis(RcMat.Metal, x + 0.25f, 0.9f, z - 1.17f, x + 0.33f, 0.98f, z - 1.14f);
        }

        /// <summary>A Waygetter delivery truck: white box, red stripe, black lettering, dark windscreen. Solid.</summary>
        private static void PTruck(float x, float z, bool alongX)
        {
            float L = 3.4f, W = 1.35f;
            float x0 = alongX ? x - L : x - W, x1 = alongX ? x + L : x + W, z0 = alongX ? z - W : z - L, z1 = alongX ? z + W : z + L;
            // The box and the cab (the cab at the +end).
            float cab = 2.2f;
            if (alongX)
            {
                B(RcMat.White, x0, z0, x1 - cab, z1, 0.45f, 3.0f, true);
                B(RcMat.White, x1 - cab, z0 + 0.1f, x1, z1 - 0.1f, 0.45f, 2.2f, true);
                B(RcMat.Glass, x1 - 0.7f, z0 + 0.12f, x1 - 0.02f, z1 - 0.12f, 1.35f, 2.05f, false);
                B(RcMat.CarC, x0, z0 - 0.02f, x1 - cab, z1 + 0.02f, 1.1f, 1.45f, false);
                B(RcMat.Black, x0 + 0.5f, z0 - 0.04f, x0 + 3.4f, z0 + 0.0f, 1.2f, 1.35f, false);
                B(RcMat.Black, x0 + 0.5f, z1, x0 + 3.4f, z1 + 0.04f, 1.2f, 1.35f, false);
                B(RcMat.Metal, x0 - 0.05f, z0 + 0.2f, x0, z1 - 0.2f, 0.6f, 2.8f, false);
                B(RcMat.Black, x0 + 0.2f, z0 - 0.02f, x1 - 0.2f, z1 + 0.02f, 0.18f, 0.5f, false);
                foreach (float wx in new[] { x0 + 1.1f, x0 + 2.2f, x1 - 0.9f })
                    foreach (float wz in new[] { z0 - 0.02f, z1 - 0.2f }) B(RcMat.Black, wx - 0.4f, wz, wx + 0.4f, wz + 0.22f, 0f, 0.8f, false);
                B(RcMat.Glow, x1, z0 + 0.25f, x1 + 0.04f, z0 + 0.5f, 0.7f, 0.9f, false);
                B(RcMat.Glow, x1, z1 - 0.5f, x1 + 0.04f, z1 - 0.25f, 0.7f, 0.9f, false);
            }
            else
            {
                B(RcMat.White, x0, z0, x1, z1 - cab, 0.45f, 3.0f, true);
                B(RcMat.White, x0 + 0.1f, z1 - cab, x1 - 0.1f, z1, 0.45f, 2.2f, true);
                B(RcMat.Glass, x0 + 0.12f, z1 - 0.7f, x1 - 0.12f, z1 - 0.02f, 1.35f, 2.05f, false);
                B(RcMat.CarC, x0 - 0.02f, z0, x1 + 0.02f, z1 - cab, 1.1f, 1.45f, false);
                B(RcMat.Black, x0 - 0.04f, z0 + 0.5f, x0, z0 + 3.4f, 1.2f, 1.35f, false);
                B(RcMat.Black, x1, z0 + 0.5f, x1 + 0.04f, z0 + 3.4f, 1.2f, 1.35f, false);
                B(RcMat.Metal, x0 + 0.2f, z0 - 0.05f, x1 - 0.2f, z0, 0.6f, 2.8f, false);
                B(RcMat.Black, x0 - 0.02f, z0 + 0.2f, x1 + 0.02f, z1 - 0.2f, 0.18f, 0.5f, false);
                foreach (float wz in new[] { z0 + 1.1f, z0 + 2.2f, z1 - 0.9f })
                    foreach (float wx in new[] { x0 - 0.02f, x1 - 0.2f }) B(RcMat.Black, wx, wz - 0.4f, wx + 0.22f, wz + 0.4f, 0f, 0.8f, false);
                B(RcMat.Glow, x0 + 0.25f, z1, x0 + 0.5f, z1 + 0.04f, 0.7f, 0.9f, false);
                B(RcMat.Glow, x1 - 0.5f, z1, x1 - 0.25f, z1 + 0.04f, 0.7f, 0.9f, false);
            }
        }

        /// <summary>A truck, if there's room for it.</summary>
        private static void PTruckAt(float x, float z, bool alongX)
        {
            float rx = alongX ? 4f : 2f, rz = alongX ? 2f : 4f;
            if (!Room(x, z, 0.1f, false) || !Room(x + rx, z + rz, 0.1f, false) || !Room(x - rx, z - rz, 0.1f, false)) return;
            PTruck(x, z, alongX);
            Keep(x - rx, z - rz, x + rx, z + rz);
        }

        private static void PContainer(float x, float z, bool alongX, RcMat paint)
        {
            float lx = alongX ? 3.0f : 1.25f, lz = alongX ? 1.25f : 3.0f;
            B(paint, x - lx, z - lz, x + lx, z + lz, 0.1f, 2.6f, true);
            for (int i = -2; i <= 2; i++)
            {
                if (alongX) B(RcMat.Metal, x + i * 1.1f - 0.05f, z - lz - 0.04f, x + i * 1.1f + 0.05f, z + lz + 0.04f, 0.1f, 2.6f, false);
                else B(RcMat.Metal, x - lx - 0.04f, z + i * 1.1f - 0.05f, x + lx + 0.04f, z + i * 1.1f + 0.05f, 0.1f, 2.6f, false);
            }
        }

        private static void PBus(float x, float z, bool alongX)
        {
            float lx = alongX ? 5.2f : 1.4f, lz = alongX ? 1.4f : 5.2f;
            B(RcMat.CarA, x - lx, z - lz, x + lx, z + lz, 0.5f, 2.9f, true);
            B(RcMat.Black, x - lx - 0.02f, z - lz - 0.02f, x + lx + 0.02f, z + lz + 0.02f, 1.15f, 1.3f, false);
            B(RcMat.Black, x - lx - 0.02f, z - lz - 0.02f, x + lx + 0.02f, z + lz + 0.02f, 0.5f, 0.62f, false);
            B(RcMat.Glass, x - lx * 0.92f, z - lz - 0.03f, x + lx * 0.92f, z + lz + 0.03f, 1.6f, 2.3f, false);
            for (int i = -3; i <= 3; i++)
            {
                if (alongX) B(RcMat.CarA, x + i * 1.35f - 0.06f, z - lz - 0.05f, x + i * 1.35f + 0.06f, z + lz + 0.05f, 1.6f, 2.3f, false);
                else B(RcMat.CarA, x - lx - 0.05f, z + i * 1.35f - 0.06f, x + lx + 0.05f, z + i * 1.35f + 0.06f, 1.6f, 2.3f, false);
            }
            B(RcMat.Black, x - lx, z - lz, x + lx, z + lz, 0f, 0.5f, false);
            B(RcMat.RedGlow, x + (alongX ? lx : lx * 0.7f) - 0.05f, z + (alongX ? lz * 0.6f : lz) - 0.05f, x + (alongX ? lx + 0.04f : lx * 0.7f + 0.2f), z + (alongX ? lz * 0.6f + 0.2f : lz + 0.04f), 2.4f, 2.6f, false);
        }

        private static void PFridge(float x, float z)
        {
            B(RcMat.White, x - 0.4f, z - 0.38f, x + 0.4f, z + 0.38f, 0f, 1.75f, true);
            Vis(RcMat.Metal, x + 0.32f, 1.0f, z + 0.38f, x + 0.37f, 1.5f, z + 0.42f);
            Vis(RcMat.Black, x - 0.4f, 1.15f, z + 0.38f, x + 0.4f, 1.18f, z + 0.4f);
        }

        private static void PHeap(float x, float z, float s)
        {
            B(RcMat.None, x - 1.1f * s, z - 1.1f * s, x + 1.1f * s, z + 1.1f * s, 0f, 1.4f * s, true);
            S.Ball(RcMat.Rust, x, 0.5f * s, z, 1.3f * s, 0.9f * s, 1.2f * s);
            S.Ball(RcMat.Metal, x + 0.6f * s, 0.9f * s, z - 0.3f * s, 0.7f * s, 0.55f * s, 0.7f * s);
            S.Cyl(RcMat.Black, x - 0.5f * s, 1.15f * s, z + 0.4f * s, 0.4f * s, 0.2f * s, false);
            Vis(RcMat.Metal, x - 0.2f * s, 1.0f * s, z - 0.1f * s, x + 0.9f * s, 1.12f * s, z + 0.05f * s);
        }

        private static void PTarp(float x, float z, RcMat m)
        {
            Vis(m, x - 1.0f, 0f, z - 0.7f, x + 1.0f, 0.18f, z + 0.7f);
            Vis(m, x - 0.6f, 0.18f, z - 0.4f, x + 0.5f, 0.38f, z + 0.35f);
            Vis(RcMat.Rust, x + 0.9f, 0f, z + 0.5f, x + 1.1f, 0.5f, z + 0.7f);
        }

        /// <summary>Bulbs on a string between two points (one axis): they hang in a shallow curve; some give light.</summary>
        private static void PStrung(float x0, float z0, float x1, float z1, float y, int lights)
        {
            float len = (float)System.Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            int n = (int)(len / 0.75f);
            for (int i = 0; i <= n; i++)
            {
                float t = n > 0 ? i / (float)n : 0f, sag = 0.35f * 4f * t * (1f - t);
                float bx = x0 + (x1 - x0) * t, bz = z0 + (z1 - z0) * t, by = y - sag;
                RcMat m = (i + (int)(x0 + z0)) % 3 == 0 ? RcMat.Neon : RcMat.Glow;
                Vis(m, bx - 0.05f, by - 0.05f, bz - 0.05f, bx + 0.05f, by + 0.05f, bz + 0.05f);
            }
            for (int i = 1; i <= lights; i++)
            {
                float t = i / (float)(lights + 1);
                S.Light(x0 + (x1 - x0) * t, y - 0.4f, z0 + (z1 - z0) * t, 0, 6.5f, 0.6f);
            }
        }

        /// <summary>Little flags on a string (bunting).</summary>
        private static void PBunting(float x0, float z0, float x1, float z1, float y)
        {
            float len = (float)System.Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            int n = (int)(len / 0.55f);
            RcMat[] flag = { RcMat.CarC, RcMat.CarA, RcMat.CarB, RcMat.Pink, RcMat.Paper, RcMat.Orange };
            for (int i = 1; i < n; i++)
            {
                float t = i / (float)n, sag = 0.3f * 4f * t * (1f - t);
                float bx = x0 + (x1 - x0) * t, bz = z0 + (z1 - z0) * t, by = y - sag;
                Vis(flag[i % flag.Length], bx - 0.11f, by - 0.3f, bz - 0.11f, bx + 0.11f, by, bz + 0.11f);
            }
        }

        // ── Streets: what's at the kerb and on the road ──────────────────────
        /// <summary>
        /// Dresses a street's edges and surface. alongX: it runs east-west at z = c (else north-south at x = c), from a0 to a1,
        /// hw is half its road width. Lamps sit at hw + 1.1, so the kerb props stand just outside them.
        /// </summary>
        private static void StreetDress(bool alongX, float c, float a0, float a1, float hw)
        {
            for (float a = a0 + 6f; a < a1 - 4f; a += 9f + Rnd() * 8f)
            {
                int side = Rnd(2) == 0 ? -1 : 1;
                float off = side * (hw + 1.75f);
                float x = alongX ? a : c + off, z = alongX ? c + off : a;
                int pick = Rnd(12);
                if (!Room(x, z, 0.7f, true) && pick < 8) pick = 11;
                switch (pick)
                {
                    case 0: case 1: PTrash(x, z); break;
                    case 2: PHydrant(x, z); break;
                    case 3: PMailbox(x, z); break;
                    case 4: PBench(x, z, alongX); break;
                    case 5: PCone(x, z); PCone(x + (alongX ? 0.9f : 0.1f), z + (alongX ? 0.1f : 0.9f)); break;
                    case 6: PLitter(x, z); break;
                    default: break;
                }
                // The road itself: puddles, a manhole, litter, a poster on the lamp.
                float rx = alongX ? a + Rnd() * 6f : c + (Rnd() - 0.5f) * hw * 1.2f, rz = alongX ? c + (Rnd() - 0.5f) * hw * 1.2f : a + Rnd() * 6f;
                int r = Rnd(7);
                if (r == 0) PManhole(rx, rz);
                else if (r == 1 || r == 2) PPuddle(rx, rz, 1.6f + Rnd() * 1.6f, 1.0f + Rnd() * 1.0f, true);
                else if (r == 3) PLitter(rx, rz);
            }
        }

        // ── The core: streets, the park, the treehouse ───────────────────────
        private static void DressCore()
        {
            StreetDress(false, 0f, -212f, -50f, 4.5f);   // Main Street, south
            StreetDress(false, 0f, 50f, 108f, 4.5f);     // Main Street, north
            StreetDress(true, 0f, -112f, -50f, 4f);      // west
            StreetDress(true, 0f, 50f, 126f, 4f);        // east
            StreetDress(true, 80f, 8f, 126f, 4f);        // north-east
            StreetDress(true, -198f, 8f, 198f, 4f);      // the road out
            // Waygetter's trucks, left where they stopped.
            PTruckAt(-3f, -62f, false); PTruckAt(3f, 66f, false); PTruckAt(-84f, -2.5f, true); PTruckAt(96f, 2.5f, true);
            // Barricades at the end of the road out.
            for (float z = -202f; z <= -194f; z += 2.7f) { PCone(55.8f, z + 1f); PCone(64.5f, z + 1f); }

            // The park.
            Scatter(-44f, -44f, 44f, 44f, 120, 2.2f, true, (x, z) =>
            {
                if (System.Math.Abs(x) < 6f || System.Math.Abs(z) < 6f) return;     // (the paths)
                int p = Rnd(10);
                if (p < 3) PBush(x, z, 0.8f + Rnd() * 0.6f);
                else if (p < 5) PFlowers(x, z, Rnd(2) == 0 ? RcMat.Pink : RcMat.Paper);
                else if (p < 6) PRock(x, z, 0.8f + Rnd() * 0.7f);
                else if (p < 7) PPine(x, z, 0.9f + Rnd() * 0.5f);
                else if (p < 8) { PBall(x, z, RcMat.CarB); PBlocks(x + 1f, z + 0.5f); }
                else if (p < 9) PTricycle(x, z);
                else PTeddy(x, z);
            });
            PPicnic(-12f, 18f); PPicnic(14f, -16f); PPicnic(-30f, 30f);
            // Lamps along the paths.
            for (int i = 0; i < 4; i++) { float z = -34f + i * 20f; Lamp(2.7f, z, -1f, 0f, true); Lamp(-2.7f, z + 10f, 1f, 0f, true); }
            // Night flowers: they glow, a little.
            for (int i = 0; i < 14; i++)
            {
                float x = -40f + Rnd() * 80f, z = -40f + Rnd() * 80f;
                if (System.Math.Abs(x) < 4f || System.Math.Abs(z) < 4f || !Room(x, z, 1f, true)) continue;
                for (int k = 0; k < 4; k++)
                {
                    float ox = (Rnd() - 0.5f) * 1.2f, oz = (Rnd() - 0.5f) * 1.2f;
                    S.Cyl(RcMat.Leaf, x + ox, 0f, z + oz, 0.015f, 0.35f, false);
                    S.Ball(i % 2 == 0 ? RcMat.BlueGlow : RcMat.Neon, x + ox, 0.38f, z + oz, 0.05f, 0.05f, 0.05f);
                }
            }

            // The treehouse: strings of lights and bunting round every deck.
            float[] half = { DECK_HALF0, DECK_HALF1, DECK_HALF2 };
            for (int d = 0; d < 3; d++)
            {
                float y = DeckY[d] + 1.55f, h = half[d] - 0.1f;
                PStrung(-h, -h, h, -h, y, 1); PStrung(h, -h, h, h, y, 1); PStrung(h, h, -h, h, y, 1); PStrung(-h, h, -h, -h, y, 1);
                PBunting(-h, h, h, h, y - 0.25f);
            }
            // Lit string out to the trees and a sleeping place under the bottom deck.
            PStrung(-7f, -7f, -9f, -9f, 5.2f, 0);
            Vis(RcMat.Fabric, -5.2f, 0f, 6.0f, -2.8f, 0.2f, 8.0f);
            Vis(RcMat.Carpet, -5.0f, 0.2f, 6.2f, -3.0f, 0.5f, 7.0f);
            PTeddy(-3.6f, 7.4f);
        }

        // ── The suburbs ──────────────────────────────────────────────────────
        private static void DressSuburbs()
        {
            // Backyards: behind each house in row A (z -106..-90), a swing set, a trampoline, a shed, a line of washing.
            for (int i = 0; i < 8; i++)
            {
                float x = -40f - i * 22f;
                int pick = (i * 5 + 2) % 6;
                if (pick == 0) PSwings(x, -98f);
                else if (pick == 1) PTrampoline(x, -98f);
                else if (pick == 2) PShed(x + 4f, -94f);
                else if (pick == 3) PClothesline(x, -97f);
                else if (pick == 4) { S.Cyl(RcMat.Pink, x, 0.05f, -98f, 1.1f, 0.35f, false); S.Cyl(RcMat.Glass, x, 0.35f, -98f, 0.95f, 0.02f, false); }
                else PBench(x, -96f, true);
            }
            // Front lawns and kerbs, all along Maple Street and Oak Lane: what the kids dropped when the truck came.
            Scatter(-196f, -125f, -30f, -119f, 70, 1.6f, true, (x, z) => ToyOnLawn(x, z));
            Scatter(-196f, -142f, -30f, -136f, 70, 1.6f, true, (x, z) => ToyOnLawn(x, z));
            Scatter(-180f, -176f, -40f, -171f, 40, 1.6f, true, (x, z) => ToyOnLawn(x, z));
            Scatter(-196f, -192f, -40f, -187f, 40, 1.6f, true, (x, z) => ToyOnLawn(x, z));
            StreetDress(true, -130f, -200f, -20f, 4f);   // Maple Street
            StreetDress(true, -180f, -166f, -42f, 4f);   // Oak Lane
            StreetDress(false, -120f, -176f, -134f, 4f); // the lane between them
            // Bushes and fences along the lots.
            Scatter(-200f, -118f, -30f, -88f, 90, 1.4f, true, (x, z) => PBush(x, z, 0.8f + Rnd() * 0.5f));
            // Trucks, and a car with its door open, left on the street.
            PTruckAt(-110f, -131.5f, true); PTruckAt(-72f, -178.5f, true);
        }

        /// <summary>A toy, a parcel with a Tattletail on it, or a heap of blocks: on the lawn in front of a house.</summary>
        private static void ToyOnLawn(float x, float z)
        {
            int p = Rnd(10);
            if (p < 2) PTricycle(x, z);
            else if (p < 4) PBall(x, z, Rnd(2) == 0 ? RcMat.CarA : RcMat.CarC);
            else if (p < 5) PBlocks(x, z);
            else if (p < 6) PTeddy(x, z);
            else if (p < 8) PParcel(x, z, Rnd(3) == 0 ? RcMat.CarA : Rnd(2) == 0 ? RcMat.Fabric : RcMat.Teal);
            else if (p < 9) PPoster(x, 0.9f, z, false);
            else PMailbox(x, z);
        }

        // ── The mall's lot ───────────────────────────────────────────────────
        private static void DressMall()
        {
            // The lot is the strip west of the building (x 130..188), the road along it, and the dock.
            Scatter(131f, -140f, 188f, -2f, 60, 2.0f, false, (x, z) => PCart(x, z, Rnd(2) == 0));
            Scatter(131f, -140f, 188f, -2f, 22, 1.4f, false, (x, z) => PCone(x, z));
            Scatter(131f, -140f, 188f, -2f, 14, 1.5f, false, (x, z) => PPuddle(x, z, 2f + Rnd() * 2f, 1.4f + Rnd(), true));
            Scatter(131f, -140f, 188f, -2f, 16, 1.2f, false, (x, z) => PLitter(x, z));
            Scatter(131f, -140f, 188f, -2f, 8, 1.8f, false, (x, z) => PTrash(x, z));
            PTruckAt(137f, -60f, false); PTruckAt(137f, -116f, false);
            // Planters and benches by the doors, and abandoned tables outside.
            for (int i = 0; i < 5; i++)
            {
                float z = -130f + i * 28f;
                if (Room(191f, z, 1.5f, false)) PPlanter(191f, z, 1.6f, 1.6f, i % 2 == 0 ? RcMat.Pink : RcMat.Orange);
            }
            // Lamps in the lot (some dead).
            for (int i = 0; i < 5; i++) Lamp(158f, -128f + i * 28f, i % 2 == 0 ? 1f : -1f, 0f, i != 2);
        }

        // ── The school ───────────────────────────────────────────────────────
        private static void DressSchool()
        {
            // The yard: bikes and bags where the kids left them, a basketball, chalk on the ground, benches along the wall.
            Scatter(-154f, -58f, -117f, 74f, 40, 1.6f, false, (x, z) => { if (Rnd(2) == 0) PBall(x, z, RcMat.Orange); else PBlocks(x, z); });
            Scatter(-154f, -58f, -117f, 74f, 30, 1.8f, false, (x, z) => PCart(x, z, Rnd(2) == 0));
            Scatter(-154f, -58f, -117f, 74f, 20, 1.2f, false, (x, z) => PLitter(x, z));
            Scatter(-154f, -58f, -117f, 74f, 18, 1.4f, false, (x, z) => PPuddle(x, z, 2f, 1.5f, true));
            Scatter(-154f, -58f, -117f, 74f, 10, 1.6f, false, (x, z) => PBench(x, z, false));
            PBus(-141f, 12f, false);
            // Chalk hopscotch.
            for (int i = 0; i < 6; i++) Vis(i % 2 == 0 ? RcMat.Paper : RcMat.Pink, -132f + (i % 2) * 0.9f, 0.05f, -8f - i * 0.9f, -131.2f + (i % 2) * 0.9f, 0.056f, -7.2f - i * 0.9f);
            // Lights over the doors.
            Lamp(-153f, -50f, 1f, 0f, true);
            Lamp(-153f, 62f, 1f, 0f, false);
        }

        // ── The junkyard ─────────────────────────────────────────────────────
        private static void DressJunkyard()
        {
            float x0 = 132f, x1 = 262f, z0 = 45f, z1 = 172f;
            Scatter(x0, z0, x1, z1, 160, 2.4f, false, (x, z) => PTires(x, z, 2 + Rnd(4)));
            Scatter(x0, z0, x1, z1, 120, 1.8f, false, (x, z) => PBarrel(x, z, Rnd(3) == 0 ? RcMat.CarC : RcMat.Rust, true));
            Scatter(x0, z0, x1, z1, 40, 2.4f, false, (x, z) => PHeap(x, z, 0.8f + Rnd() * 1.1f));
            Scatter(x0, z0, x1, z1, 24, 1.6f, false, (x, z) => PFridge(x, z));
            Scatter(x0, z0, x1, z1, 24, 2.4f, false, (x, z) => PTarp(x, z, Rnd(2) == 0 ? RcMat.Teal : RcMat.CarB));
            Scatter(x0, z0, x1, z1, 14, 2.6f, false, (x, z) => PContainer(x, z, Rnd(2) == 0, Rnd(3) == 0 ? RcMat.CarB : Rnd(2) == 0 ? RcMat.CarC : RcMat.Rust));
            Scatter(x0, z0, x1, z1, 10, 2.2f, false, (x, z) => PFireBarrel(x, z));
            Scatter(x0, z0, x1, z1, 40, 1.4f, false, (x, z) => PLitter(x, z));
            Scatter(x0, z0, x1, z1, 24, 1.2f, false, (x, z) => PCone(x, z));
            Scatter(x0, z0, x1, z1, 14, 1.2f, false, (x, z) => PTeddy(x, z));          // (it keeps toys: look who's here)
            Scatter(x0, z0, x1, z1, 18, 1.6f, false, (x, z) => PPuddle(x, z, 2.4f, 1.6f, false));
            PTruckAt(135f, 100f, false); PTruckAt(135f, 140f, false);
            // Lamps on poles round the yard, some dead.
            for (int i = 0; i < 6; i++) Lamp(133f, 52f + i * 22f, 1f, 0f, i % 3 != 1);
        }

        // ── The Facility's lot ───────────────────────────────────────────────
        private static void DressWarehouse()
        {
            // (The lot and the building are the game's own factory: this is just the strip outside the gate.)
            PCone(-8f, 112f); PCone(8f, 112f); PCone(-7f, 113.2f); PCone(7f, 113.2f);
            PPoster(-9.5f, 0.9f, 111.2f, false);
            PPuddle(0f, 111.5f, 2.6f, 1.2f, true);
        }


        private static void Dress(int part)
        {
            switch (part)
            {
                case PART_CORE: DressCore(); break;
                case 0: DressSuburbs(); break;
                case 1: DressMall(); break;
                case 2: DressSchool(); break;
                case 3: DressJunkyard(); break;
                case 4: DressWarehouse(); break;
            }
        }
    }
}
