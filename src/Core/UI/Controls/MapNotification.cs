using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Glide;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
namespace Nekres.Regions_Of_Tyria.UI.Controls {
    internal sealed class MapNotification : Container {

        private const string BREAKRULE      = "<br>";
        private const int    STROKE_DIST    = 1;
        private const int    UNDERLINE_SIZE = 2;

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

        public static void ShowNotification(string header, string text, float showDuration = 4, float fadeInDuration = 2, float fadeOutDuration = 2, float effectDuration = 0.85f) {
            if (DateTime.UtcNow.Subtract(_lastNotificationTime).TotalMilliseconds < NOTIFICATION_COOLDOWN_MS) {
                return;
            }

            _lastNotificationTime = DateTime.UtcNow;

            if (string.IsNullOrEmpty(text)) {
                return; // Main text is required.
            }

            try {
                var nNot = new MapNotification(header, text, showDuration, fadeInDuration, fadeOutDuration, effectDuration) {
                    Parent = Graphics.SpriteScreen
                };

                nNot.ZIndex = _activeMapNotifications.DefaultIfEmpty(nNot).Max(n => n.ZIndex) + 1;

                foreach (var activeScreenNotification in _activeMapNotifications) {
                    activeScreenNotification.SlideDown(150);
                }

                _activeMapNotifications.Add(nNot);

                nNot.Show();
            } catch (Exception) {
                // Module was probably unloaded.
            }
        }

        private string _header;
        private string _text;
        private float  _showDuration;
        private float  _fadeInDuration;
        private float  _fadeOutDuration;
        private float  _effectDuration;

        private SpriteBatchParameters _decode;
        private SpriteBatchParameters _reveal;

        private SoundEffectInstance _decodeSound;
        private SoundEffectInstance _vanishSound;

        private Tween _anim;
        private int   _targetTop;
        private float _amount = 0.0f;
        private bool  _isFading;
        private bool  _dissolve;

        private MapNotification(string header, string text, float showDuration = 4, float fadeInDuration = 2, float fadeOutDuration = 2, float effectDuration = 0.85f) {
            _showDuration    = showDuration;
            _fadeInDuration  = fadeInDuration;
            _fadeOutDuration = fadeOutDuration;
            _effectDuration  = effectDuration;
            _text            = FilterDisplayName(text);
            _header          = FilterDisplayName(header);

            // Make header the main text if the latter is empty.
            if (string.IsNullOrEmpty(_text)) {
                _text   = _header;
                _header = string.Empty;
            }

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

            if (!RegionsOfTyria.Instance.MuteReveal.Value) {
                _decodeSound        = RegionsOfTyria.Instance.DecodeSound.CreateInstance();
                _decodeSound.Volume = RegionsOfTyria.Instance.RevealVolume.Value / 100f * GameService.GameIntegration.Audio.Volume;
            }

            if (!RegionsOfTyria.Instance.MuteVanish.Value) {
                _vanishSound        = RegionsOfTyria.Instance.VanishSound.CreateInstance();
                _vanishSound.Volume = RegionsOfTyria.Instance.VanishVolume.Value / 100f * GameService.GameIntegration.Audio.Volume;
            }

            _dissolve = RegionsOfTyria.Instance.Dissolve.Value;

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

        internal static string FilterDisplayName(string text) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }
            // Return empty where the API doesn't provide a proper name eg. "((1089116))".
            if (text.StartsWith("((")) {
                return string.Empty;
            }
            // Remove preceding info eg. "Weekly Strike Mission:".
            var idx = text.IndexOf(':');
            if (idx >= 0 && idx++ < text.Length) {
                text = text.Substring(idx);
            }
            // Remove trailing info eg. "(Squad)".
            idx = text.IndexOf('(');
            if (idx >= 0) {
                text = text.Substring(0, idx);
            }
            return text.Trim(); // Trim left-over separator spaces.
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
                PaintText(this, spriteBatch, bounds, 
                          RegionsOfTyria.Instance.KrytanFont, RegionsOfTyria.Instance.KrytanFontSmall, _header, _text, 
                          RegionsOfTyria.Instance.OverlapHeader.Value, false);
                spriteBatch.End();
            }

