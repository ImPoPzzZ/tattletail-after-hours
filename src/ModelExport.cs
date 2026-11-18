using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Dev builds only: writes a model in the scene out as a Wavefront .obj (+ .mtl and the textures as .png), for the
    /// team's 3D tools. Skinned meshes are baked in the pose they're standing in.
    /// </summary>
    public static class ModelExport
    {
        public static string Folder(string name)
        {
            string docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(Path.Combine(docs, "Tattletail After Hours"), "export"), name);
        }

        /// <summary>Papa (the game's Mama model in his party outfit), built fresh and written to Documents\Tattletail After Hours\export\papa.</summary>
        public static void Papa()
        {
            GameObject papa = null;
            try
            {
                papa = VersusHunters.BuildBody("KE_PapaExport", VersusHunters.CODE_BASE + 100);
                if (papa == null) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Export] No Papa: this level has no model library."); return; }
                papa.transform.position = Vector3.zero;
                papa.transform.rotation = Quaternion.identity;
                string msg = Export(papa, Folder("papa"), "papa");
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Export] " + msg);
                HubMode.SetHudText(msg);
            }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Export] Papa failed: " + e); }
            finally { if (papa != null) Object.Destroy(papa); }
        }

        public static string Export(GameObject root, string dir, string name)
        {
            Directory.CreateDirectory(dir);
            var obj = new StringBuilder();
            var mtl = new StringBuilder();
            obj.Append("# Tattletail: After Hours model export\nmtllib ").Append(name).Append(".mtl\n");
            var done = new Dictionary<Material, string>();
            var texDone = new Dictionary<Texture, string>();
            int offset = 1, meshes = 0, skipped = 0;
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Mesh m = null;
                Matrix4x4 world = r.transform.localToWorldMatrix;
                var skinned = r as SkinnedMeshRenderer;
                try
                {
                    if (skinned != null)
                    {
                        m = new Mesh();
                        skinned.BakeMesh(m);
                        world = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one); // (a baked mesh has no scale of its own)
                    }
                    else
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf != null) m = mf.sharedMesh;
                    }
                }
                catch (System.Exception) { m = null; }
                if (m == null || m.vertexCount == 0) { skipped++; continue; }
                Vector3[] v, n;
                Vector2[] uv;
                try { v = m.vertices; n = m.normals; uv = m.uv; }
                catch (System.Exception) { skipped++; continue; }
                if (v == null || v.Length == 0) { skipped++; continue; }
                Matrix4x4 to = toRoot * world;
                obj.Append("o ").Append(Clean(r.gameObject.name)).Append(meshes).Append('\n');
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 p = to.MultiplyPoint3x4(v[i]);
                    obj.Append("v ").Append(F(-p.x)).Append(' ').Append(F(p.y)).Append(' ').Append(F(p.z)).Append('\n');
                }
                for (int i = 0; i < v.Length; i++)
                {
                    Vector2 t = uv != null && i < uv.Length ? uv[i] : Vector2.zero;
                    obj.Append("vt ").Append(F(t.x)).Append(' ').Append(F(t.y)).Append('\n');
                }
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 nn = n != null && i < n.Length ? to.MultiplyVector(n[i]).normalized : Vector3.up;
                    obj.Append("vn ").Append(F(-nn.x)).Append(' ').Append(F(nn.y)).Append(' ').Append(F(nn.z)).Append('\n');
                }
                Material[] mats = r.sharedMaterials;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    Material mat = mats != null && mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null;
                    obj.Append("usemtl ").Append(MaterialName(mat, done, mtl, texDone, dir)).Append('\n');
                    int[] tris = m.GetTriangles(s);
                    for (int i = 0; i + 2 < tris.Length; i += 3)
                    {
                        int a = tris[i] + offset, b = tris[i + 1] + offset, c = tris[i + 2] + offset;
                        // (x is mirrored for the obj's handedness, so the winding flips)
                        obj.Append("f ").Append(a).Append('/').Append(a).Append('/').Append(a).Append(' ')
                           .Append(c).Append('/').Append(c).Append('/').Append(c).Append(' ')
                           .Append(b).Append('/').Append(b).Append('/').Append(b).Append('\n');
                    }
                }
                offset += v.Length;
                meshes++;
                if (skinned != null) Object.Destroy(m);
            }
            File.WriteAllText(Path.Combine(dir, name + ".obj"), obj.ToString());
            File.WriteAllText(Path.Combine(dir, name + ".mtl"), mtl.ToString());
            return "Exported " + meshes + " meshes (" + skipped + " skipped) to " + dir;
        }

        private static string MaterialName(Material mat, Dictionary<Material, string> done, StringBuilder mtl, Dictionary<Texture, string> texDone, string dir)
        {
            if (mat == null) mat = null;
            string key;
            if (mat != null && done.TryGetValue(mat, out key)) return key;
            string name = "mat" + done.Count + "_" + (mat != null ? Clean(mat.name) : "none");
            if (mat != null) done[mat] = name; else return name;
            Color c = Color.white;
            try { if (mat.HasProperty("_Color")) c = mat.color; } catch (System.Exception) { }
            mtl.Append("newmtl ").Append(name).Append("\nKd ").Append(F(c.r)).Append(' ').Append(F(c.g)).Append(' ').Append(F(c.b)).Append("\nKa 0 0 0\nKs 0 0 0\nNs 10\nd 1\n");
            Texture tex = null;
            try { tex = mat.mainTexture; } catch (System.Exception) { }
            if (tex != null)
            {
                string file;
                if (!texDone.TryGetValue(tex, out file))
                {
                    file = Clean(tex.name) + "_" + texDone.Count + ".png";
                    try
                    {
                        File.WriteAllBytes(Path.Combine(dir, file), Png(tex));
                        texDone[tex] = file;
                    }
                    catch (System.Exception) { file = null; }
                }
                if (file != null) mtl.Append("map_Kd ").Append(file).Append('\n');
            }
            mtl.Append('\n');
            return name;
        }

        /// <summary>A texture as a PNG even when it isn't readable (it's drawn into a render texture and read back).</summary>
        private static byte[] Png(Texture tex)
        {
            int w = Mathf.Min(tex.width, 2048), h = Mathf.Min(tex.height, 2048);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture old = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.ARGB32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(rt);
            byte[] bytes = copy.EncodeToPNG();
            Object.Destroy(copy);
            return bytes;
        }

        private static string F(float f) { return f.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture); }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "unnamed";
            var sb = new StringBuilder();
            foreach (char ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            return sb.ToString();
        }
    }
}
