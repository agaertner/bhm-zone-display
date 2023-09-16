using Gw2Sharp.Models;
using Microsoft.Xna.Framework;
using System;
using Rectangle = Gw2Sharp.WebApi.V2.Models.Rectangle;

namespace Nekres.Regions_Of_Tyria {
    internal static class CoordinatesExtensions
    {
        private const float INCH_TO_METER = 0.0254F;

        public static Coordinates3 SwapYz(this Coordinates3 coords) {
            return new Coordinates3(coords.X, coords.Z, coords.Y);
        }

        public static Coordinates2 ToPlane(this Coordinates3 coords) {
            return new Coordinates2(coords.X, coords.Y);
        }

        public static Coordinates3 ToUnit(this Coordinates3 coords, CoordsUnit fromUnit, CoordsUnit toUnit) {
            return fromUnit switch {
                CoordsUnit.METERS when toUnit == CoordsUnit.INCHES => new Coordinates3(coords.X / INCH_TO_METER, coords.Y / INCH_TO_METER, coords.Z / INCH_TO_METER),
                CoordsUnit.INCHES when toUnit == CoordsUnit.METERS => new Coordinates3(coords.X * INCH_TO_METER, coords.Y * INCH_TO_METER, coords.Z * INCH_TO_METER),
                _                                                  => coords
            };
        }

        public static Coordinates3 ToMapCoords(this Coordinates3 coords, CoordsUnit fromUnit)
        {
            coords = coords.ToUnit(fromUnit, CoordsUnit.GAME_WORLD);
            return new Coordinates3(coords.X, coords.Y, coords.Z);
        }

        public static Coordinates3 ToContinentCoords(this Coordinates3 coords, CoordsUnit fromUnit, Rectangle mapRectangle, Rectangle continentRectangle)
        {
            var    mapCoords = coords.ToMapCoords(fromUnit);
            double x         = (mapCoords.X - mapRectangle.TopLeft.X) / mapRectangle.Width            * continentRectangle.Width  + continentRectangle.TopLeft.X;
            double z         = (1 - (mapCoords.Z - mapRectangle.BottomRight.Y) / mapRectangle.Height) * continentRectangle.Height + continentRectangle.TopRight.Y;
            return new Coordinates3(x, mapCoords.Y, z);
        }

        public static Point ToPoint(this Coordinates2 coords) {
            return new Point((int)Math.Round(coords.X), (int)Math.Round(coords.Y));
        }

        public static Vector2 ToVector2(this Coordinates2 coords) {
            return new Vector2((float)coords.X, (float)coords.Y);
        }
    }

    public enum CoordsUnit
    {
        INCHES,
        GAME_WORLD = INCHES,

        METERS,
        MUMBLE = METERS
    }
}