            spriteBatch.Begin(_reveal);
            PaintText(this, spriteBatch, bounds, 
                      RegionsOfTyria.Instance.TitlingFont, RegionsOfTyria.Instance.TitlingFontSmall, _header, _text, 
                      RegionsOfTyria.Instance.OverlapHeader.Value, 
                      RegionsOfTyria.Instance.UnderlineHeader.Value, _amount);
            spriteBatch.End();
            spriteBatch.Begin(_defaultParams);
        }

        internal static void PaintText(Control ctrl, SpriteBatch spriteBatch, Rectangle bounds, BitmapFont font, BitmapFont smallFont, string header, string text, bool overlap = false, bool underline = true, float deltaAmount = 1) {
            var       height = (int)Math.Round(RegionsOfTyria.Instance.VerticalPosition.Value / 100f * bounds.Height);
            Rectangle rect;

            if (!string.IsNullOrEmpty(header) && !header.Equals(text, StringComparison.InvariantCultureIgnoreCase)) {

                // Count the header lines to determine the height of the header.
                var lines  = 1 + header.Count(BREAKRULE);
                var bottom = height + lines * smallFont.LineHeight;

                if (underline) {
                    // Draw underline before header so that the serifs or terminals of letters are not drawn over.

                    var maxWidth  = (int)Math.Round(smallFont.MeasureString(header).Width);
                    var lineWidth = (int)Math.Round(deltaAmount * maxWidth);

                    // underline border
                    rect = new Rectangle((bounds.Width - lineWidth) / 2, bottom + 15, (lineWidth + 2) / 2, UNDERLINE_SIZE + 2);
                    spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, rect, Color.Black * 0.8f);

                    // underline fill
                    rect = new Rectangle(rect.X + 1, rect.Y + 1, lineWidth / 2, UNDERLINE_SIZE);
                    spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, rect, _darkGold);

                    // underline border
                    rect = new Rectangle(bounds.Width / 2, bottom + 15, (lineWidth + 1) / 2, UNDERLINE_SIZE + 2);
                    spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, rect, Color.Black * 0.8f);

                    // underline fill
                    rect = new Rectangle(rect.X - 1, rect.Y + 1, lineWidth / 2, UNDERLINE_SIZE);
                    spriteBatch.DrawOnCtrl(ctrl, ContentService.Textures.Pixel, rect, _darkGold);
                }

                rect = new Rectangle(0, height, bounds.Width, bounds.Height);
                spriteBatch.DrawStringOnCtrl(ctrl, header.Wrap(BREAKRULE), smallFont, rect, _darkGold, false, true, STROKE_DIST, HorizontalAlignment.Center, VerticalAlignment.Top);
                
                height = bottom - smallFont.LineHeight * (overlap ? 2 : 1);
            }

            if (!string.IsNullOrEmpty(text)) {
                rect = new Rectangle(0, height, bounds.Width, bounds.Height);

                // Draw lines separately to fix the overlapping line height of the titling font.
                foreach (string line in text.Split(BREAKRULE)) {
                    rect.Y += (int)Math.Round(font.MeasureString(line).Height * 2.5f);
                    spriteBatch.DrawStringOnCtrl(ctrl, line, font, rect, _brightGold, false, true, STROKE_DIST, HorizontalAlignment.Center, VerticalAlignment.Top);
                }
            }
        }

        /// <inheritdoc />
        public override void Show() {
            //Nesting instead of Reverse so we are able to set a different duration per fade direction.
            _anim = Animation.Tweener.Tween(this, new { Opacity = 1f }, _fadeInDuration)
                     .OnComplete(() => {
                         _anim = Animation.Tweener.Timer(0.2f)
                         .OnComplete(() => {
                            _decodeSound?.Play();
                            _anim = Animation.Tweener.Tween(this, new {_amount = 1f}, _effectDuration)
                            .OnComplete(() => {
                                _decodeSound?.Stop();
                                _anim = Animation.Tweener.Tween(this, new {Opacity = 1f}, _showDuration)
                                .OnComplete(() => {
                                    _isFading = true;
                                    _vanishSound?.Play();
                                    _anim = Animation.Tweener.Tween(this, _dissolve ? 
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

            _anim?.Cancel();
            _anim = Animation.Tweener.Tween(this, new { Opacity = 0f, Top = _targetTop }, _fadeOutDuration).OnComplete(Dispose);
        }

        /// <inheritdoc />
        protected override void DisposeControl() {
            _activeMapNotifications.Remove(this);
            GameService.Graphics.SpriteScreen.Resized -= UpdateLocation;

            _anim?.Cancel();
            _vanishSound?.Dispose();
            _decodeSound?.Dispose();
            _reveal.Effect?.Dispose();
            _decode.Effect?.Dispose();

            base.DisposeControl();
        }
    }
}
