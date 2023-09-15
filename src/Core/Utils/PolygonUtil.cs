using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Nekres.Regions_Of_Tyria {
    public static class PolygonUtil {
        public static bool IsPointInsidePolygon(Point targetPoint, List<Point> polygon) {
            if (polygon.Count < 3) {
                // A polygon must have at least 3 vertices to be valid.
                return false;
            }

            double x        = targetPoint.X;
            double y        = targetPoint.Y;
            bool   isInside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++) {
                if ((polygon[i].Y > y) != (polygon[j].Y > y) &&
                    x < (polygon[j].X - polygon[i].X) * (y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X) {
                    isInside = !isInside;
                }
            }

            return isInside;
        }
    }
}
