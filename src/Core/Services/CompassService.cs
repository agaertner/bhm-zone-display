using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Gw2Sharp.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

namespace Nekres.Regions_Of_Tyria.Core.Services {
    internal class CompassService : IDisposable {

        private const string BREAKRULE = "<br>";

        private const int MAPWIDTH_MAX  = 362;
        private const int MAPHEIGHT_MAX = 338;
        private const int MAPWIDTH_MIN  = 170;
        private const int MAPHEIGHT_MIN = 170;
        private const int MAPOFFSET_MIN = 19;

        private CompassRegionDisplay _label;

        private Rectangle _compass;

        private bool _isMouseOver;
        public bool IsMouseOver {
            get => _isMouseOver;
            set {
                if (value == _isMouseOver) {
                    return;
                }
                _isMouseOver = value;

                if (_label != null && GameService.GameIntegration.Gw2Instance.IsInGame) {
                    GameService.Animation.Tweener.Tween(_label, value ? new { Opacity = 0f } : new { Opacity = 1f }, 0.15f);
                }
            }
        }

        public CompassService() {
            GameService.Gw2Mumble.UI.CompassSizeChanged             += OnCompassSizeChanged;
            GameService.Gw2Mumble.UI.IsCompassTopRightChanged       += OnCompassTopRightChanged;
            GameService.Gw2Mumble.UI.IsMapOpenChanged               += OnMapOpenChanged;
            GameService.GameIntegration.Gw2Instance.IsInGameChanged += OnIsInGameChanged;
            GameService.Input.Mouse.MouseMoved += OnMouseMoved;
        }

        private void OnMouseMoved(object sender, MouseEventArgs e) {
            IsMouseOver = _compass.Contains(e.MousePosition);
        }

        private void OnIsInGameChanged(object sender, ValueEventArgs<bool> e) {
            if (_label != null) {
                _label.Visible = e.Value;
            }
        }

        public void Show(string text) {
            _label ??= new CompassRegionDisplay {
                Font = GameService.Content.DefaultFont14,
                Height = 20,
                ZIndex = Screen.MENUUI_BASEINDEX
            };

            _label.Parent = GameService.Graphics.SpriteScreen;
            _label.Text   = text;

            UpdateCompass();

            _label.Left   = _compass.Left;
            _label.Top    = _compass.Top;
            _label.Width  = _compass.Width;
        }

        private void OnCompassSizeChanged(object sender, ValueEventArgs<Size> e) {
            UpdateCompass();
        }

        private void OnCompassTopRightChanged(object sender, ValueEventArgs<bool> e) {
            UpdateCompass();
        }

        private void OnMapOpenChanged(object sender, ValueEventArgs<bool> e) {
            if (_label != null) {
                GameService.Animation.Tweener.Tween(_label, e.Value ? new {Opacity = 0f} : new {Opacity = 1f}, 0.35f);
            }
        }

        private int GetOffset(float curr, float max, float min, float val) {
            return (int)Math.Round((curr - min) / (max - min) * (val - MAPOFFSET_MIN) + MAPOFFSET_MIN, 0);
        }

        private void UpdateCompass() {
            int offsetWidth  = GetOffset(GameService.Gw2Mumble.UI.CompassSize.Width,  MAPWIDTH_MAX,  MAPWIDTH_MIN,  40);
            int offsetHeight = GetOffset(GameService.Gw2Mumble.UI.CompassSize.Height, MAPHEIGHT_MAX, MAPHEIGHT_MIN, 40);

            int width  = GameService.Gw2Mumble.UI.CompassSize.Width            + offsetWidth;
            int height = GameService.Gw2Mumble.UI.CompassSize.Height           + offsetHeight;
            int x      = GameService.Graphics.SpriteScreen.ContentRegion.Width - width;
            int y      = 0;

            if (!GameService.Gw2Mumble.UI.IsCompassTopRight) {
                y += GameService.Graphics.SpriteScreen.ContentRegion.Height - height - 40;
            }

            _compass = new Rectangle(x, y, width, height);

            if (_label != null) {
                _label.Left  = _compass.Left;
                _label.Top   = _compass.Top;
                _label.Width = _compass.Width;
            }
        }

        public void Dispose() {
            GameService.Gw2Mumble.UI.CompassSizeChanged             -= OnCompassSizeChanged;
            GameService.Gw2Mumble.UI.IsCompassTopRightChanged       -= OnCompassTopRightChanged;
            GameService.Gw2Mumble.UI.IsMapOpenChanged               -= OnMapOpenChanged;
            GameService.GameIntegration.Gw2Instance.IsInGameChanged -= OnIsInGameChanged;
            GameService.Input.Mouse.MouseMoved                      -= OnMouseMoved;
            _label?.Dispose();
        }

        private class CompassRegionDisplay : Control {

            public string Text;
            public MonoGame.Extended.BitmapFonts.BitmapFont Font;

            protected override CaptureType CapturesInput() {
                return CaptureType.DoNotBlock;
            }

            protected override void OnMouseMoved(MouseEventArgs e) {
                var rel = RelativeMousePosition;

                base.OnMouseMoved(e);
            }

            protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds) {
                if (string.IsNullOrWhiteSpace(Text)) {
                    return;
                }

                spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, bounds, Color.Black * 0.25f);

                var lines  = Text.Split(BREAKRULE).ToList();
                int height = 0;
                for (int i = 0; i < lines.Count; i++) {
                    string line = lines[i];
                    int lineHeight = (int)Math.Round(Font.MeasureString(line).Height) + 4;
                    if (i > 0) {
                        lineHeight += Font.LineHeight;
                    }
                    spriteBatch.DrawStringOnCtrl(this, line, Font, new Rectangle(0, 0, bounds.Width, lineHeight + height), Color.White, false, true, 1, HorizontalAlignment.Center);
                    height += 20;
                }
                this.Height = height;
            }
        }
    }
}
