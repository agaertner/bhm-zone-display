using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Nekres.Regions_Of_Tyria.Geometry {
    public class Sector
    {
        public readonly int Id;

        public readonly string Name;

        public readonly List<Point> Bounds;

        public Sector(ContinentFloorRegionMapSector sector)
        {
            Id     = sector.Id;
            Name   = sector.Name;
            Bounds = sector.Bounds.Select(b => b.ToPoint()).ToList();
        }
    }
}
