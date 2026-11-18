using System.IO;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>Which Versus character: the two kids, then DLC characters (Dez: DrDezmen's, Kaz: Kazyzone's, the Rat King, Plague: ImPoPzzZ the plague doctor, Fish: Sa1monfr).</summary>
    public enum KidGender { Boy = 0, Girl = 1, Dez = 2, Kaz = 3, Custom = 4, Rat = 5, Plague = 6, Fish = 7, Akurrio = 8 }  // Custom: built in the Character Creator. Akurrio: a DLC character built from the Creator's kit (CharacterKit.BuildAkurrio)

    /// <summary>
    /// The survivor as other players see them: a sculpted, skinned kid (~1.1 m, feet at the root),
    /// a boy or a girl, with Idle / Walk / Run / Scared loops and outfits (KidSkins).
    /// The models are built by tools/kid_model.py into assets/kid_boy.bin and kid_girl.bin,
    /// which are embedded in the DLL.
    /// </summary>
    public static class ChildModel
    {
        public const float HEIGHT = 1.1f; // tools/kid_model.py HEIGHT

        internal class Clip
        {
            public float Period;
            public int Frames;
            public Vector3[] Hips;        // per frame
            public Quaternion[] Rots;     // per frame * bone
        }

        internal class Data
        {
            public string[] Names;
            public int[] Parents;
            public Vector3[] Local;
            public Mesh Mesh;
            public Material[] Mats;
            public Clip Idle, Walk, Run, Scared;
            public int HipsBone;
            public float Height;    // top of the model (Dez's ear tips)
            public float HipHeight; // leg length: a grown-up takes longer steps
        }

        private static readonly Data[] _data = new Data[9];
        private static readonly bool[] _tried = new bool[9];

        /// <summary>How tall this character's model is (for name tags and previews).</summary>
        public static float HeightOf(KidGender g) { if (g == KidGender.Custom || g == KidGender.Akurrio) return HEIGHT; Data d = Load(g); return d != null ? d.Height : HEIGHT; }

        /// <summary>Roughly where the face is (Mama's kill aims at it).</summary>
        public static float FaceHeightOf(KidGender g) { return g == KidGender.Kaz ? 1.62f : g == KidGender.Dez ? 0.86f : g == KidGender.Rat ? 0.7f : g == KidGender.Plague ? 1.62f : g == KidGender.Fish ? 1.5f : HEIGHT * 0.85f; }

        public static GameObject Build(string name, KidGender gender = KidGender.Boy, int skin = 0)
        {
            if (skin == KidSkins.MANNEQUIN) // (a blank mannequin: Identity)
            {
                var blank = Build(name, KidGender.Boy, 0);
                Identity.PaintMannequin(blank);
                return blank;
            }
            if (gender == KidGender.Custom) return CharacterKit.Build(name, CharacterSpec.Local, skin); // this player's own custom kid
            if (gender == KidGender.Akurrio) return CharacterKit.BuildAkurrio(name, skin);
            var root = new GameObject(name);
            Data d = Load(gender);
            if (d == null) return root;

            var bones = new Transform[d.Names.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = new GameObject(d.Names[i]).transform;
                bones[i].SetParent(d.Parents[i] < 0 ? root.transform : bones[d.Parents[i]], false);
                bones[i].localPosition = d.Local[i];
            }
            var meshGO = new GameObject("KidMesh");
            meshGO.transform.SetParent(root.transform, false);
            var smr = meshGO.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = d.Mesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            var mats = new Material[d.Mats.Length]; // its own copies, so each kid can wear its own outfit
            for (int i = 0; i < mats.Length; i++) mats[i] = new Material(d.Mats[i]) { name = d.Mats[i].name };
            smr.sharedMaterials = mats;
            smr.updateWhenOffscreen = true; // only ever a few of these

            var anim = root.AddComponent<KidAnimator>();
            anim.Bones = bones;
            anim.Model = d;
            root.AddComponent<KidLook>().Init(mats, gender, skin);
            return root;
        }

        private static Data Load(KidGender gender)
        {
            int g = (int)gender;
            if (_tried[g]) return _data[g];
            _tried[g] = true;
            string res = "kid_" + gender.ToString().ToLowerInvariant() + ".bin"; // kid_boy / kid_girl / kid_dez / kid_kaz
            try
            {
                var d = new Data();
                using (var s = typeof(ChildModel).Assembly.GetManifestResourceStream(res))
                using (var r = new BinaryReader(s))
                {
                    if (new string(r.ReadChars(4)) != "KID2") throw new IOException("bad header");
                    int nb = r.ReadInt32();
                    d.Names = new string[nb]; d.Parents = new int[nb]; d.Local = new Vector3[nb];
                    var world = new Vector3[nb];
                    for (int i = 0; i < nb; i++)
                    {
                        d.Names[i] = Str(r);
                        d.Parents[i] = r.ReadInt32();
                        d.Local[i] = V3(r);
                        world[i] = d.Local[i] + (d.Parents[i] < 0 ? Vector3.zero : world[d.Parents[i]]);
                        if (d.Names[i] == "hips") { d.HipsBone = i; d.HipHeight = world[i].y; }
                    }

                    int nv = r.ReadInt32();
                    var verts = new Vector3[nv]; var nrms = new Vector3[nv]; var bw = new BoneWeight[nv];
                    for (int i = 0; i < nv; i++)
                    {
                        verts[i] = V3(r); nrms[i] = V3(r);
                        var w = new BoneWeight();
                        w.boneIndex0 = r.ReadInt32(); w.weight0 = r.ReadSingle();
                        w.boneIndex1 = r.ReadInt32(); w.weight1 = r.ReadSingle();
                        w.boneIndex2 = r.ReadInt32(); w.weight2 = r.ReadSingle();
                        w.boneIndex3 = r.ReadInt32(); w.weight3 = r.ReadSingle();
                        bw[i] = w;
                    }
                    d.Mesh = new Mesh { name = "Kid" + gender };
                    d.Mesh.vertices = verts; d.Mesh.normals = nrms; d.Mesh.boneWeights = bw;
                    var bind = new Matrix4x4[nb];
                    for (int i = 0; i < nb; i++) bind[i] = Matrix4x4.TRS(-world[i], Quaternion.identity, Vector3.one); // rest pose has no rotations
                    d.Mesh.bindposes = bind;

                    int ns = r.ReadInt32();
                    d.Mesh.subMeshCount = ns;
                    d.Mats = new Material[ns];
                    for (int m = 0; m < ns; m++)
                    {
                        string mname = Str(r);
                        var c = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        d.Mats[m] = Mat.Lit(c, r.ReadSingle());
                        d.Mats[m].name = mname;
                        var tris = new int[r.ReadInt32()];
                        for (int t = 0; t < tris.Length; t++) tris[t] = r.ReadInt32();
                        d.Mesh.SetTriangles(tris, m);
                    }
                    d.Mesh.RecalculateBounds();
                    d.Height = d.Mesh.bounds.max.y;

                    int nc = r.ReadInt32();
                    for (int k = 0; k < nc; k++)
                    {
                        string cname = Str(r);
                        var clip = new Clip { Frames = r.ReadInt32(), Period = r.ReadSingle() };
                        clip.Hips = new Vector3[clip.Frames];
                        clip.Rots = new Quaternion[clip.Frames * nb];
                        for (int f = 0; f < clip.Frames; f++)
                        {
                            clip.Hips[f] = V3(r);
                            for (int b = 0; b < nb; b++)
                                clip.Rots[f * nb + b] = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        }
                        if (cname == "idle") d.Idle = clip; else if (cname == "walk") d.Walk = clip; else if (cname == "run") d.Run = clip; else if (cname == "scared") d.Scared = clip;
                    }
                }
                if (d.Idle == null || d.Walk == null || d.Run == null) throw new IOException("missing animations");
                _data[g] = d;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Kid model loaded (" + gender + ").");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: [Versus] Kid model (" + gender + ") failed to load: " + e);
            }
            return _data[g];
        }

        private static Vector3 V3(BinaryReader r) { return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }
        private static string Str(BinaryReader r) { return System.Text.Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32())); }
    }

    /// <summary>
    /// The kids' outfits: shirt stripes (A/B), sleeves, pants (the girl's skirt) and shoes.
    /// Both kids are free, each with a free first outfit; the rest cost OUTFIT_PRICE shards each,
    /// owned in Shop.txt as "Outfit Boy Ringer Tee = 1".
    /// </summary>
    public static class KidSkins
    {
        public const int OUTFIT_PRICE = 100;

        public struct Skin
        {
            public string Name;
            public Color ShirtA, ShirtB, Sleeve, Pants, Shoe, Letters;
            public bool Rainbow;
            public Skin(string name, Color a, Color b, Color sleeve, Color pants, Color shoe, bool rainbow = false)
            { Name = name; ShirtA = a; ShirtB = b; Sleeve = sleeve; Pants = pants; Shoe = shoe; Rainbow = rainbow; Letters = new Color(0.04f, 0.04f, 0.05f); }
        }

        private static Color C(int hex) { return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f); }
        private static readonly Color Jeans = C(0x384F85), White = C(0xEBEBE6);

        public static readonly Skin[] Boy =
        {
            new Skin("Classic Stripes", C(0xBD2124), C(0xEDE0C2), C(0xBD2124), Jeans, White),
            new Skin("Ringer Tee",      C(0xF2C43A), C(0xF2C43A), C(0x24336B), Jeans, White),
            new Skin("Pajamas",         C(0x8FB8E6), C(0xB5D2F2), C(0x8FB8E6), C(0x8FB8E6), White),
            new Skin("Dino Stripes",    C(0x2E8C3E), C(0xA8D65C), C(0x2E8C3E), C(0xA8946B), C(0x595959)),
            new Skin("Night Owl",       C(0x1C1C21), C(0x1C1C21), C(0x1C1C21), C(0x262B3D), C(0xB32626)),
            new Skin("Bubblegum",       C(0xE673A6), C(0xFAEBF2), C(0xE673A6), Jeans, White),
            new Skin("Party Time",      C(0xF28A1F), C(0xF7D440), C(0xF28A1F), Jeans, White),
            new Skin("Kaleidoscope",    Color.white, Color.white, Color.white, C(0x2B2140), White, true),
        };

        public static readonly Skin[] Girl =
        {
            new Skin("Pink Tee",        C(0xE87AA8), C(0xE87AA8), C(0xE87AA8), Jeans, White),
            new Skin("Striped Tee",     C(0xBD2124), C(0xEDE0C2), C(0xBD2124), Jeans, White),
            new Skin("Sunflower Dress", C(0xF5C842), C(0xF5C842), C(0xF5C842), C(0xF5C842), White),
            new Skin("Pajamas",         C(0x8FB8E6), C(0xB5D2F2), C(0x8FB8E6), C(0x8FB8E6), White),
            new Skin("Mint Dress",      C(0x8FDBB8), C(0x8FDBB8), C(0x8FDBB8), C(0x8FDBB8), White),
            new Skin("Night Owl",       C(0x1C1C21), C(0x1C1C21), C(0x1C1C21), C(0x3A2552), C(0xB32626)),
            new Skin("Party Dress",     C(0x9A5BD6), C(0xC9A3F0), C(0x9A5BD6), C(0x9A5BD6), White),
            new Skin("Kaleidoscope",    Color.white, Color.white, Color.white, C(0x2B2140), White, true),
        };

        // Dez's tees: the shirt color, and his "DEZ" letters in a color that reads on it.
        private static Skin DezTee(string name, Color shirt, Color letters, bool rainbow = false)
        {
            var s = new Skin(name, shirt, shirt, shirt, shirt, shirt, rainbow);
            s.Letters = letters;
            return s;
        }

        public static readonly Skin[] Dez =
        {
            DezTee("DEZ Tee",      C(0x9C0E2E), C(0x0A0A0D)),
            DezTee("Midnight Tee", C(0x16161B), C(0xF2F2F2)),
            DezTee("Ocean Tee",    C(0x1F5FA8), C(0x0A0A0D)),
            DezTee("Toxic Tee",    C(0x5FBF2A), C(0x0A0A0D)),
            DezTee("Kaleidoscope", Color.white, C(0x0A0A0D), true),
        };

        // Kaz's tank tops (his KAZ badge stays white), dark pants and shoes.
        private static Skin KazTank(string name, Color tank, bool rainbow = false) { return new Skin(name, tank, tank, tank, C(0x2B334D), C(0x242429), rainbow); }

        public static readonly Skin[] Kaz =
        {
            KazTank("KAZ Tank",    C(0xF26A1B)),
            KazTank("Black Tank",  C(0x1A1A1F)),
            KazTank("Red Tank",    C(0xB8141E)),
            KazTank("Royal Tank",  C(0x2447B3)),
            KazTank("Kaleidoscope", Color.white, true),
        };

        // The Rat King's crowns (and his controller to match).
        private static Skin RatCrown(string name, Color crown, Color controller, bool rainbow = false) { return new Skin(name, crown, crown, crown, controller, controller, rainbow); }

        public static readonly Skin[] Rat =
        {
            RatCrown("Gold Crown",    C(0xFFD61F), C(0x54545C)),
            RatCrown("Silver Crown",  C(0xC9CDD6), C(0x1A1A1F)),
            RatCrown("Ruby Crown",    C(0xC8102E), C(0x54545C)),
            RatCrown("Diamond Crown", C(0xA8E6FF), C(0xF2F2F2)),
            RatCrown("Kaleidoscope",  Color.white, C(0x2B2140), true),
        };

        // ImPoPzzZ, the plague doctor: coat, cloak and buttons, hat and boots. shirtA is the coat, shirtB the cloak and trim, shoe the leather.
        private static Skin Doctor(string name, int coat, int trim, int leather, bool rainbow = false)
        { return new Skin(name, C(coat), C(trim), C(coat), C(0x17171A), C(leather), rainbow); }

        public static readonly Skin[] Plague =
        {
            Doctor("Plague Doctor",     0x1B1B21, 0x2B1E17, 0x1F1712),
            Doctor("Crimson Physician", 0x5E0E14, 0x1A1414, 0x2A1812),
            Doctor("Bone Doctor",       0xC9C2B0, 0x4A3F33, 0x3A2A1E),
            Doctor("Midnight Surgeon",  0x10183A, 0x262B4A, 0x14141E),
            Doctor("Toxic Green",       0x1F5A2A, 0x0F2A14, 0x1C1C14),
            Doctor("Kaleidoscope",      0xFFFFFF, 0xFFFFFF, 0x1C1C14, true),
        };

        // Sa1monfr: a very strong man with a fish's head. shirtA is his belt, sleeve his buckle and wristbands, pants his shorts.
        private static Skin Swimmer(string name, int belt, int bands, int shorts, int shoes, bool rainbow = false)
        { return new Skin(name, C(belt), C(belt), C(bands), C(shorts), C(shoes), rainbow); }

        public static readonly Skin[] Fish =
        {
            Swimmer("Salmon",       0xFA8072, 0xF2C840, 0x1F3366, 0xEBEBE6),
            Swimmer("Champion",     0xD4AF37, 0xD4AF37, 0x8B0000, 0x1A1A1E),
            Swimmer("Beach Bum",    0x2EC4B6, 0xFF9F1C, 0xFF6B6B, 0xFFFFFF),
            Swimmer("Night Swim",   0x6C5CE7, 0x00CEC9, 0x111122, 0x222233),
            Swimmer("Sushi",        0xEEEEEE, 0xE63946, 0x2D6A4F, 0xEEEEEE),
            Swimmer("Kaleidoscope", 0xFFFFFF, 0xFFFFFF, 0x2B2140, 0xEBEBE6, true),
        };

        // Akurrio: a black hoodie with green drawstrings. shirtA is the hoodie, sleeve the hood and cuffs, pants the joggers.
        private static Skin Hoodie(string name, int hoodie, int sleeve, int pants, int shoes, bool rainbow = false)
        { return new Skin(name, C(hoodie), C(hoodie), C(sleeve), C(pants), C(shoes), rainbow); }

        public static readonly Skin[] Akurrio =
        {
            Hoodie("Black Hoodie",  0x17171B, 0x17171B, 0x1E1E24, 0x1A1A1F),
            Hoodie("Toxic Hoodie",  0x1F7A2E, 0x1F7A2E, 0x17171B, 0x17171B),
            Hoodie("Ghost Hoodie",  0xE6E6EA, 0xE6E6EA, 0x2A2A32, 0x1A1A1F),
            Hoodie("Kaleidoscope",  0xFFFFFF, 0xFFFFFF, 0x2B2140, 0xEBEBE6, true),
        };

        /// <summary>DLC characters, in the OUTFITS screen's "DLC Characters" dropdown.</summary>
        public static readonly KidGender[] DlcCharacters = { KidGender.Dez, KidGender.Kaz, KidGender.Rat, KidGender.Plague, KidGender.Fish, KidGender.Akurrio };
        public static bool IsDlc(KidGender g) { return g == KidGender.Dez || g == KidGender.Kaz || g == KidGender.Rat || g == KidGender.Plague || g == KidGender.Fish || g == KidGender.Akurrio; }
        public static string CharacterName(KidGender g)
        {
            switch (g) { case KidGender.Dez: return "Dez"; case KidGender.Kaz: return "Kaz"; case KidGender.Rat: return "Rat King"; case KidGender.Plague: return "ImPoPzzZ"; case KidGender.Fish: return "Sa1monfr"; case KidGender.Akurrio: return "Akurrio"; case KidGender.Girl: return "Girl"; case KidGender.Custom: return "Custom"; default: return "Boy"; }
        }

        public static Skin[] For(KidGender g)
        {
            switch (g) { case KidGender.Dez: return Dez; case KidGender.Kaz: return Kaz; case KidGender.Rat: return Rat; case KidGender.Plague: return Plague; case KidGender.Fish: return Fish; case KidGender.Akurrio: return Akurrio; case KidGender.Girl: return Girl; default: return Boy; } // Custom wears the boy's outfits
        }

        private const string PREF_GENDER = "KE_KidGender", PREF_SKIN = "KE_KidOutfit_";

        /// <summary>This player's kid and outfit (saved between sessions).</summary>
        public static KidGender Gender
        {
            get { int g = PlayerPrefs.GetInt(PREF_GENDER, 0); return g >= 0 && g <= 8 ? (KidGender)g : KidGender.Boy; }
        }

        public static int SkinFor(KidGender g)
        {
            int i = PlayerPrefs.GetInt(PREF_SKIN + g, 0);
            return i >= 0 && i < For(g).Length && Owned(g, i) ? i : 0;
        }

        public static int Selected { get { return SkinFor(Gender); } }

        public static string Key(KidGender g, int i) { return "Outfit " + (g == KidGender.Custom ? KidGender.Boy : g) + " " + For(g)[i].Name; } // Custom shares the boy's outfits
        public static bool Owned(KidGender g, int i) { return i == 0 || Shop.IsOwned(Key(g, i)); }

        public static void SetGender(KidGender g) { PlayerPrefs.SetInt(PREF_GENDER, (int)g); PlayerPrefs.Save(); }

        public static void Equip(KidGender g, int i)
        {
            if (!Owned(g, i)) return;
            PlayerPrefs.SetInt(PREF_SKIN + g, i);
            PlayerPrefs.Save();
        }

        /// <summary>Buys (and wears) an outfit. False if it's already owned or you can't afford it.</summary>
        public static bool Buy(KidGender g, int i)
        {
            if (Owned(g, i) || Shards.Balance < OUTFIT_PRICE) return false;
            Shop.SpendShards(OUTFIT_PRICE);
            Shop.Unlock(Key(g, i));
            Equip(g, i);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Outfits] Bought '" + Key(g, i) + "'.");
            return true;
        }

        // Sent to the other player as one number: gender * 100 + outfit.
        public static int Code { get { return Identity.Forgotten ? MANNEQUIN : (int)Gender * 100 + Selected; } } // (erased: your friend sees the mannequin too)

        /// <summary>The "outfit" of a blank wooden mannequin (Identity.Forgotten): sent as the code 99.</summary>
        public const int MANNEQUIN = 99;
        public static KidGender GenderOf(int code) { int g = code / 100; return g >= 0 && g <= 8 ? (KidGender)g : KidGender.Boy; }
        public static int SkinOf(int code) { return code % 100; }
    }

    /// <summary>Puts an outfit on one kid (and cycles the Kaleidoscope shirt's colors).</summary>
    public class KidLook : MonoBehaviour
    {
        private Material _a, _b, _sleeve, _pants, _shoe, _bow, _letters;
        private KidGender _gender;
        public KidGender Gender { get { return _gender; } }
        private int _skin = -1;
        private bool _rainbow, _hasAccent;
        private Color _accent;

        /// <summary>Sleeves and shoes in one color over any outfit (a custom kid matching their Tattletail).</summary>
        public void SetAccent(Color c)
        {
            _hasAccent = true;
            _accent = c;
            int s = _skin;
            _skin = -1;
            Set(s);
        }

        public void Init(Material[] mats, KidGender gender, int skin)
        {
            _gender = gender;
            foreach (var m in mats)
            {
                if (m.name == "shirtA") _a = m;
                else if (m.name == "shirtB") _b = m;
                else if (m.name == "sleeve") _sleeve = m;
                else if (m.name == "pants") _pants = m;
                else if (m.name == "shoe") _shoe = m;
                else if (m.name == "bow") _bow = m;
                else if (m.name == "letters") _letters = m;
            }
            Set(skin);
        }

        public void Set(int skin)
        {
            var list = KidSkins.For(_gender);
            if (skin < 0 || skin >= list.Length) skin = 0;
            if (skin == _skin || _a == null) return;
            _skin = skin;
            var s = list[skin];
            _a.color = s.ShirtA;
            if (_b != null) _b.color = s.ShirtB;
            if (_sleeve != null) _sleeve.color = s.Sleeve;
            if (_pants != null) _pants.color = s.Pants;
            if (_shoe != null) _shoe.color = s.Shoe;
            if (_bow != null) _bow.color = s.Sleeve;
            if (_letters != null) _letters.color = s.Letters;
            _rainbow = s.Rainbow;
            if (_hasAccent)
            {
                if (_sleeve != null) _sleeve.color = _accent;
                if (_shoe != null) _shoe.color = _accent;
            }
        }

        private void Update()
        {
            if (!_rainbow) return;
            float t = Time.unscaledTime * 0.25f;
            _a.color = Gadgets.Rainbow(t);
            if (_b != null) _b.color = Gadgets.Rainbow(t + 0.33f);
            if (_sleeve != null && !_hasAccent) _sleeve.color = _gender == KidGender.Dez ? _a.color : Gadgets.Rainbow(t + 0.66f); // Dez: one tee
            if (_bow != null && _sleeve != null) _bow.color = _sleeve.color;
        }
    }

    /// <summary>
    /// Plays Idle / Walk / Run on the kid, blended by how fast the model moves.
    /// Flinch(): Mama got them. Arms up over the face, then they fall flat on their back.
    /// </summary>
    public class KidAnimator : MonoBehaviour
    {
        public Transform[] Bones;
        internal ChildModel.Data Model;
        private Vector3 _last, _hipsRest;
        private float _speed, _phase, _idleT, _scaredT;
        private float _flinchAt = -1f;

        private const float FALL_DELAY = 0.75f, FALL_TIME = 0.45f, FALL_ANGLE = 84f;

        /// <summary>0..1: knees up and arms in, as in a jump (Recalled's dodge roll asks for it).</summary>
        public float Tuck;

        public void Flinch() { if (_flinchAt < 0f) _flinchAt = Time.time; }
        public void ResetFlinch() { _flinchAt = -1f; _scareUntil = 0f; }

        private float _scareUntil;
        /// <summary>Frozen in fear for a moment (Mama's Gaze): cowering and trembling, no fall.</summary>
        public void Scare(float seconds) { _scareUntil = Mathf.Max(_scareUntil, Time.time + seconds); }
        public bool Scared { get { return _flinchAt >= 0f || Time.time < _scareUntil; } }

        private const float WALK_STRIDE = 0.9f; // meters per walk cycle (two steps)
        private const float RUN_STRIDE = 1.7f;
        private const float RUN_FROM = 2.8f, RUN_FULL = 4.2f; // m/s
        private const float KID_HIP_HEIGHT = 0.55f;             // the strides above are a kid's

        private void Start()
        {
            if (Bones == null || Model == null) return;
            _hipsRest = Bones[Model.HipsBone].localPosition;
            // The bones a jump bends: knees up, arms up.
            var list = new System.Collections.Generic.List<int>();
            var angle = new System.Collections.Generic.List<float>();
            for (int i = 0; i < Bones.Length; i++)
            {
                string n = Bones[i].name;
                if (n.StartsWith("upperleg")) { list.Add(i); angle.Add(-40f); }
                else if (n.StartsWith("lowerleg")) { list.Add(i); angle.Add(70f); }
                else if (n.StartsWith("upperarm")) { list.Add(i); angle.Add(-55f); }
            }
            _jumpBones = list.ToArray();
            _jumpAngles = angle.ToArray();
        }

        private void OnEnable() { _last = transform.position; _lastY = transform.position.y; _speed = 0f; _vy = 0f; _air = 0f; }

        // In the air (jumping or falling: the body moves up or down fast): tucked legs, arms up.
        private int[] _jumpBones;
        private float[] _jumpAngles;
        private float _lastY, _vy, _air;
        private const float AIR_SPEED = 1.5f; // m/s up or down; walking down the stairs is slower

        private void LateUpdate()
        {
            var m = Model;
            if (Bones == null || m == null || Time.deltaTime <= 0f) return;
            Vector3 d = transform.position - _last;
            d.y = 0f;
            _last = transform.position;
            _speed = Mathf.Lerp(_speed, d.magnitude / Time.deltaTime, Mathf.Clamp01(Time.deltaTime * 6f));

            float walkW = Mathf.Clamp01(_speed / 0.6f);
            float runW = Mathf.Clamp01((_speed - RUN_FROM) / (RUN_FULL - RUN_FROM));
            _idleT += Time.deltaTime / m.Idle.Period;
            float legs = m.HipHeight > 0.1f ? m.HipHeight / KID_HIP_HEIGHT : 1f; // longer legs, longer steps
            _phase += Time.deltaTime * Mathf.Max(1f / m.Walk.Period, _speed / (Mathf.Lerp(WALK_STRIDE, RUN_STRIDE, runW) * legs)) * walkW;

            // Caught: blend into the scared pose, then tip over backwards around the feet.
            float scaredW = 0f, fall = 0f;
            if (_flinchAt >= 0f && m.Scared != null)
            {
                float ft = Time.time - _flinchAt;
                _scaredT += Time.deltaTime / m.Scared.Period;
                scaredW = Mathf.Clamp01(ft / 0.15f);
                float k = Mathf.Clamp01((ft - FALL_DELAY) / FALL_TIME);
                fall = FALL_ANGLE * k * k; // speeds up like a real fall
                float landed = ft - FALL_DELAY - FALL_TIME;
                if (landed > 0f) fall -= 7f * Mathf.Sin(Mathf.Clamp01(landed / 0.3f) * Mathf.PI); // a little bounce
            }
            else if (Time.time < _scareUntil && m.Scared != null)
            {
                _scaredT += Time.deltaTime / m.Scared.Period;
                scaredW = 0.7f;
            }

            for (int b = 0; b < Bones.Length; b++)
            {
                Quaternion moving = Quaternion.Slerp(Rot(m.Walk, _phase, b), Rot(m.Run, _phase, b), runW);
                Quaternion q = Quaternion.Slerp(Rot(m.Idle, _idleT, b), moving, walkW);
                if (scaredW > 0f) q = Quaternion.Slerp(q, Rot(m.Scared, _scaredT, b), scaredW);
                Bones[b].localRotation = q;
            }
            Vector3 hipsMove = Vector3.Lerp(Hips(m.Walk, _phase), Hips(m.Run, _phase), runW);
            Vector3 hips = _hipsRest + Vector3.Lerp(Hips(m.Idle, _idleT), hipsMove, walkW);
            if (scaredW > 0f) hips = Vector3.Lerp(hips, _hipsRest + Hips(m.Scared, _scaredT), scaredW);
            if (_flinchAt < 0f && Time.time < _scareUntil) hips += Random.insideUnitSphere * 0.004f; // trembling
            float vy = (transform.position.y - _lastY) / Time.deltaTime;
            _lastY = transform.position.y;
            _vy = Mathf.Lerp(_vy, vy, Mathf.Clamp01(Time.deltaTime * 12f));
            _air = Mathf.MoveTowards(_air, Mathf.Abs(_vy) > AIR_SPEED && _flinchAt < 0f ? 1f : 0f, Time.deltaTime * 7f);
            float bend = Mathf.Max(_air, Tuck);
            if (bend > 0f && _jumpBones != null)
                for (int j = 0; j < _jumpBones.Length; j++)
                    Bones[_jumpBones[j]].localRotation *= Quaternion.Euler(_jumpAngles[j] * bend, 0f, 0f);

            Quaternion tilt = Quaternion.AngleAxis(-fall, Vector3.right);
            Transform h = Bones[m.HipsBone];
            h.localPosition = tilt * hips;
            h.localRotation = tilt * h.localRotation;
        }

        private int Frame(ChildModel.Clip c, float t, out int next, out float k)
        {
            float f = (t - Mathf.Floor(t)) * c.Frames;
            int i = Mathf.Min((int)f, c.Frames - 1);
            next = (i + 1) % c.Frames;
            k = f - i;
            return i;
        }

        private Quaternion Rot(ChildModel.Clip c, float t, int b)
        {
            int next; float k;
            int i = Frame(c, t, out next, out k);
            int n = Bones.Length;
            return Quaternion.Slerp(c.Rots[i * n + b], c.Rots[next * n + b], k);
        }

        private Vector3 Hips(ChildModel.Clip c, float t)
        {
            int next; float k;
            int i = Frame(c, t, out next, out k);
            return Vector3.Lerp(c.Hips[i], c.Hips[next], k);
        }
    }

    /// <summary>Spins something slowly in place (the outfit preview's kid).</summary>
    public class Turntable : MonoBehaviour
    {
        public float DegreesPerSecond = 35f;
        private void Update() { transform.Rotate(0f, DegreesPerSecond * Time.unscaledDeltaTime, 0f, Space.Self); }
    }

    /// <summary>
    /// Dev builds only: K in the Arcade puts your kid in front of you that walks, then runs,
    /// then stands, in a loop. K again removes it. J makes it flinch and fall (J again: stand up).
    /// </summary>
    public class KidPreviewTool : MonoBehaviour
    {
        private GameObject _kid;
        private Vector3 _center;
        private float _t, _angle;
        private bool _fallen;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                if (_kid != null) { Destroy(_kid); _kid = null; return; }
                var cam = Camera.main;
                if (cam == null) return;
                Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
                _center = cam.transform.position + fwd * 3f;
                RaycastHit hit;
                if (Physics.Raycast(_center + Vector3.up, Vector3.down, out hit, 5f, ~0, QueryTriggerInteraction.Ignore)) _center.y = hit.point.y;
                _kid = ChildModel.Build("KE_KidPreview", KidSkins.Gender, KidSkins.Selected);
                _t = 0f; _angle = 0f; _fallen = false;
            }
            if (Input.GetKeyDown(KeyCode.O)) ModelExport.Papa(); // dev builds: Papa to Documents\Tattletail After Hours\export\papa (.obj + textures)
            if (_kid == null) return;
            if (Input.GetKeyDown(KeyCode.J))
            {
                var anim = _kid.GetComponent<KidAnimator>();
                _fallen = !_fallen;
                if (_fallen) anim.Flinch(); else anim.ResetFlinch();
            }
            if (_fallen) return; // stays where it fell

            // 6 s walking, 4 s running, 3 s standing still.
            _t = (_t + Time.deltaTime) % 13f;
            float speed = _t < 6f ? 1.4f : _t < 10f ? 4.5f : 0f;
            const float RADIUS = 1.5f;
            _angle += speed / RADIUS * Time.deltaTime;
            Vector3 offset = new Vector3(Mathf.Sin(_angle), 0f, Mathf.Cos(_angle)) * RADIUS;
            _kid.transform.position = _center + offset;
            _kid.transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(_angle), 0f, -Mathf.Sin(_angle)));
        }

        private void OnDestroy() { if (_kid != null) Destroy(_kid); }
    }
}
