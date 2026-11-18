using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Everything a custom kid is: body frame and bottom, head shape, hair and its color, skin
    /// tone, eyes and their color, brows, nose, mouth, slider tweaks, and the extras (expression,
    /// glasses, freckles, band-aid, eye circles, Tattletail-colored accents). Small enough to
    /// travel with every pose packet (as text), so your friend always sees your exact kid.
    /// </summary>
    public class CharacterSpec
    {
        public byte Frame = 1, Bottom, Head, Hair = 1, HairColor = 2, Skin = 2, Eyes, EyeColor, Brows = 1, Nose = 1, Mouth;
        // Sliders, -100..100.
        public sbyte EyeSpacing, EyeHeight, EyeSize, BrowHeight, BrowAngle, MouthHeight, MouthSize, NoseSize;
        // Extras. AccentColor is filled in from your Tattletail's color when the kid is sent or built.
        public byte Expression, Glasses, Freckles, BandAid, EyeCircles, Accent, AccentColor;
        // Voice (KidVoice.Types) and its pitch, -100..100 — any voice on any body.
        public byte Voice;
        public sbyte VoicePitch;

        public static readonly string[] Expressions = { "Neutral", "Happy", "Worried", "Angry", "Sad", "Terrified" };
        // Each expression on top of the sliders: brow angle, brow height, eye size, and a mouth (-1 keeps yours).
        private static readonly int[,] ExpressionTweaks =
        {
            { 0, 0, 0, -1 }, { -12, 30, 5, -1 }, { -45, 25, 10, -1 }, { 50, -40, -10, -1 }, { -40, -5, -5, 2 }, { -55, 50, 40, 4 },
        };
        public const int TERRIFIED = 5; // what every custom kid's face does when Mama gets them (or her Gaze does)
        public static readonly string[] GlassesStyles = { "None", "Round", "Square", "Shades" };
        public static readonly string[] FreckleAmounts = { "None", "Some", "Lots" };
        public static readonly string[] BandAids = { "None", "Cheek", "Nose", "Chin", "Cross" };
        public static readonly string[] EyeCircleStyles = { "None", "Tired", "Exhausted" };
        public static readonly string[] Accents = { "Outfit", "Match Tattletail" };
        // Same order as the Tattletail colors (CustomizationSubmenu).
        public static readonly string[] FurNames = { "Original", "Yellow", "Teal", "Black", "White", "Gold", "Diamond" };
        public static readonly Color[] FurColors =
        {
            new Color(0.55f, 0.33f, 0.78f), new Color(0.95f, 0.8f, 0.25f), new Color(0.2f, 0.68f, 0.7f), new Color(0.12f, 0.12f, 0.14f),
            new Color(0.93f, 0.93f, 0.95f), new Color(0.95f, 0.72f, 0.22f), new Color(0.72f, 0.9f, 1f),
        };

        public int BrowAngleWith(int expression) { return Tweak(BrowAngle, expression, 0); }
        public int BrowHeightWith(int expression) { return Tweak(BrowHeight, expression, 1); }
        public int EyeSizeWith(int expression) { return Tweak(EyeSize, expression, 2); }
        public int MouthWith(int expression) { int m = ExpressionTweaks[expression, 3]; return m >= 0 ? m : Mouth; }
        private static int Tweak(sbyte v, int expression, int k) { return Mathf.Clamp(v + ExpressionTweaks[expression, k], -100, 100); }

        public static readonly string[] Frames = { "Slim", "Average", "Sturdy" };
        public static readonly string[] Bottoms = { "Pants", "Skirt" };
        public static readonly string[] Heads = { "Round", "Oval", "Wide" };
        public static readonly string[] Hairs = { "Bald", "Messy", "Bob", "Buzz", "Long", "Pigtails", "Spiky", "Ponytail" };
        public static readonly string[] EyeStyles = { "Round", "Big", "Sleepy", "Narrow", "Dot" };
        public static readonly string[] BrowStyles = { "None", "Thin", "Thick", "Bushy" };
        public static readonly string[] Noses = { "None", "Button", "Small", "Pointy" };
        public static readonly string[] Mouths = { "Smile", "Grin", "Frown", "Flat", "Gasp", "Cat" };

        public static readonly string[] SkinNames = { "Porcelain", "Fair", "Peach", "Tan", "Olive", "Brown", "Dark", "Deep" };
        public static readonly Color[] SkinTones =
        {
            new Color(1f, 0.88f, 0.8f), new Color(0.97f, 0.81f, 0.7f), new Color(0.96f, 0.76f, 0.62f), new Color(0.87f, 0.65f, 0.48f),
            new Color(0.78f, 0.6f, 0.42f), new Color(0.6f, 0.42f, 0.3f), new Color(0.43f, 0.29f, 0.2f), new Color(0.3f, 0.2f, 0.15f),
        };
        public static readonly string[] HairColorNames = { "Black", "Dark Brown", "Brown", "Auburn", "Ginger", "Blonde", "Platinum", "Gray", "Blue", "Pink", "Green", "Purple" };
        public static readonly Color[] HairColors =
        {
            new Color(0.07f, 0.06f, 0.06f), new Color(0.2f, 0.12f, 0.07f), new Color(0.32f, 0.19f, 0.09f), new Color(0.45f, 0.18f, 0.1f),
            new Color(0.75f, 0.35f, 0.12f), new Color(0.9f, 0.75f, 0.4f), new Color(0.93f, 0.9f, 0.82f), new Color(0.55f, 0.55f, 0.57f),
            new Color(0.2f, 0.4f, 0.85f), new Color(0.95f, 0.45f, 0.7f), new Color(0.25f, 0.7f, 0.35f), new Color(0.5f, 0.28f, 0.75f),
        };
        public static readonly string[] EyeColorNames = { "Brown", "Blue", "Green", "Hazel", "Gray", "Violet" };
        public static readonly Color[] EyeColors =
        {
            new Color(0.38f, 0.22f, 0.12f), new Color(0.25f, 0.5f, 0.85f), new Color(0.25f, 0.6f, 0.3f),
            new Color(0.55f, 0.45f, 0.2f), new Color(0.5f, 0.55f, 0.6f), new Color(0.55f, 0.3f, 0.8f),
        };

        public static CharacterSpec BoyPreset() { return new CharacterSpec(); }

        public static CharacterSpec GirlPreset()
        {
            return new CharacterSpec { Frame = 0, Bottom = 1, Head = 0, Hair = 2, HairColor = 3, Eyes = 1, EyeColor = 1, Brows = 1, Nose = 2, Mouth = 0, Voice = 1, VoicePitch = 25 };
        }

        public static CharacterSpec Random()
        {
            var r = new System.Random();
            return new CharacterSpec
            {
                Frame = (byte)r.Next(3), Bottom = (byte)r.Next(2), Head = (byte)r.Next(3), Hair = (byte)r.Next(Hairs.Length),
                HairColor = (byte)r.Next(HairColors.Length), Skin = (byte)r.Next(SkinTones.Length), Eyes = (byte)r.Next(EyeStyles.Length),
                EyeColor = (byte)r.Next(EyeColors.Length), Brows = (byte)r.Next(BrowStyles.Length), Nose = (byte)r.Next(Noses.Length),
                Mouth = (byte)r.Next(Mouths.Length),
                EyeSpacing = (sbyte)r.Next(-40, 41), EyeHeight = (sbyte)r.Next(-40, 41), EyeSize = (sbyte)r.Next(-40, 41),
                BrowAngle = (sbyte)r.Next(-50, 51), MouthSize = (sbyte)r.Next(-40, 41),
                // Extras now and then, not on everyone.
                Expression = (byte)(r.Next(3) == 0 ? r.Next(Expressions.Length - 1) : 0),
                Glasses = (byte)(r.Next(4) == 0 ? 1 + r.Next(GlassesStyles.Length - 1) : 0),
                Freckles = (byte)(r.Next(3) == 0 ? 1 + r.Next(2) : 0),
                BandAid = (byte)(r.Next(5) == 0 ? 1 + r.Next(BandAids.Length - 1) : 0),
                EyeCircles = (byte)(r.Next(6) == 0 ? 1 + r.Next(2) : 0),
                Voice = (byte)r.Next(KidVoice.Types.Length - 1), VoicePitch = (sbyte)r.Next(-50, 51), // (Deep is for grown-ups, pick it yourself)
            };
        }

        public CharacterSpec Clone() { return FromString(ToString()); }

        // As text: every field as a byte, then base64. Travels in pose packets and lives in PlayerPrefs.
        public override string ToString()
        {
            var b = new byte[]
            {
                3, Frame, Bottom, Head, Hair, HairColor, Skin, Eyes, EyeColor, Brows, Nose, Mouth,
                (byte)EyeSpacing, (byte)EyeHeight, (byte)EyeSize, (byte)BrowHeight, (byte)BrowAngle, (byte)MouthHeight, (byte)MouthSize, (byte)NoseSize,
                Expression, Glasses, Freckles, BandAid, EyeCircles, Accent, AccentColor, Voice, (byte)VoicePitch,
            };
            return System.Convert.ToBase64String(b);
        }

        public static CharacterSpec FromString(string s)
        {
            var c = new CharacterSpec();
            if (string.IsNullOrEmpty(s)) return c;
            try
            {
                byte[] b = System.Convert.FromBase64String(s);
                if (b.Length < 20 || b[0] < 1 || b[0] > 3) return c; // 1: before the extras, 2: with them, 3: with a voice
                c.Frame = Clamp(b[1], Frames.Length); c.Bottom = Clamp(b[2], Bottoms.Length); c.Head = Clamp(b[3], Heads.Length);
                c.Hair = Clamp(b[4], Hairs.Length); c.HairColor = Clamp(b[5], HairColors.Length); c.Skin = Clamp(b[6], SkinTones.Length);
                c.Eyes = Clamp(b[7], EyeStyles.Length); c.EyeColor = Clamp(b[8], EyeColors.Length); c.Brows = Clamp(b[9], BrowStyles.Length);
                c.Nose = Clamp(b[10], Noses.Length); c.Mouth = Clamp(b[11], Mouths.Length);
                c.EyeSpacing = (sbyte)b[12]; c.EyeHeight = (sbyte)b[13]; c.EyeSize = (sbyte)b[14]; c.BrowHeight = (sbyte)b[15];
                c.BrowAngle = (sbyte)b[16]; c.MouthHeight = (sbyte)b[17]; c.MouthSize = (sbyte)b[18]; c.NoseSize = (sbyte)b[19];
                if (b[0] >= 2 && b.Length >= 27)
                {
                    c.Expression = Clamp(b[20], Expressions.Length); c.Glasses = Clamp(b[21], GlassesStyles.Length); c.Freckles = Clamp(b[22], FreckleAmounts.Length);
                    c.BandAid = Clamp(b[23], BandAids.Length); c.EyeCircles = Clamp(b[24], EyeCircleStyles.Length); c.Accent = Clamp(b[25], Accents.Length);
                    c.AccentColor = Clamp(b[26], FurColors.Length);
                }
                if (b[0] >= 3 && b.Length >= 29) { c.Voice = Clamp(b[27], KidVoice.Types.Length); c.VoicePitch = (sbyte)b[28]; }
            }
            catch (System.Exception) { }
            return c;
        }

        private static byte Clamp(byte v, int count) { return (byte)(v < count ? v : 0); }

        // This player's custom kid.
        private const string PREF = "KE_CustomKid";
        private static string _local, _withFur;
        private static int _fur = -1;

        /// <summary>This player's custom kid, as text, with their Tattletail's current color in it (cached: it's sent with every pose packet).</summary>
        public static string LocalString
        {
            get
            {
                if (_local == null) _local = PlayerPrefs.GetString(PREF, BoyPreset().ToString());
                int fur = CustomizationSubmenu.ColorIndex(VersusMode.IsSurvivor);
                if (_withFur == null || fur != _fur)
                {
                    _fur = fur;
                    var c = FromString(_local);
                    c.AccentColor = (byte)Mathf.Clamp(fur, 0, FurColors.Length - 1);
                    _withFur = c.ToString();
                }
                return _withFur;
            }
        }

        public static CharacterSpec Local
        {
            get { return FromString(LocalString); }
            set { _local = value.ToString(); _withFur = null; PlayerPrefs.SetString(PREF, _local); PlayerPrefs.Save(); }
        }
    }

    /// <summary>
    /// The Character Creator's pieces (tools/creator_kit.py -> assets/creator_kit.bin, embedded):
    /// six bodies skinned to the kid skeleton, three heads, the hairstyles and the face features,
    /// plus a map of each head's face surface. Build() snaps a kid together from a spec.
    /// </summary>
    public static class CharacterKit
    {
        private class KitMesh
        {
            public Mesh Mesh;
            public bool Skinned;
            public string[] Mats;
            public Color[] Colors;
            public float[] Gloss;
        }

        internal class FaceGrid
        {
            public int Nx, Ny;
            public float X0, Y0, Step;
            public float[] Z;

            public float Sample(float x, float y)
            {
                float fx = Mathf.Clamp((x - X0) / Step, 0f, Nx - 1.001f), fy = Mathf.Clamp((y - Y0) / Step, 0f, Ny - 1.001f);
                int i = (int)fx, j = (int)fy;
                float tx = fx - i, ty = fy - j;
                float a = Mathf.Lerp(Z[j * Nx + i], Z[j * Nx + i + 1], tx);
                float b = Mathf.Lerp(Z[(j + 1) * Nx + i], Z[(j + 1) * Nx + i + 1], tx);
                return Mathf.Lerp(a, b, ty);
            }

            public Vector3 Normal(float x, float y)
            {
                const float E = 0.004f;
                float zx = (Sample(x + E, y) - Sample(x - E, y)) / (2f * E);
                float zy = (Sample(x, y + E) - Sample(x, y - E)) / (2f * E);
                return new Vector3(-zx, -zy, 1f).normalized;
            }
        }

        private static bool _tried;
        private static ChildModel.Data _skeleton;
        private static readonly Dictionary<string, KitMesh> _meshes = new Dictionary<string, KitMesh>();
        private static readonly Dictionary<string, FaceGrid> _grids = new Dictionary<string, FaceGrid>();
        private static Vector3[] _world;
        private static float _skullY, _eyeX, _eyeY, _browY, _noseY, _mouthY;
        private static Vector3[] _skullRadii;
        private static int _headBone;

        public static bool Ready { get { return Load(); } }

        private static bool Load()
        {
            if (_tried) return _skeleton != null;
            _tried = true;
            try
            {
                using (var s = typeof(CharacterKit).Assembly.GetManifestResourceStream("creator_kit.bin"))
                using (var r = new BinaryReader(s))
                {
                    if (new string(r.ReadChars(4)) != "KIT1") throw new IOException("bad header");
                    var d = new ChildModel.Data();
                    d.Height = r.ReadSingle();
                    int nb = r.ReadInt32();
                    d.Names = new string[nb]; d.Parents = new int[nb]; d.Local = new Vector3[nb];
                    _world = new Vector3[nb];
                    for (int i = 0; i < nb; i++)
                    {
                        d.Names[i] = Str(r); d.Parents[i] = r.ReadInt32(); d.Local[i] = V3(r);
                        _world[i] = d.Local[i] + (d.Parents[i] < 0 ? Vector3.zero : _world[d.Parents[i]]);
                        if (d.Names[i] == "hips") { d.HipsBone = i; d.HipHeight = _world[i].y; }
                        if (d.Names[i] == "head") _headBone = i;
                    }
                    int nc = r.ReadInt32();
                    for (int k = 0; k < nc; k++)
                    {
                        string cname = Str(r);
                        var clip = new ChildModel.Clip { Frames = r.ReadInt32(), Period = r.ReadSingle() };
                        clip.Hips = new Vector3[clip.Frames];
                        clip.Rots = new Quaternion[clip.Frames * nb];
                        for (int f = 0; f < clip.Frames; f++)
                        {
                            clip.Hips[f] = V3(r);
                            for (int b = 0; b < nb; b++) clip.Rots[f * nb + b] = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        }
                        if (cname == "idle") d.Idle = clip; else if (cname == "walk") d.Walk = clip; else if (cname == "run") d.Run = clip; else if (cname == "scared") d.Scared = clip;
                    }

                    var bind = new Matrix4x4[nb];
                    for (int i = 0; i < nb; i++) bind[i] = Matrix4x4.TRS(-_world[i], Quaternion.identity, Vector3.one);
                    int nm = r.ReadInt32();
                    for (int m = 0; m < nm; m++)
                    {
                        string name = Str(r);
                        bool skinned = r.ReadByte() == 1;
                        int nv = r.ReadInt32();
                        Vector3 lo = V3(r), hi = V3(r), span = hi - lo;
                        var verts = new Vector3[nv];
                        for (int i = 0; i < nv; i++)
                            verts[i] = lo + Vector3.Scale(span, new Vector3(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16()) / 65535f);
                        var nrms = new Vector3[nv];
                        for (int i = 0; i < nv; i++)
                            nrms[i] = new Vector3(r.ReadSByte(), r.ReadSByte(), r.ReadSByte()).normalized;
                        var mesh = new Mesh { name = name };
                        mesh.vertices = verts; mesh.normals = nrms;
                        if (skinned)
                        {
                            var idx = r.ReadBytes(nv * 4);
                            var wts = r.ReadBytes(nv * 4);
                            var bw = new BoneWeight[nv];
                            for (int i = 0; i < nv; i++)
                            {
                                bw[i].boneIndex0 = idx[i * 4]; bw[i].weight0 = wts[i * 4] / 255f;
                                bw[i].boneIndex1 = idx[i * 4 + 1]; bw[i].weight1 = wts[i * 4 + 1] / 255f;
                                bw[i].boneIndex2 = idx[i * 4 + 2]; bw[i].weight2 = wts[i * 4 + 2] / 255f;
                                bw[i].boneIndex3 = idx[i * 4 + 3]; bw[i].weight3 = wts[i * 4 + 3] / 255f;
                            }
                            mesh.boneWeights = bw;
                            mesh.bindposes = bind;
                        }
                        int ns = r.ReadInt32();
                        var km = new KitMesh { Mesh = mesh, Skinned = skinned, Mats = new string[ns], Colors = new Color[ns], Gloss = new float[ns] };
                        mesh.subMeshCount = ns;
                        for (int k = 0; k < ns; k++)
                        {
                            km.Mats[k] = Str(r);
                            km.Colors[k] = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                            km.Gloss[k] = r.ReadSingle();
                            int ni = r.ReadInt32();
                            var tris = new int[ni];
                            for (int t = 0; t < ni; t++) tris[t] = r.ReadUInt16();
                            mesh.SetTriangles(tris, k);
                        }
                        mesh.RecalculateBounds();
                        _meshes[name] = km;
                    }
                    int ng = r.ReadInt32();
                    for (int g = 0; g < ng; g++)
                    {
                        string name = Str(r);
                        var grid = new FaceGrid { Nx = r.ReadInt32(), Ny = r.ReadInt32(), X0 = r.ReadSingle(), Y0 = r.ReadSingle(), Step = r.ReadSingle() };
                        grid.Z = new float[grid.Nx * grid.Ny];
                        for (int i = 0; i < grid.Z.Length; i++) grid.Z[i] = r.ReadSingle();
                        _grids[name] = grid;
                    }
                    _skullY = r.ReadSingle();
                    _eyeX = r.ReadSingle(); _eyeY = r.ReadSingle(); _browY = r.ReadSingle(); _noseY = r.ReadSingle(); _mouthY = r.ReadSingle();
                    _skullRadii = new Vector3[3];
                    for (int i = 0; i < 3; i++) _skullRadii[i] = V3(r);
                    _skeleton = d;
                }
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Creator] Kit loaded (" + _meshes.Count + " pieces).");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: [Creator] Kit failed to load: " + e);
                _skeleton = null;
            }
            return _skeleton != null;
        }

        /// <summary>Snaps a custom kid together. outfit: an index into the kids' outfit list (KidSkins.Custom).</summary>
        public static GameObject Build(string name, CharacterSpec spec, int outfit) { return Build(name, spec, outfit, KidGender.Custom); }

        /// <summary>Akurrio (DLC): green messy hair, green eyes, a grin with fangs, cat ears, a black hoodie with green drawstrings, and a ring of green flames.</summary>
        public static GameObject BuildAkurrio(string name, int outfit)
        {
            var spec = new CharacterSpec { Frame = 0, Bottom = 0, Head = 0, Hair = 1, HairColor = 10, Skin = 1, Eyes = 1, EyeColor = 2, Brows = 1, Nose = 2, Mouth = 1, Expression = 0, Voice = 0, VoicePitch = -5 };
            return Build(name, spec, outfit, KidGender.Akurrio);
        }

        private static GameObject Build(string name, CharacterSpec spec, int outfit, KidGender who)
        {
            var root = new GameObject(name);
            if (!Load()) return root;
            var d = _skeleton;
            var bones = new Transform[d.Names.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = new GameObject(d.Names[i]).transform;
                bones[i].SetParent(d.Parents[i] < 0 ? root.transform : bones[d.Parents[i]], false);
                bones[i].localPosition = d.Local[i];
            }
            Transform head = bones[_headBone];
            Vector3 headRest = _world[_headBone];

            // One material per name for the whole kid, so the outfit, skin tone and hair color each change in one place.
            var mats = new Dictionary<string, Material>();
            var body = Piece("body_" + CharacterSpec.Frames[spec.Frame].ToLowerInvariant() + "_" + CharacterSpec.Bottoms[spec.Bottom].ToLowerInvariant());
            if (body != null)
            {
                var go = new GameObject("Body");
                go.transform.SetParent(root.transform, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = body.Mesh;
                smr.bones = bones;
                smr.rootBone = bones[0];
                smr.sharedMaterials = Materials(body, mats, spec);
                smr.updateWhenOffscreen = true;
            }

            string headName = CharacterSpec.Heads[spec.Head].ToLowerInvariant();
            Rigid(Piece("head_" + headName), head, -headRest, Vector3.one, Quaternion.identity, mats, spec);

            if (spec.Hair > 0)
            {
                var hair = Piece("hair_" + CharacterSpec.Hairs[spec.Hair].ToLowerInvariant());
                if (hair != null)
                {
                    // Built for the round head; stretched to this head around the skull's center.
                    Vector3 c = new Vector3(0f, _skullY, 0f);
                    Vector3 s = new Vector3(_skullRadii[spec.Head].x / _skullRadii[0].x, _skullRadii[spec.Head].y / _skullRadii[0].y, _skullRadii[spec.Head].z / _skullRadii[0].z);
                    var pivot = new GameObject("HairPivot").transform;
                    pivot.SetParent(head, false);
                    pivot.localPosition = c - headRest;
                    pivot.localScale = s;
                    Rigid(hair, pivot, -c, Vector3.one, Quaternion.identity, mats, spec);
                }
            }

            var anim = root.AddComponent<KidAnimator>();
            anim.Bones = bones;
            anim.Model = d;

            // The face: every feature on the head's own surface, moved by the sliders.
            var face = root.AddComponent<KidFace>();
            face.Spec = spec; face.Head = head; face.HeadRest = headRest; face.Mats = mats; face.Anim = anim;
            _grids.TryGetValue("face_" + headName, out face.Grid);
            BuildFace(face, spec.Expression);

            var all = new Material[mats.Count];
            mats.Values.CopyTo(all, 0);
            var look = root.AddComponent<KidLook>();
            look.Init(all, who, outfit);
            if (who == KidGender.Akurrio) DecorateAkurrio(root.transform, bones, head, headRest, mats, face.Grid);
            if (spec.Accent == 1) look.SetAccent(CharacterSpec.FurColors[spec.AccentColor]); // sleeves and shoes in the Tattletail's color
            return root;
        }

        // â”€â”€ Akurrio's extras: built from primitives and a few small meshes, hung on the kid's own bones â”€â”€
        private static Mesh Pyramid(float w, float h, float d)
        {
            // A four-sided pyramid, base on y = 0, apex up, flat-shaded.
            Vector3 a = new Vector3(-w / 2f, 0f, -d / 2f), b = new Vector3(w / 2f, 0f, -d / 2f), c = new Vector3(w / 2f, 0f, d / 2f), e = new Vector3(-w / 2f, 0f, d / 2f), t = new Vector3(0f, h, 0f);
            var v = new List<Vector3> { a, t, b, b, t, c, c, t, e, e, t, a, a, b, c, c, e, a };
            var tris = new int[v.Count];
            for (int i = 0; i < tris.Length; i++) tris[i] = i;
            var m = new Mesh { name = "KE_Pyramid" };
            m.vertices = v.ToArray(); m.triangles = tris; m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        private static Mesh Flame(float w, float h)
        {
            // One flame blade (a curling tongue), visible from both sides.
            var p = new Vector3[] { new Vector3(-w / 2f, 0f, 0f), new Vector3(w / 2f, 0f, 0f), new Vector3(-w * 0.42f, h * 0.45f, 0f), new Vector3(w * 0.34f, h * 0.5f, 0f), new Vector3(w * 0.12f, h, 0f) };
            int[] front = { 0, 1, 2, 1, 3, 2, 2, 3, 4 };
            var verts = new Vector3[p.Length];
            p.CopyTo(verts, 0);
            var tris = new int[front.Length * 2];
            for (int i = 0; i < front.Length; i += 3)
            {
                tris[i] = front[i]; tris[i + 1] = front[i + 1]; tris[i + 2] = front[i + 2];
                tris[front.Length + i] = front[i + 2]; tris[front.Length + i + 1] = front[i + 1]; tris[front.Length + i + 2] = front[i];
            }
            var m = new Mesh { name = "KE_Flame" };
            m.vertices = verts; m.triangles = tris; m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        private static GameObject Bit(Transform parent, Vector3 parentRestWorld, Vector3 restWorld, Mesh mesh, PrimitiveType prim, Vector3 scale, Quaternion rot, Material mat)
        {
            GameObject go;
            if (mesh != null)
            {
                go = new GameObject(mesh.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>();
            }
            else
            {
                go = GameObject.CreatePrimitive(prim);
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = restWorld - parentRestWorld;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static void DecorateAkurrio(Transform root, Transform[] bones, Transform head, Vector3 headRest, Dictionary<string, Material> mats, FaceGrid grid)
        {
            Color green = new Color(0.2f, 0.62f, 0.2f), bright = new Color(0.35f, 1f, 0.4f);
            Material mHair;
            if (mats.TryGetValue("hair", out mHair)) mHair.color = new Color(0.16f, 0.5f, 0.16f);
            Material mIris;
            if (mats.TryGetValue("iris", out mIris)) mIris.color = new Color(0.4f, 0.95f, 0.4f);

            // Cat ears: two green pyramids on top of the head, with a bright inside.
            Material earMat = Mat.Lit(new Color(0.16f, 0.5f, 0.16f), 0.2f), insideMat = Mat.Lit(new Color(0.55f, 0.95f, 0.55f), 0.2f);
            Mesh ear = Pyramid(0.056f, 0.08f, 0.03f), inner = Pyramid(0.032f, 0.052f, 0.012f);
            for (int side = -1; side <= 1; side += 2)
            {
                Quaternion tilt = Quaternion.Euler(0f, 0f, -side * 16f);
                Bit(head, headRest, new Vector3(side * 0.058f, _skullY + 0.07f, -0.004f), ear, PrimitiveType.Cube, Vector3.one, tilt, earMat);
                Bit(head, headRest, new Vector3(side * 0.058f, _skullY + 0.072f, 0.01f), inner, PrimitiveType.Cube, Vector3.one, tilt, insideMat);
            }

            // Fangs: two little white points under the lip.
            Material white = Mat.Lit(new Color(0.97f, 0.97f, 0.95f), 0.4f);
            Mesh fang = Pyramid(0.007f, 0.011f, 0.006f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.0125f, y = _mouthY - 0.0035f;
                float z = grid != null ? grid.Sample(x, y) + 0.0025f : 0.1f;
                Bit(head, headRest, new Vector3(x, y, z), fang, PrimitiveType.Cube, Vector3.one, Quaternion.Euler(180f, 0f, 0f), white);
            }

            // The hoodie: the hood bunched behind the neck, and two green drawstrings down the chest.
            Transform chest = bones[3];
            Vector3 chestRest = _world[3];
            Material hood;
            if (!mats.TryGetValue("shirtA", out hood)) hood = Mat.Lit(new Color(0.09f, 0.09f, 0.11f), 0.2f);
            Bit(chest, chestRest, new Vector3(0f, 0.835f, -0.068f), null, PrimitiveType.Sphere, new Vector3(0.2f, 0.11f, 0.115f), Quaternion.identity, hood);
            Material lace = Mat.Lit(new Color(0.3f, 0.95f, 0.35f), 0.3f);
            for (int side = -1; side <= 1; side += 2)
            {
                Bit(chest, chestRest, new Vector3(side * 0.03f, 0.755f, 0.088f), null, PrimitiveType.Cylinder, new Vector3(0.007f, 0.055f, 0.007f), Quaternion.Euler(0f, 0f, side * 3f), lace);
                Bit(chest, chestRest, new Vector3(side * 0.031f, 0.69f, 0.088f), null, PrimitiveType.Sphere, new Vector3(0.014f, 0.014f, 0.014f), Quaternion.identity, lace);
            }

            // The aura: a ring of green flames around him, flickering.
            var aura = new GameObject("KE_AkurrioAura");
            aura.transform.SetParent(root, false);
            aura.layer = root.gameObject.layer;
            Material fire = Mat.Unlit(new Color(0.22f, 0.9f, 0.3f)), fireBright = Mat.Unlit(bright);
            var list = new List<Transform>();
            int n = 22;
            for (int i = 0; i < n; i++)
            {
                float ang = i / (float)n * Mathf.PI * 2f + (i % 2) * 0.12f;
                float rad = 0.34f + (i % 3) * 0.05f;
                float baseY = 0.12f + (i % 4) * 0.2f + (i % 2) * 0.07f;
                float h = 0.34f + (i % 5) * 0.07f;
                Vector3 pos = new Vector3(Mathf.Sin(ang) * rad, baseY, Mathf.Cos(ang) * rad);
                var f = Bit(aura.transform, Vector3.zero, pos, Flame(0.13f, h), PrimitiveType.Cube, Vector3.one, Quaternion.Euler(0f, ang * Mathf.Rad2Deg + 90f, 0f), i % 3 == 0 ? fireBright : fire);
                f.transform.localRotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg + 90f, (i % 2 == 0 ? 1 : -1) * 12f);
                list.Add(f.transform);
            }
            aura.AddComponent<AuraFlames>().Init(list);
        }

        // Freckles around each cheek (x out from the eye, y down from it), the first five for "Some".
        private static readonly Vector2[] FRECKLES =
        {
            new Vector2(-0.0066f, -0.033f), new Vector2(0.0044f, -0.0297f), new Vector2(0.0143f, -0.0352f), new Vector2(-0.0011f, -0.0396f), new Vector2(0.0099f, -0.0429f),
            new Vector2(0.0198f, -0.0286f), new Vector2(-0.0099f, -0.044f), new Vector2(0.022f, -0.0407f), new Vector2(0.0044f, -0.0484f),
        };

        /// <summary>(Re)builds a custom kid's face with an expression: eyes, brows, nose, mouth and the extras.</summary>
        internal static void BuildFace(KidFace f, int expression)
        {
            if (f.Face != null) Object.Destroy(f.Face.gameObject);
            f.Face = new GameObject("Face").transform;
            f.Face.SetParent(f.Head, false);
            f.Face.gameObject.layer = f.Head.gameObject.layer;
            Transform head = f.Face;
            Vector3 headRest = f.HeadRest;
            FaceGrid grid = f.Grid;
            CharacterSpec spec = f.Spec;
            var mats = f.Mats;

            float eyeX = _eyeX + spec.EyeSpacing / 100f * 0.009f, eyeY = _eyeY + spec.EyeHeight / 100f * 0.009f;
            float eyeS = 1f + spec.EyeSizeWith(expression) / 100f * 0.35f;
            float browY = _browY + (eyeY - _eyeY) + spec.BrowHeightWith(expression) / 100f * 0.009f, browA = spec.BrowAngleWith(expression) / 100f * 25f;
            for (int side = -1; side <= 1; side += 2)
            {
                bool m = side < 0;
                Feature("feature_eye_" + CharacterSpec.EyeStyles[spec.Eyes].ToLowerInvariant(), head, headRest, grid, eyeX * side, eyeY, eyeS, 0f, m, mats, spec);
                if (spec.Brows > 0)
                    Feature("feature_brow_" + CharacterSpec.BrowStyles[spec.Brows].ToLowerInvariant(), head, headRest, grid, eyeX * side, browY, 1f, browA, m, mats, spec);
                if (spec.EyeCircles > 0)
                    Feature("feature_extra_circles", head, headRest, grid, eyeX * side, eyeY, eyeS, 0f, m, mats, spec);
                if (spec.Glasses > 0)
                    Feature("feature_glasses_" + CharacterSpec.GlassesStyles[spec.Glasses].ToLowerInvariant(), head, headRest, grid, eyeX * side, eyeY, 1f + (eyeS - 1f) * 0.5f, 0f, m, mats, spec);
                int dots = spec.Freckles == 0 ? 0 : spec.Freckles == 1 ? 5 : FRECKLES.Length;
                for (int i = 0; i < dots; i++)
                    Feature("feature_extra_dot", head, headRest, grid, (eyeX + FRECKLES[i].x) * side, eyeY + FRECKLES[i].y, 1f - (i % 3) * 0.12f, 0f, m, mats, spec);
            }
            if (spec.Nose > 0)
                Feature("feature_nose_" + CharacterSpec.Noses[spec.Nose].ToLowerInvariant(), head, headRest, grid, 0f, _noseY, 1f + spec.NoseSize / 100f * 0.35f, 0f, false, mats, spec);
            float mouthY = _mouthY + spec.MouthHeight / 100f * 0.009f;
            Feature("feature_mouth_" + CharacterSpec.Mouths[spec.MouthWith(expression)].ToLowerInvariant(), head, headRest, grid, 0f, mouthY, 1f + spec.MouthSize / 100f * 0.35f, 0f, false, mats, spec);

            switch (spec.BandAid)
            {
                case 1: Feature("feature_extra_bandaid", head, headRest, grid, -(eyeX + 0.013f), eyeY - 0.037f, 1f, -20f, false, mats, spec); break;
                case 2: Feature("feature_extra_bandaid", head, headRest, grid, 0f, _noseY + 0.02f, 0.75f, 8f, false, mats, spec); break;
                case 3: Feature("feature_extra_bandaid", head, headRest, grid, 0.012f, mouthY - 0.03f, 0.8f, 12f, false, mats, spec); break;
                case 4:
                    Feature("feature_extra_bandaid", head, headRest, grid, -(eyeX + 0.013f), eyeY - 0.038f, 0.9f, 40f, false, mats, spec);
                    Feature("feature_extra_bandaid", head, headRest, grid, -(eyeX + 0.013f), eyeY - 0.038f, 0.9f, -40f, false, mats, spec);
                    break;
            }
        }

        private static KitMesh Piece(string name) { KitMesh m; return _meshes.TryGetValue(name, out m) ? m : null; }

        private static Material[] Materials(KitMesh piece, Dictionary<string, Material> mats, CharacterSpec spec)
        {
            var list = new Material[piece.Mats.Length];
            for (int i = 0; i < list.Length; i++)
            {
                string n = piece.Mats[i];
                Material m;
                if (!mats.TryGetValue(n, out m))
                {
                    Color c = piece.Colors[i];
                    if (n == "skin") c = CharacterSpec.SkinTones[spec.Skin];
                    else if (n == "hair") c = CharacterSpec.HairColors[spec.HairColor];
                    else if (n == "iris") c = CharacterSpec.EyeColors[spec.EyeColor];
                    else if (n == "freckle") c = Color.Lerp(CharacterSpec.SkinTones[spec.Skin], new Color(0.45f, 0.24f, 0.13f), 0.5f);
                    else if (n == "circles") c = Color.Lerp(CharacterSpec.SkinTones[spec.Skin], new Color(0.3f, 0.17f, 0.3f), spec.EyeCircles == 2 ? 0.5f : 0.28f);
                    m = Mat.Lit(c, piece.Gloss[i]);
                    m.name = n;
                    mats[n] = m;
                }
                list[i] = m;
            }
            return list;
        }

        private static void Rigid(KitMesh piece, Transform parent, Vector3 localPos, Vector3 scale, Quaternion rot, Dictionary<string, Material> mats, CharacterSpec spec)
        {
            if (piece == null) return;
            var go = new GameObject(piece.Mesh.name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer; // on the preview stage too, from the first frame
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = piece.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = Materials(piece, mats, spec);
        }

        // A feature (centered, facing +Z) placed on the face at (x, y), turned to the face's slope.
        private static void Feature(string name, Transform head, Vector3 headRest, FaceGrid grid, float x, float y, float size, float angle, bool mirror,
                                    Dictionary<string, Material> mats, CharacterSpec spec)
        {
            var piece = Piece(name);
            if (piece == null) return;
            float z = grid != null ? grid.Sample(x, y) + 0.0015f : 0.11f;
            Vector3 n = grid != null ? grid.Normal(x, y) : Vector3.forward;
            Quaternion rot = Quaternion.FromToRotation(Vector3.forward, n) * Quaternion.Euler(0f, 0f, mirror ? -angle : angle);
            Rigid(piece, head, new Vector3(x, y, z) - headRest, new Vector3(mirror ? -size : size, size, size), rot, mats, spec);
        }

        private static Vector3 V3(BinaryReader r) { return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }
        private static string Str(BinaryReader r) { return System.Text.Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32())); }
    }

    /// <summary>A custom kid's face: their chosen expression, or Terrified while they're scared (caught, or Mama's Gaze).</summary>
    public class KidFace : MonoBehaviour
    {
        internal CharacterSpec Spec;
        internal Transform Head, Face;
        internal Vector3 HeadRest;
        internal CharacterKit.FaceGrid Grid;
        internal Dictionary<string, Material> Mats;
        internal KidAnimator Anim;
        private bool _scared;

        private void Update()
        {
            bool scared = Anim != null && Anim.Scared;
            if (scared == _scared) return;
            _scared = scared;
            CharacterKit.BuildFace(this, scared ? CharacterSpec.TERRIFIED : Spec.Expression);
        }
    }
}

namespace KaleidoscopeEndless
{
    /// <summary>Akurrio's aura: the flames grow and shrink at random and sway.</summary>
    public class AuraFlames : MonoBehaviour
    {
        private Transform[] _flames;
        private Quaternion[] _rest;
        private Vector3[] _restPos;

        public void Init(System.Collections.Generic.List<Transform> flames)
        {
            _flames = flames.ToArray();
            _rest = new Quaternion[_flames.Length];
            _restPos = new Vector3[_flames.Length];
            for (int i = 0; i < _flames.Length; i++) { _rest[i] = _flames[i].localRotation; _restPos[i] = _flames[i].localPosition; }
        }

        private void Update()
        {
            if (_flames == null) return;
            float t = Time.time;
            for (int i = 0; i < _flames.Length; i++)
            {
                if (_flames[i] == null) continue;
                float k = Mathf.PerlinNoise(t * 2.4f + i * 3.1f, i * 1.7f);
                _flames[i].localScale = new Vector3(0.85f + 0.3f * k, 0.65f + 0.75f * k, 1f);
                float sway = (Mathf.PerlinNoise(t * 1.3f + i * 5.3f, 9f) - 0.5f) * 24f;
                _flames[i].localRotation = _rest[i] * Quaternion.Euler(0f, 0f, sway);
                Vector3 p = _restPos[i];
                p.y += k * 0.05f;
                _flames[i].localPosition = p;
            }
        }
    }
}
