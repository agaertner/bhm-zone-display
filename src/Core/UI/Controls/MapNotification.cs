using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
namespace Nekres.Regions_Of_Tyria.UI.Controls {
    internal sealed class MapNotification : Container
    {
        public enum RevealEffect {
            Decode,
            Dissolve
        }

        private const int TOP_MARGIN     = 20;
        private const int STROKE_DIST    = 1;
        private const int UNDERLINE_SIZE = 1;

        private static readonly Color _brightGold;
        private static readonly Color _darkGold;

        private const int NOTIFICATION_COOLDOWN_MS = 2000;
        private static DateTime _lastNotificationTime;
        
        private static readonly SynchronizedCollection<MapNotification> _activeMapNotifications;

        private static BitmapFont _krytanFont;
        private static BitmapFont _krytanFontSmall;
        internal static BitmapFont TitlingFont;
        internal static BitmapFont TitlingFontSmall;

        private static SpriteBatchParameters _defaultParams;

        static MapNotification()
        {
            _lastNotificationTime = DateTime.UtcNow;
            _activeMapNotifications = new SynchronizedCollection<MapNotification>();

            _defaultParams = new SpriteBatchParameters();

            _brightGold = new Color(223, 194, 149, 255);
            _darkGold = new Color(168, 150,  135, 255);
        }

        public static void UpdateFonts(float fontSize = 0.92f) {
            var size = (int)Math.Round((fontSize + 0.35f) * 37);
            _krytanFont      = RegionsOfTyria.Instance.ContentsManager.GetBitmapFont("fonts/NewKrytan.ttf", size + 10);
            _krytanFontSmall = RegionsOfTyria.Instance.ContentsManager.GetBitmapFont("fonts/NewKrytan.ttf", size - 2, 30);

            TitlingFont      = RegionsOfTyria.Instance.ContentsManager.GetBitmapFont("fonts/StoweTitling.ttf", size);
            TitlingFontSmall = RegionsOfTyria.Instance.ContentsManager.GetBitmapFont("fonts/StoweTitling.ttf", size - 12);
        } 

        public static void ShowNotification(string header, string footer, Texture2D icon = null, float showDuration = 4, float fadeInDuration = 2, float fadeOutDuration = 2, float effectDuration = 0.85f) {
            if (DateTime.UtcNow.Subtract(_lastNotificationTime).TotalMilliseconds < NOTIFICATION_COOLDOWN_MS) {
                return;
            }

            _lastNotificationTime = DateTime.UtcNow;

            var nNot = new MapNotification(header, footer, showDuration, fadeInDuration, fadeOutDuration, effectDuration) {
                Parent = Graphics.SpriteScreen
            };

            nNot.ZIndex = _activeMapNotifications.DefaultIfEmpty(nNot).Max(n => n.ZIndex) + 1;

            foreach (var activeScreenNotification in _activeMapNotifications) {
                activeScreenNotification.SlideDown((int)(TitlingFontSmall.LineHeight + TitlingFont.LineHeight + TOP_MARGIN * 1.05f));
            }

            _activeMapNotifications.Add(nNot);

            nNot.Show();
        }

        private string _header;
        private string _text;
        private float  _showDuration;
        private float  _fadeInDuration;
        private float  _fadeOutDuration;
        private float  _effectDuration;

        // ReSharper disable once NotAccessedField.Local
        #pragma warning disable IDE0052 // Remove unread private members
        private Glide.Tween _animFadeLifecycle;
        private int _targetTop;

        private SpriteBatchParameters _dissolve;
        private SpriteBatchParameters _reveal;

        private float _amount = 0.0f;

        private MapNotification(string header, string text, float showDuration = 4, float fadeInDuration = 2, float fadeOutDuration = 2, float effectDuration = 0.85f) {
            _showDuration    = showDuration;
            _fadeInDuration  = fadeInDuration;
            _fadeOutDuration = fadeOutDuration;
            _effectDuration  = effectDuration;
            _text            = text;
            _header          = header;
            ClipsBounds      = true;
            Opacity          = 0f;
            Size             = new Point(GameService.Graphics.SpriteScreen.Width, GameService.Graphics.SpriteScreen.Height);
            ZIndex           = Screen.MENUUI_BASEINDEX;

            _targetTop = Top;

            _dissolve = new SpriteBatchParameters {
                Effect = RegionsOfTyria.Instance.ContentsManager.GetEffect("effects/dissolve.mgfx")
            };
            _reveal = new SpriteBatchParameters {
                Effect = RegionsOfTyria.Instance.ContentsManager.GetEffect("effects/dissolve.mgfx")
            };

            var burnColor = new Vector4(0.5f, 0.25f, 0.0f, 0.5f);
            _dissolve.Effect.Parameters["Amount"].SetValue(0.0f);
            _dissolve.Effect.Parameters["GlowColor"].SetValue(burnColor);
            _dissolve.Effect.Parameters["Slide"].SetValue(true);
            _reveal.Effect.Parameters["Amount"].SetValue(1.0f);
            _reveal.Effect.Parameters["GlowColor"].SetValue(burnColor);
            _reveal.Effect.Parameters["Slide"].SetValue(true);
            //_reveal.Effect.Parameters["Glow"].SetValue(false);

            GameService.Graphics.SpriteScreen.Resized += UpdateLocation;
        }

