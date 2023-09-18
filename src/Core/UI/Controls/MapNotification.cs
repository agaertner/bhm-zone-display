﻿using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
namespace Nekres.Regions_Of_Tyria.UI.Controls {
    internal sealed class MapNotification : Container
    {
        private const int TOP_MARGIN     = 20;
        private const int STROKE_DIST    = 1;
        private const int UNDERLINE_SIZE = 2;

        private static readonly Color _brightGold;
        private static readonly Color _darkGold;

        private const int NOTIFICATION_COOLDOWN_MS = 2000;
        private static DateTime _lastNotificationTime;
        
        private static readonly SynchronizedCollection<MapNotification> _activeMapNotifications;

        private static SpriteBatchParameters _defaultParams;

        static MapNotification()
        {
            _lastNotificationTime = DateTime.UtcNow;
            _activeMapNotifications = new SynchronizedCollection<MapNotification>();

            _defaultParams = new SpriteBatchParameters();

            _brightGold = new Color(223, 194, 149, 255);
            _darkGold = new Color(178, 160,  145, 255);
        }

        public static void ShowNotification(string header, string footer, float showDuration = 4, float fadeInDuration = 2, float fadeOutDuration = 2, float effectDuration = 0.85f) {
            if (DateTime.UtcNow.Subtract(_lastNotificationTime).TotalMilliseconds < NOTIFICATION_COOLDOWN_MS) {
                return;
            }

            _lastNotificationTime = DateTime.UtcNow;

            var nNot = new MapNotification(header, footer, showDuration, fadeInDuration, fadeOutDuration, effectDuration) {
                Parent = Graphics.SpriteScreen
            };

            nNot.ZIndex = _activeMapNotifications.DefaultIfEmpty(nNot).Max(n => n.ZIndex) + 1;

            foreach (var activeScreenNotification in _activeMapNotifications) {
                activeScreenNotification.SlideDown(150);
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

        private SpriteBatchParameters _decode;
        private SpriteBatchParameters _reveal;

        private int   _targetTop;
        private float _amount = 0.0f;
        private bool  _isFading;

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

            _decode = new SpriteBatchParameters {
                Effect = RegionsOfTyria.Instance.DissolveEffect.Clone()
            };
            _reveal = new SpriteBatchParameters {
                Effect = RegionsOfTyria.Instance.DissolveEffect.Clone()
            };

            //var burnColor = new Vector4(0.4f, 0.23f, 0.0f, 0.8f);
            _decode.Effect.Parameters["Amount"].SetValue(0.0f);
            //_decode.Effect.Parameters["GlowColor"].SetValue(burnColor);
            _decode.Effect.Parameters["Slide"].SetValue(true);
            _reveal.Effect.Parameters["Amount"].SetValue(1.0f);
            //_reveal.Effect.Parameters["GlowColor"].SetValue(burnColor);
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

            _decode.Effect.Parameters["Opacity"].SetValue(this.Opacity);
            _reveal.Effect.Parameters["Opacity"].SetValue(this.Opacity);

            _decode.Effect.Parameters["Amount"].SetValue(_amount);
            _reveal.Effect.Parameters["Amount"].SetValue(1.0f - _amount);

            spriteBatch.End();

            if (_isFading) {

                _reveal.Effect.Parameters["Slide"].SetValue(false);
                
            } else if (RegionsOfTyria.Instance.Translate.Value) {

                spriteBatch.Begin(_decode);
                PaintText(this, spriteBatch, bounds, RegionsOfTyria.Instance.KrytanFont, RegionsOfTyria.Instance.KrytanFontSmall, _header, _text, false);
                spriteBatch.End();

            }

            spriteBatch.Begin(_reveal);
            PaintText(this, spriteBatch, bounds, RegionsOfTyria.Instance.TitlingFont, RegionsOfTyria.Instance.TitlingFontSmall, _header, _text, true, _amount);
            spriteBatch.End();
            spriteBatch.Begin(_defaultParams);
        }

        internal static void PaintText(Control ctrl, SpriteBatch spriteBatch, Rectangle bounds, BitmapFont font, BitmapFont smallFont, string header, string text, bool underline = true, float deltaAmount = 1) {
            var       height = (int)Math.Round(RegionsOfTyria.Instance.VerticalPosition.Value / 100f * bounds.Height);
            Rectangle rect;

            if (!string.IsNullOrEmpty(header) && !header.Equals(text, StringComparison.InvariantCultureIgnoreCase)) {

                var str  = header.Wrap();
                var size       = smallFont.MeasureString(str);
                var lineHeight = (int)size.Height;

                if (underline) {
                    // Draw underline before header so that the serifs or terminals of letters are not drawn over.

                    var lineWidth = (int)Math.Round(deltaAmount * size.Width);

                    // underline border
                    rect = new Rectangle((bounds.Width - lineWidth) / 2, height + lineHeight + 15, (lineWidth + 2) / 2, UNDERLINE_SIZE + 2);
                    spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, rect, Color.Black * 0.8f);

                    // underline fill
                    rect = new Rectangle(rect.X + 1, rect.Y + 1, lineWidth / 2, UNDERLINE_SIZE);
                    spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, rect, _darkGold);

                    // underline border
                    rect = new Rectangle(bounds.Width / 2, height + lineHeight + 15, (lineWidth + 1) / 2, UNDERLINE_SIZE + 2);
                    spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, rect, Color.Black * 0.8f);

                    // underline fill
                    rect = new Rectangle(rect.X - 1, rect.Y + 1, lineWidth / 2, UNDERLINE_SIZE);
                    spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, rect, _darkGold);
                }

                rect = new Rectangle(0, height, bounds.Width, bounds.Height);
                spriteBatch.DrawStringOnCtrl(ctrl, str, smallFont, rect, _darkGold, false, true, STROKE_DIST, HorizontalAlignment.Center, VerticalAlignment.Top);
                height += font.LineHeight * 2 + TOP_MARGIN;
            }

            if (!string.IsNullOrEmpty(text)) {
                rect   =  new Rectangle(0, height, bounds.Width, bounds.Height);
                spriteBatch.DrawStringOnCtrl(ctrl, text.Wrap(), font, rect, _brightGold, false, true, STROKE_DIST, HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }

        /// <inheritdoc />
        public override void Show() {
            //Nesting instead so we are able to set a different duration per fade direction.
            Animation.Tweener.Tween(this, new { Opacity = 1f }, _fadeInDuration)
                     .OnComplete(() => {
                         Animation.Tweener.Timer(0.2f)
                         .OnComplete(() => {
                            Animation.Tweener.Tween(this, new {_amount = 1f}, _effectDuration)
                            .OnComplete(() => {
                                Animation.Tweener.Tween(this, new {Opacity = 1f}, _showDuration)
                                .OnComplete(() => {
                                    _isFading = true;
                                    Animation.Tweener.Tween(this, RegionsOfTyria.Instance.Dissolve.Value ? 
                                                                      new {Opacity = 0.9f, _amount = 0f} : 
                                                                      new {Opacity = 0f}, _fadeOutDuration)
                                    .OnComplete(Dispose);
                                });
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

            Animation.Tweener.Tween(this, new { Opacity = 0f, Top = _targetTop }, _fadeOutDuration).OnComplete(Dispose);
        }

        /// <inheritdoc />
        protected override void DisposeControl() {
            _reveal.Effect?.Dispose();
            _decode.Effect?.Dispose();

            _activeMapNotifications.Remove(this);
            GameService.Graphics.SpriteScreen.Resized -= UpdateLocation;

            base.DisposeControl();
        }
    }
}
