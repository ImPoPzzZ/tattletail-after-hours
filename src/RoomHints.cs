using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Rough "where is it" hints for hidden objectives ("Look in: Kitchen,
    /// Garage"). Rooms come from the house's own HouseLight components,
    /// each tagged with the room it lights: a spot belongs to the nearest
    /// light on the same floor (the game's basement split is y = -0.2,
    /// MamaManager.IsAboveGround). Rough on purpose, near a doorway it can
    /// name the room next door.
    /// </summary>
    public static class RoomHints
    {
        private static HouseLight[] _lights;

        public static string RoomOf(Vector3 pos)
        {
            if (_lights == null || _lights.Length == 0 || _lights[0] == null)
            {
                var all = new List<HouseLight>();
                foreach (var l in Resources.FindObjectsOfTypeAll<HouseLight>())
                    if (l != null && l.gameObject.scene.IsValid()) all.Add(l); // real scene lights, lit or not
                _lights = all.ToArray();
            }

            bool above = pos.y > -0.2f;
            HouseLight best = null;
            float bestDist = float.MaxValue;
            foreach (var l in _lights)
            {
                if (l == null) continue;
                Vector3 lp = l.transform.position;
                if ((lp.y > -0.2f) != above) continue;
                float d = new Vector2(lp.x - pos.x, lp.z - pos.z).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = l; }
            }
            return best != null ? Name(best.lightType) : null;
        }

        /// <summary>"\nLook in: Kitchen, Garage" for the given spots (each room once), or "" if none are known.</summary>
        public static string LookIn(List<Vector3> spots)
        {
            if (VersusMode.Role != VersusRole.None) return ""; // Versus: the child has to search (Mama is hunting them)
            if (FactoryMode.Active) return ""; // (the hints name the house's rooms)
            var rooms = new List<string>();
            foreach (var p in spots)
            {
                string r = RoomOf(p);
                if (r != null && !rooms.Contains(r)) rooms.Add(r);
            }
            return rooms.Count == 0 ? "" : "\nLook in: " + string.Join(", ", rooms.ToArray());
        }

        private static string Name(HouseManager.LightType t)
        {
            switch (t)
            {
                case HouseManager.LightType.KidBedroom: return "Your Bedroom";
                case HouseManager.LightType.ParentBedroom: return "Parents' Bedroom";
                case HouseManager.LightType.Bathroom: return "Bathroom";
                case HouseManager.LightType.Foyer: return "Foyer";
                case HouseManager.LightType.LivingRoom:
                case HouseManager.LightType.ChristmasTree: return "Living Room";
                case HouseManager.LightType.DiningRoom: return "Dining Room";
                case HouseManager.LightType.KitchenFluorescent:
                case HouseManager.LightType.KitchenHanging: return "Kitchen";
                case HouseManager.LightType.Porch: return "Porch";
                case HouseManager.LightType.Garage: return "Garage";
                case HouseManager.LightType.BasementSteps: return "Basement Stairs";
                case HouseManager.LightType.BasementLaundry: return "Laundry Room";
                default: return "Basement"; // main room, hallway, tattletail/flashlight rooms, pentagram
            }
        }
    }
}
