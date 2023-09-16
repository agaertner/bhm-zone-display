using Blish_HUD.Gw2Mumble;
using Microsoft.Xna.Framework;
using System;

namespace Nekres.Regions_Of_Tyria {
    public static class PlayerCharacterExtensions {

        private static Vector3 _prevPosition;
        private static float   _prevSpeed;
        private static DateTime _prevUpdate = DateTime.UtcNow;
        public static float GetSpeed(this PlayerCharacter player, GameTime gameTime) {
            if (DateTime.UtcNow.Subtract(_prevUpdate).TotalMilliseconds < 40) {
                return _prevSpeed;
            }
            _prevUpdate = DateTime.UtcNow;

            var currentPosition = player.Position;

            var speed = Vector3.Distance(currentPosition, _prevPosition) / (float)gameTime.ElapsedGameTime.TotalSeconds;

            _prevPosition = currentPosition;
            _prevSpeed    = speed;
            return speed;
        }
    }
}
