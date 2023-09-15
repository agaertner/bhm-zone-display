using Blish_HUD;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;
using Blish_HUD.Modules.Managers;
using SpriteFontPlus;
namespace Nekres.Regions_Of_Tyria {
    internal static class ContentsManagerExtensions {

        /// <summary>
        /// Loads a <see cref="SpriteFont"/> from a TrueTypeFont (*.ttf) file.
        /// </summary>
        /// <param name="manager">Module's <see cref="ContentsManager"/>.</param>
        /// <param name="fontPath">The path to the TTF font file.</param>
        /// <param name="fontSize">Size of the font.</param>
        public static SpriteFont GetSpriteFont(this ContentsManager manager, string fontPath, int fontSize) {
            if (fontSize <= 0) {
                throw new ArgumentException("Font size must be greater than 0.", nameof(fontSize));
            }

            using var fontStream = manager.GetFileStream(fontPath);
            var fontData = new byte[fontStream.Length];
            var fontDataLength = fontStream.Read(fontData, 0, fontData.Length);

            if (fontDataLength > 0) {
                using var ctx = GameService.Graphics.LendGraphicsDeviceContext();
                var bakeResult = TtfFontBaker.Bake(fontData, fontSize, 1024, 1024, new[] {
                    CharacterRange.BasicLatin,
                    CharacterRange.Latin1Supplement,
                    CharacterRange.LatinExtendedA
                });
                return bakeResult.CreateSpriteFont(ctx.GraphicsDevice);
            }

            return null;
        }

        /// <summary>
        /// Loads a <see cref="BitmapFont"/> from a TrueTypeFont (*.ttf) file.
        /// </summary>
        /// <param name="manager">Module's <see cref="ContentsManager"/>.</param>
        /// <param name="fontPath">The path to the TTF font file.</param>
        /// <param name="fontSize">Size of the font.</param>
        /// <param name="lineHeight">Sets the line height. By default, <see cref="SpriteFont.LineSpacing"/> will be used.</param>
        public static BitmapFont GetBitmapFont(this ContentsManager manager, string fontPath, int fontSize, int lineHeight = 0) {
            if (lineHeight < 0) {
                throw new ArgumentException("Line height cannot be negative.", nameof(lineHeight));
            }
            return manager.GetSpriteFont(fontPath, fontSize)?.ToBitmapFont(lineHeight);
        }
    }
}
