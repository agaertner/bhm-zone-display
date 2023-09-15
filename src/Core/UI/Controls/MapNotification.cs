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

        private const           int   TOP_MARGIN     = 20;
        private const           int   STROKE_DIST    = 1;
        private const           int   UNDERLINE_SIZE = 1;
        private static readonly Color _brightGold;
        private static readonly Color _darkGold;

        private const int NOTIFICATION_COOLDOWN_MS = 2000;
        private static DateTime _lastNotificationTime;
        
        private static readonly SynchronizedCollection<MapNotification> _activeMapNotifications;

        private static readonly BitmapFont _krytanFont;
        private static readonly BitmapFont _krytanFontSmall;
        private static readonly BitmapFont _titlingFont;
        private static readonly BitmapFont _titlingFontSmall;

        private static SpriteBatchParameters _defaultParams;

        static MapNotification()
        {
            _lastNotificationTime = DateTime.UtcNow;
            _activeMapNotifications = new SynchronizedCollection<MapNotification>();

            _krytanFont = RegionsOfTyria.Instance.ContentsManager.GetBitmapFont("fonts/NewKrytan.ttf", 46);
            _krytanFontSmall = RegionsOfTyria.Instance.ContentsManager.GetBitmapFont("fonts/NewKrytan.ttf", 34, 30);

            _titlingFont = RegionsOfTyria.Instance.ContentsManager.GetBitmapFont("fonts/StoweTitling.ttf", 36);
            _titlingFontSmall = RegionsOfTyria.Instance.ContentsManager.GetBitmapFont("fonts/StoweTitling.ttf", 24);

            _defaultParams = new SpriteBatchParameters();

            _brightGold = new Color(223, 194, 149, 255);
            _darkGold = new Color(168, 150,  135, 255);
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
                activeScreenNotification.SlideDown((int)(_titlingFontSmall.LineHeight + _titlingFont.LineHeight + TOP_MARGIN * 1.05f));
            }

            _activeMapNotifications.Add(nNot);

            nNot.Show();
        }

        private IEnumerable<string> _headerLines;
        private string              _header;
        private IEnumerable<string> _textLines;
        private string              _text;
        private float               _showDuration;
        private float               _fadeInDuration;
        private float               _fadeOutDuration;
        private float               _effectDuration;

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
            _textLines       = text?.Split(new[] { "<br>" }, StringSplitOptions.RemoveEmptyEntries).ForEach(x => x.Trim());
            _header          = header;
            _headerLines     = header?.Split(new[] { "<br>" }, StringSplitOptions.RemoveEmptyEntries).ForEach(x => x.Trim());
            ClipsBounds      = true;
            Opacity          = 0f;
            Size             = new Point(GameService.Graphics.SpriteScreen.Width, GameService.Graphics.SpriteScreen.Height);
            ZIndex           = Screen.MENUUI_BASEINDEX;
            Location         = new Point(0, 0);

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

            var slide = RegionsOfTyria.Instance.RevealEffectSetting.Value == RevealEffect.Decode;
            _dissolve.Effect.Parameters["Slide"].SetValue(slide);
            _reveal.Effect.Parameters["Slide"].SetValue(slide);
            
            _dissolve.Effect.Parameters["Opacity"].SetValue(this.Opacity);
            _reveal.Effect.Parameters["Opacity"].SetValue(this.Opacity);

            _dissolve.Effect.Parameters["Amount"].SetValue(_amount);
            _reveal.Effect.Parameters["Amount"].SetValue(1.0f - _amount);

            spriteBatch.End();

            if (RegionsOfTyria.Instance.TranslateSetting.Value) {
                spriteBatch.Begin(_dissolve);
                PaintText(spriteBatch, bounds, _krytanFont, _krytanFontSmall, false);
                spriteBatch.End();
            }
            
            spriteBatch.Begin(_reveal);
            PaintText(spriteBatch, bounds, _titlingFont, _titlingFontSmall, false);
            spriteBatch.End();
            spriteBatch.Begin(_defaultParams);
        }

        private void PaintText(SpriteBatch spriteBatch, Rectangle bounds, BitmapFont font, BitmapFont smallFont, bool underline) {
            var       height = (int)(RegionsOfTyria.Instance.VerticalPositionSetting.Value / 100 * bounds.Height);
            Rectangle rect;

            if (!string.IsNullOrEmpty(_header) && !_header.Equals(_text, StringComparison.InvariantCultureIgnoreCase)) {
                foreach (var headerLine in _headerLines) {
                    var size       = smallFont.MeasureString(headerLine);
                    var lineWidth  = (int)size.Width;
                    var lineHeight = (int)size.Height;
                    rect   =  new Rectangle(0, TOP_MARGIN + height, bounds.Width, bounds.Height);
                    height += smallFont.LineHeight;
                    spriteBatch.DrawStringOnCtrl(this, headerLine, smallFont, rect, _darkGold, false, true, STROKE_DIST, HorizontalAlignment.Center, VerticalAlignment.Top);

                    if (underline) {
                        rect = new Rectangle((bounds.Width - (lineWidth + 2)) / 2, rect.Y + lineHeight + 5, lineWidth + 2, UNDERLINE_SIZE + 2);
                        spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, rect, Color.Black * 0.8f);
                        rect = new Rectangle(rect.X + 1, rect.Y + 1, lineWidth, UNDERLINE_SIZE);
                        spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, rect, _darkGold);
                    }
                }

                height += TOP_MARGIN;
            }

            if (!string.IsNullOrEmpty(_text)) {
                foreach (var textLine in _textLines) {
                    rect   =  new Rectangle(0, TOP_MARGIN + height, bounds.Width, bounds.Height);
                    height += font.LineHeight;
                    spriteBatch.DrawStringOnCtrl(this, textLine, font, rect, _brightGold, false, true, STROKE_DIST, HorizontalAlignment.Center, VerticalAlignment.Top);
                }
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

            Animation.Tweener.Tween(this, new {Top = _targetTop}, _fadeOutDuration);

            if (_opacity < 1f) {
                return;
            }

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
