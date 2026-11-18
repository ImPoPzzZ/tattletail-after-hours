using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Raycasts that see through the local player's own body. Finding "the floor" with a plain
    /// raycast hits the player's capsule when it's in the way: the other player's kid ended up
    /// standing on your head when you walked into them, and a Toy Parrot aimed at your feet
    /// landed on top of you.
    /// </summary>
    public static class WorldRays
    {
        public static bool Raycast(Vector3 origin, Vector3 dir, float dist, out RaycastHit hit)
        {
            hit = new RaycastHit();
            var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsPlayer(hits[i].collider) || hits[i].distance >= best) continue;
                best = hits[i].distance;
                hit = hits[i];
                found = true;
            }
            return found;
        }

        public static bool Linecast(Vector3 from, Vector3 to)
        {
            RaycastHit hit;
            Vector3 d = to - from;
            return Raycast(from, d.normalized, d.magnitude, out hit);
        }

        /// <summary>
        /// Where a player's feet are, from their controller's position (what the pose packets carry):
        /// on the floor when they stand, in the air when they jump or fall. Every player has the same
        /// controller, so this game's own capsule gives the height.
        /// </summary>
        public static Vector3 FeetOf(Vector3 controllerPos)
        {
            float drop = 1f;
            var fps = RM.fpsController;
            var cc = fps != null ? fps.GetComponent<CharacterController>() : null;
            if (cc != null) drop = (cc.height * 0.5f - cc.center.y) * fps.transform.lossyScale.y;
            Vector3 feet = controllerPos - Vector3.up * drop;
            RaycastHit hit;
            if (Raycast(controllerPos, Vector3.down, drop + 0.3f, out hit) && feet.y - hit.point.y < 0.25f) feet.y = hit.point.y; // standing: exactly on the floor
            return feet;
        }

        private static bool IsPlayer(Collider c)
        {
            var fps = RM.fpsController;
            return fps != null && c != null && c.transform.IsChildOf(fps.transform);
        }
    }
}
