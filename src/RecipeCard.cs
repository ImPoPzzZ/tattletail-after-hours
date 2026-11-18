using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Papa's 10th Birthday!: a recipe card on the kitchen wall (the user's
    /// E-mark), listing every food and its two ingredients (PartyItems).
    /// </summary>
    public static class RecipeCard
    {
        private static GameObject _card;

        // The user's E-mark on the kitchen wall: stood at (6.0, 1.1, 3.6) facing +Z,
        // hit the wall at (6.0, 1.1, 4.0) — so the card faces -Z, back toward them.
        private static readonly Vector3 WallMark = new Vector3(6.0f, 1.1f, 4.0f);
        private static readonly Vector3 OutOfWall = Vector3.back;

        public static void Spawn()
        {
            if (_card != null) Object.Destroy(_card);

            // Eye height above whatever floor is under the mark.
            RaycastHit floor;
            Vector3 probe = WallMark + OutOfWall * 0.3f;
            float floorY = Physics.Raycast(probe, Vector3.down, out floor, 4f, ~0, QueryTriggerInteraction.Ignore) ? floor.point.y : WallMark.y - 1f;

            _card = Build();
            _card.transform.position = new Vector3(WallMark.x, floorY + 1.5f, WallMark.z) + OutOfWall * 0.02f;
            _card.transform.rotation = Quaternion.LookRotation(OutOfWall); // local +Z = out of the wall
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Recipe card on the kitchen wall at " + _card.transform.position + ".");
        }

        private static GameObject Build()
        {
            var root = new GameObject("KE_RecipeCard");
            const float W = 0.9f, H = 0.75f;

            var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.transform.SetParent(root.transform, false);
            paper.transform.localPosition = new Vector3(0f, 0f, 0.005f);
            paper.transform.localScale = new Vector3(W, H, 0.01f);
            paper.GetComponent<Renderer>().material = Mat.Lit(new Color(0.8f, 0.77f, 0.68f), 0.1f); // lit like the room — unlit glowed
            Object.Destroy(paper.GetComponent<Collider>());

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.transform.SetParent(root.transform, false);
            stripe.transform.localPosition = new Vector3(0f, H / 2f - 0.07f, 0.012f);
            stripe.transform.localScale = new Vector3(W, 0.14f, 0.004f);
            stripe.GetComponent<Renderer>().material = Mat.Lit(new Color(0.6f, 0.12f, 0.3f), 0.1f);
            Object.Destroy(stripe.GetComponent<Collider>());

            Text(root.transform, "PARTY RECIPES", new Color(0.85f, 0.82f, 0.78f), 0.07f, TextAnchor.MiddleCenter, new Vector3(0f, H / 2f - 0.07f, 0.016f), W - 0.05f);

            string lines = "";
            foreach (string food in PartyItems.FoodNames)
            {
                string[] r = PartyItems.RecipeFor(food);
                if (r == null) continue;
                lines += (lines.Length > 0 ? "\n" : "") + food + "  =  " + r[0] + " + " + r[1];
            }
            Text(root.transform, lines, new Color(0.15f, 0.12f, 0.1f), 0.045f, TextAnchor.MiddleLeft, new Vector3(0f, -0.07f, 0.012f), W - 0.08f);
            return root;
        }

        private static void Text(Transform parent, string text, Color color, float lineHeight, TextAnchor anchor, Vector3 pos, float width)
        {
            var t = WorldText.Create(parent, text, color, lineHeight, anchor, width);
            t.canvas.transform.localPosition = pos;
            t.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // world UI reads from behind its forward
        }
    }
}
