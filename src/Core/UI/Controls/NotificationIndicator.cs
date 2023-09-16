using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
namespace Nekres.Regions_Of_Tyria.UI.Controls {
    internal sealed class NotificationIndicator : Container
    {
        private string _header;
        private string _text;

        public NotificationIndicator(string header, string text) {
            _header = header;
            _text   = text;

            Size        = new Point(GameService.Graphics.SpriteScreen.Width, GameService.Graphics.SpriteScreen.Height);
            ZIndex      = Screen.MENUUI_BASEINDEX;
            ClipsBounds = true;

            GameService.Graphics.SpriteScreen.Resized += UpdateLocation;
        }

        private void UpdateLocation(object o, ResizedEventArgs e)
        {
            this.Size     = new Point(GameService.Graphics.SpriteScreen.Width, GameService.Graphics.SpriteScreen.Height);
            this.Location = new Point(0, 0);
        }

        /// <inheritdoc />
        protected override CaptureType CapturesInput()
        {
            return CaptureType.Filter;
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds) {
            if (RegionsOfTyria.Instance == null) {
                return;
            }

            MapNotification.PaintText(this, spriteBatch, bounds, MapNotification.TitlingFont, MapNotification.TitlingFontSmall,false, _header, _text);
        }

        /// <inheritdoc />
        protected override void DisposeControl() {
            GameService.Graphics.SpriteScreen.Resized -= UpdateLocation;
            base.DisposeControl();
        }
    }
}
