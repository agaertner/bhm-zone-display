using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Extended;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
namespace Nekres.Regions_Of_Tyria.UI.Controls {
    internal sealed class ControlPositionIndicator : Container
    {
        public ControlPositionIndicator()
        {
            Size                                      =  new Point(GameService.Graphics.SpriteScreen.Width, GameService.Graphics.SpriteScreen.Height);
            Location                                  =  new Point((GameService.Graphics.SpriteScreen.Width - 500) / 2, 0);
            ZIndex                                    =  Screen.MENUUI_BASEINDEX;
            ClipsBounds                               =  true;
            GameService.Graphics.SpriteScreen.Resized += UpdateLocation;
        }

        private void UpdateLocation(object o, ResizedEventArgs e)
        {
            this.Size     = new Point(GameService.Graphics.SpriteScreen.Width, GameService.Graphics.SpriteScreen.Height);
            this.Location = new Point((GameService.Graphics.SpriteScreen.Width - 500) / 2, 0);
        }

        /// <inheritdoc />
        protected override CaptureType CapturesInput()
        {
            return CaptureType.Filter;
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds) {
            var height = (int)(RegionsOfTyriaModule.ModuleInstance.VerticalPositionSetting.Value / 100 * bounds.Height);
            var rect   = new Rectangle(0, height + 12 * 2, 500, 100);

            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, rect, Color.White * 0.4f);
            spriteBatch.DrawRectangleOnCtrl(this, rect, 5, Color.White);
        }

        /// <inheritdoc />
        protected override void DisposeControl() {
            GameService.Graphics.SpriteScreen.Resized -= UpdateLocation;
            base.DisposeControl();
        }
    }
}
