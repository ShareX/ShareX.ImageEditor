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

using Avalonia.Media;

namespace ShareX.ImageEditor
{
    public class EditorOptions
    {
        public static readonly Color PrimaryColor = Color.FromArgb(255, 242, 60, 60);
        public static readonly Color SecondaryColor = Color.FromArgb(255, 250, 250, 250);

        // Shared options
        public Color BorderColor { get; set; } = PrimaryColor;
        public Color FillColor { get; set; } = Colors.Transparent;
        public int Thickness { get; set; } = 4;
        public bool Shadow { get; set; } = true;
        public float ShadowBlur { get; set; } = 30;
        public Color ShadowColor { get; set; } = Color.FromArgb(128, 0, 0, 0);

        // Text tool
        public Color TextColor { get; set; } = SecondaryColor;
        public float TextFontSize { get; set; } = 48;

        // Speech balloon tool
        public Color SpeechBalloonBorderColor { get; set; } = Colors.Transparent;
        public Color SpeechBalloonFillColor { get; set; } = PrimaryColor;
        public Color SpeechBalloonTextColor { get; set; } = SecondaryColor;
        public float SpeechBalloonFontSize { get; set; } = 48;

        // Step tool
        public Color StepBorderColor { get; set; } = Colors.Transparent;
        public Color StepFillColor { get; set; } = PrimaryColor;
        public Color StepTextColor { get; set; } = SecondaryColor;
        public float StepFontSize { get; set; } = 30;

        // Highlighter tool
        public Color HighlighterColor { get; set; } = Colors.Yellow;

        public float BlurStrength { get; set; } = 30;
        public float PixelateStrength { get; set; } = 20;
        public float MagnifierStrength { get; set; } = 2;
        public float SpotlightStrength { get; set; } = 15;
    }
}