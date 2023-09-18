using Gw2Sharp.WebApi.V2.Models;
using System.Collections.Generic;
using System.Linq;

namespace Nekres.Regions_Of_Tyria.Geometry {
    public class Sector
    {
        public readonly int Id;

        public readonly string Name;

        private readonly IReadOnlyList<Point> _bounds;

        public Sector(ContinentFloorRegionMapSector sector)
        {
            Id     = sector.Id;
            Name   = sector.Name;
            _bounds = sector.Bounds.Select(b => new Point(b.X, b.Y)).ToList();
        }

        public bool Contains(double x, double y) {
            return Contains(new Point(x, y), _bounds);
        }

        private bool Contains(Point targetPoint, IReadOnlyList<Point> polygon) {
            if (polygon.Count < 3) {
                // Must have at least 3 vertices to be valid.
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

        private struct Point {

            public readonly double X;
            public readonly double Y;

            public Point(double x, double y) {
                X = x;
                Y = y;
            }
        }
    }
}