        private void UpdateLocation(object o, ResizedEventArgs e)
        {
            this.Size = new Point(GameService.Graphics.SpriteScreen.Width, GameService.Graphics.SpriteScreen.Height);
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

            var slide = RegionsOfTyria.Instance.RevealEffect.Value == RevealEffect.Decode;
            _dissolve.Effect.Parameters["Slide"].SetValue(slide);
            _reveal.Effect.Parameters["Slide"].SetValue(slide);
            
            _dissolve.Effect.Parameters["Opacity"].SetValue(this.Opacity);
            _reveal.Effect.Parameters["Opacity"].SetValue(this.Opacity);

            _dissolve.Effect.Parameters["Amount"].SetValue(_amount);
            _reveal.Effect.Parameters["Amount"].SetValue(1.0f - _amount);

            spriteBatch.End();

            if (RegionsOfTyria.Instance.Translate.Value) {
                spriteBatch.Begin(_dissolve);
                PaintText(this, spriteBatch, bounds, _krytanFont, _krytanFontSmall, false, _header, _text);
                spriteBatch.End();
            }
            
            spriteBatch.Begin(_reveal);
            PaintText(this, spriteBatch, bounds, TitlingFont, TitlingFontSmall, false, _header, _text);
            spriteBatch.End();
            spriteBatch.Begin(_defaultParams);
        }

        internal static void PaintText(Control ctrl, SpriteBatch spriteBatch, Rectangle bounds, BitmapFont font, BitmapFont smallFont, bool underline, string header, string text) {
            var       height = (int)(RegionsOfTyria.Instance.VerticalPosition.Value / 100f * bounds.Height);
            Rectangle rect;

            if (!string.IsNullOrEmpty(header) && !header.Equals(text, StringComparison.InvariantCultureIgnoreCase)) {

                var str  = header.Wrap();
                var size       = smallFont.MeasureString(str);
                var lineWidth  = (int)size.Width;
                var lineHeight = (int)size.Height;

                rect   =  new Rectangle(0, TOP_MARGIN + height, bounds.Width, bounds.Height);
                height += smallFont.LineHeight;
                spriteBatch.DrawStringOnCtrl(ctrl, str, smallFont, rect, _darkGold, false, true, STROKE_DIST, HorizontalAlignment.Center, VerticalAlignment.Top);

                if (underline) {
                    rect = new Rectangle((bounds.Width - (lineWidth + 2)) / 2, rect.Y + lineHeight + 5, lineWidth + 2, UNDERLINE_SIZE + 2);
                    spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, rect, Color.Black * 0.8f);
                    rect = new Rectangle(rect.X + 1, rect.Y + 1, lineWidth, UNDERLINE_SIZE);
                    spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, rect, _darkGold);
                }

                height += TOP_MARGIN;
            }

            if (!string.IsNullOrEmpty(text)) {
                rect   =  new Rectangle(0, TOP_MARGIN + height, bounds.Width, bounds.Height);
                spriteBatch.DrawStringOnCtrl(ctrl, text.Wrap(), font, rect, _brightGold, false, true, STROKE_DIST, HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }

        /// <inheritdoc />
        public override void Show() {
            //Nesting instead so we are able to set a different duration per fade direction.
            _animFadeLifecycle = Animation.Tweener
                .Tween(this, new { Opacity = 1f }, _fadeInDuration)
                   .OnComplete(() => {
                       _animFadeLifecycle = Animation.Tweener.Tween(this, new { _amount = 1f }, _effectDuration)
                       .OnComplete(() => {
                           _animFadeLifecycle = Animation.Tweener.Tween(this, new { Opacity = 1f }, _showDuration)
                           .OnComplete(() => { 
                               _animFadeLifecycle = Animation.Tweener.Tween(this, new { Opacity = 0f }, _fadeOutDuration)
                               .OnComplete(Dispose);
                           });
                       });
                   });
            base.Show();
        }

        private void SlideDown(int distance) {
            _targetTop += distance;

            if (_opacity < 1) {
                return;
            }

            Animation.Tweener.Tween(this, new { Top = _targetTop }, _fadeOutDuration);

            _animFadeLifecycle = Animation.Tweener
                                          .Tween(this, new { Opacity = 0f }, _fadeOutDuration)
                                          .OnComplete(Dispose);
        }

        /// <inheritdoc />
        protected override void DisposeControl() {
            _reveal.Effect?.Dispose();
            _dissolve.Effect?.Dispose();

            _activeMapNotifications.Remove(this);
            GameService.Graphics.SpriteScreen.Resized -= UpdateLocation;

            base.DisposeControl();
        }
    }
}
