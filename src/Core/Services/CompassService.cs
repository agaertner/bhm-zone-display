using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Gw2Sharp.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

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

        private HashSet<int> _noCompassMaps = new(new[] {
            935, // SAB Lobby
            895, // SAB World 1
            934, // SAB World 2
        });

        public CompassService() {
            GameService.Gw2Mumble.UI.CompassSizeChanged             += OnCompassSizeChanged;
            GameService.Gw2Mumble.UI.IsCompassTopRightChanged       += OnCompassTopRightChanged;
            GameService.Gw2Mumble.UI.IsMapOpenChanged               += OnMapOpenChanged;
            GameService.GameIntegration.Gw2Instance.IsInGameChanged += OnIsInGameChanged;
            GameService.Input.Mouse.MouseMoved                      += OnMouseMoved;
            GameService.Gw2Mumble.CurrentMap.MapChanged             += OnMapChanged;
        }

        private void OnMapChanged(object sender, ValueEventArgs<int> e) {
            if (!HasCompass()) {
                _label?.Dispose();
                _label = null;
            }
        }

        private void OnMouseMoved(object sender, MouseEventArgs e) {
            IsMouseOver = _compass.Contains(e.MousePosition);
        }

        private void OnIsInGameChanged(object sender, ValueEventArgs<bool> e) {
            if (_label != null) {
                _label.Visible = e.Value;
            }
        }

        private bool HasCompass() {
            return !_noCompassMaps.Contains(GameService.Gw2Mumble.CurrentMap.Id)
                && GameService.Gw2Mumble.RawClient.Compass is {Width: > 0, Height: > 0};
        }

        public void Show(string text) {
            if (string.IsNullOrEmpty(text)) {
                _label?.Dispose();
                _label = null;
                return;
            }

            if (!HasCompass()) {
                return;
            }

            _label ??= new CompassRegionDisplay {
                Font = GameService.Content.DefaultFont16,
                Height = 20,
                ZIndex = Screen.MENUUI_BASEINDEX
            };

            _label.Parent = GameService.Graphics.SpriteScreen;
            _label.Text   = text;

            UpdateCompass();
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

            var l = GameService.Gw2Mumble.RawClient.Compass;
            if (!GameService.Gw2Mumble.UI.IsCompassTopRight) {
                y += GameService.Graphics.SpriteScreen.ContentRegion.Height - height - 40;
            }

            _compass = new Rectangle(x, y, width, height);

            if (_label != null) {
                _label.Left   = _compass.Left;
                _label.Top    = _compass.Top;
                _label.Width  = _compass.Width;
                _label.Height = _compass.Height;
            }
        }

        public void Dispose() {
            GameService.Gw2Mumble.UI.CompassSizeChanged             -= OnCompassSizeChanged;
            GameService.Gw2Mumble.UI.IsCompassTopRightChanged       -= OnCompassTopRightChanged;
            GameService.Gw2Mumble.UI.IsMapOpenChanged               -= OnMapOpenChanged;
            GameService.GameIntegration.Gw2Instance.IsInGameChanged -= OnIsInGameChanged;
            GameService.Input.Mouse.MouseMoved                      -= OnMouseMoved;
            GameService.Gw2Mumble.CurrentMap.MapChanged             -= OnMapChanged;
            _label?.Dispose();
            _label = null;
        }

        private class CompassRegionDisplay : Control {

            public string Text;
            public MonoGame.Extended.BitmapFonts.BitmapFont Font;

            private Texture2D _bgTex;

            public CompassRegionDisplay() {
                _bgTex = GameService.Content.GetTexture("fade-down-46");
            }

            protected override CaptureType CapturesInput() {
                return CaptureType.DoNotBlock;
            }

            protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds) {
                if (string.IsNullOrWhiteSpace(Text) || !GameService.Gw2Mumble.IsAvailable) {
                    return;
                }

                spriteBatch.DrawOnCtrl(this, _bgTex, new Rectangle(bounds.X, bounds.Y, bounds.Width, 30), _bgTex.Bounds, Color.White * 0.7f);
                //spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, bounds, Color.Black * 0.25f);

                int height = 5;
                foreach (var line in Text.Split(BREAKRULE)) {
                    spriteBatch.DrawStringOnCtrl(this, line, Font, new Rectangle(0, height, bounds.Width, bounds.Height), Color.White, false, true, 1, HorizontalAlignment.Center, VerticalAlignment.Top);
                    height += Font.LineHeight;
                }
            }
        }
    }
}
