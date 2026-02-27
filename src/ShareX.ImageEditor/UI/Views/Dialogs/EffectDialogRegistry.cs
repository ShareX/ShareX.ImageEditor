#region License Information (GPL v3)

/*
    ShareX.ImageEditor - The UI-agnostic Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Avalonia.Controls;

namespace ShareX.ImageEditor.Views.Dialogs
{
    /// <summary>
    /// XIP0039 Pain Point 3: Centralizes effect dialog factories so new effects can be
    /// registered without adding handler methods to <c>EditorView</c>.
    /// <para>
    /// To add a new dialog-based effect:
    /// <list type="number">
    ///   <item>Implement <see cref="IEffectDialog"/> on a <see cref="UserControl"/>.</item>
    ///   <item>Add a factory entry in <see cref="EffectDialogRegistry._factories"/> below.</item>
    ///   <item>Add a menu item in <c>EffectsMenuDropdown</c> that calls <c>RaiseDialog("your_id")</c>.</item>
    /// </list>
    /// No new handler method in <c>EditorView.EffectsHost.cs</c> is required.
    /// </para>
    /// </summary>
    public static class EffectDialogRegistry
    {
        private static readonly Dictionary<string, Func<UserControl>> _factories =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // --- Adjustments ---
                ["brightness"]      = () => new BrightnessDialog(),
                ["contrast"]        = () => new ContrastDialog(),
                ["hue"]             = () => new HueDialog(),
                ["saturation"]      = () => new SaturationDialog(),
                ["gamma"]           = () => new GammaDialog(),
                ["alpha"]           = () => new AlphaDialog(),
                ["colorize"]        = () => new ColorizeDialog(),
                ["selective_color"] = () => new SelectiveColorDialog(),
                ["replace_color"]   = () => new ReplaceColorDialog(),
                ["grayscale"]       = () => new GrayscaleDialog(),
                ["sepia"]           = () => new SepiaDialog(),

                // --- Filters ---
                ["border"]          = () => new BorderDialog(),
                ["outline"]         = () => new OutlineDialog(),
                ["shadow"]          = () => new ShadowDialog(),
                ["glow"]            = () => new GlowDialog(),
                ["reflection"]      = () => new ReflectionDialog(),
                ["torn_edge"]       = () => new TornEdgeDialog(),
                ["slice"]           = () => new SliceDialog(),

                // --- Transforms ---
                ["rounded_corners"] = () => new RoundedCornersDialog(),
                ["skew"]            = () => new SkewDialog(),
                ["rotate_3d"]       = () => new Rotate3DDialog(),
                ["rotate_3d_box"]   = () => new Rotate3DBoxDialog(),

                // --- Quality ---
                ["blur"]            = () => new BlurDialog(),
                ["pixelate"]        = () => new PixelateDialog(),
                ["sharpen"]         = () => new SharpenDialog(),
            };

        /// <summary>
        /// Tries to create a new effect dialog instance for the given <paramref name="effectId"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> and a fresh dialog <see cref="UserControl"/> if the ID is registered;
        /// otherwise <see langword="false"/> and <see langword="null"/>.
        /// </returns>
        public static bool TryCreate(string effectId, out UserControl? dialog)
        {
            if (_factories.TryGetValue(effectId, out var factory))
            {
                dialog = factory();
                return true;
            }

            dialog = null;
            return false;
        }

        /// <summary>Returns all registered effect IDs (case-insensitive).</summary>
        public static IReadOnlyCollection<string> RegisteredIds => _factories.Keys;
    }
}
