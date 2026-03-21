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

using ShareX.ImageEditor.Presentation.Theming;

namespace ShareX.ImageEditor.Presentation.Filters;

public static partial class FilterCatalog
{
    private static IReadOnlyDictionary<string, FilterPresentationMetadata> BuildPresentationMetadata()
    {
        return new Dictionary<string, FilterPresentationMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["add_noise"] = new("Add noise...", LucideIcons.SprayCan, "Adds noise to the image."),
            ["anime_speed_lines"] = new("Anime speed lines...", LucideIcons.ScanLine, "Overlays dramatic radial anime motion streaks from a focal point."),
            ["ascii_art"] = new("ASCII art...", LucideIcons.SquareTerminal, "Converts luminance to configurable ASCII glyph shading."),
            ["bevel"] = new("Bevel...", LucideIcons.Combine, "Adds an inner beveled edge with configurable light, highlight, and shadow."),
            ["blood_splash"] = new("Blood splash...", LucideIcons.Droplet, "Overlays deep red splashes, fine spray, and glossy runny drips."),
            ["block_glitch"] = new("Block glitch / databending...", LucideIcons.Cpu, "Displaces rectangular chunks and misaligns color channels like corrupted files."),
            ["bloom"] = new("Bloom...", LucideIcons.Sparkle, "Creates a soft glow around bright regions."),
            ["blueprint_drawing"] = new("Blueprint drawing...", LucideIcons.DraftingCompass, "Converts image structure into cyan technical lines over blueprint paper."),
            ["blur"] = new("Blur...", LucideIcons.Focus, "Applies a blur effect."),
            ["border"] = new("Border...", LucideIcons.Frame, "Adds a border to the image.", IncludeInFiltersCategory: false),
            ["cartoon_sticker_cutout"] = new("Cartoon sticker cutout...", LucideIcons.Sticker, "Posterizes colors, inks edges and adds a white sticker-like border glow."),
            ["chromatic_aberration"] = new("Chromatic aberration...", LucideIcons.Aperture, "Simulates lens fringing by separating red and blue channels toward the frame edges."),
            ["claymation_texture"] = new("Claymation texture...", LucideIcons.PaintRoller, "Builds chunky plasticine-like color lumps with tactile relief grain."),
            ["color_depth"] = new("Color depth...", LucideIcons.Layers2, "Changes the color depth of the image."),
            ["convolution_matrix"] = new("Convolution matrix...", LucideIcons.Grid3X3, "Applies a custom convolution matrix."),
            ["crosshatch"] = new("Crosshatch...", LucideIcons.Hash, "Builds shading from layered hatch lines."),
            ["crt"] = new("CRT...", LucideIcons.Monitor, "Applies a retro CRT monitor effect."),
            ["crystal_prism"] = new("Crystal prism...", LucideIcons.Gem, "Creates refracted crystal facets with RGB dispersion."),
            ["crystalize_shards"] = new("Crystalize shards...", LucideIcons.Diamond, "Breaks the image into faceted crystal shards with edge glints."),
            ["dithering"] = new("Dithering...", LucideIcons.DotSquare, "Reduces palette with Floyd-Steinberg or Bayer dot diffusion."),
            ["frosted_glass_ice_edges"] = new("Frosted glass + ice edges...", LucideIcons.GlassWater, "Applies frosted refraction with cool icy edge highlights."),
            ["gaussian_blur"] = new("Gaussian blur...", LucideIcons.CircleGauge, "Applies a Gaussian blur effect."),
            ["glow"] = new("Glow...", LucideIcons.Lightbulb, "Applies a glowing effect."),
            ["halation"] = new("Halation...", LucideIcons.Sunset, "Adds warm cinematic glow around intense highlights."),
            ["halftone"] = new("Halftone...", LucideIcons.CircleDotDashed, "Creates a comic-style CMYK dot print pattern."),
            ["heat_haze_refraction"] = new("Heat haze refraction...", LucideIcons.Waves, "Distorts the image with shimmering refractive heat ripples."),
            ["hologram_scan"] = new("Hologram scan...", LucideIcons.ScanFace, "Adds scanlines, glitch offsets and cyan glow."),
            ["holographic_foil_shimmer"] = new("Holographic foil shimmer...", LucideIcons.Rainbow, "Adds iridescent foil rainbow shimmer with specular sparkle."),
            ["ink_splatter_drips"] = new("Ink splatter + drips...", LucideIcons.Brush, "Adds expressive ink blotches with gravity drips and paper stain fade."),
            ["lens_blur"] = new("Lens blur (bokeh)...", LucideIcons.Target, "Simulates circular aperture blur with weighted highlight bloom."),
            ["liquid_glass"] = new("Liquid glass...", LucideIcons.Glasses, "Applies refractive liquid-like glass distortion and gloss."),
            ["luminance_contour_lines"] = new("Luminance contour lines...", LucideIcons.Map, "Quantizes luminance into grayscale contour bands with adjustable line overlays."),
            ["matrix_digital_rain"] = new("Matrix digital rain...", LucideIcons.Terminal, "Rebuilds the image with glowing falling terminal glyphs."),
            ["median_filter"] = new("Median filter...", LucideIcons.Filter, "Applies a median filter for noise reduction."),
            ["mosaic_polygon"] = new("Mosaic polygon...", LucideIcons.Hexagon, "Pixelates with tessellated hexagon or triangle polygons."),
            ["motion_blur"] = new("Motion blur...", LucideIcons.MoveHorizontal, "Applies a motion blur effect."),
            ["nebula_starfield"] = new("Nebula starfield...", LucideIcons.Sparkles, "Overlays colored nebula clouds and procedural stars."),
            ["neon_edge_glow"] = new("Neon edge glow...", LucideIcons.Zap, "Detects edges and renders a neon glow around them."),
            ["oil_paint"] = new("Oil paint...", LucideIcons.Paintbrush2, "Makes the image look like an oil painting."),
            ["old_camera_flash_burn"] = new("Old camera flash burn...", LucideIcons.Flashlight, "Simulates overexposed flash, warm frame burn and analog grain."),
            ["outline"] = new("Outline...", LucideIcons.VectorSquare, "Applies an outline effect."),
            ["paper_stencil_mask"] = new("Paper stencil mask...", LucideIcons.Stamp, "Builds a feathered stencil mask over a dimmed background."),
            ["pencil_sketch"] = new("Pencil sketch...", LucideIcons.PencilLine, "Simulates graphite sketch using dodge-blur and edge darkening."),
            ["pixel_sorting"] = new("Pixel sorting...", LucideIcons.ArrowDownWideNarrow, "Sorts horizontal or vertical pixel spans by brightness or hue for glitch cascades."),
            ["pixelate"] = new("Pixelate...", LucideIcons.Grid2X2, "Pixelates the image."),
            ["pointillism"] = new("Pointillism...", LucideIcons.Dot, "Re-renders the image as overlapping colored dots."),
            ["rainy_window"] = new("Rainy window...", LucideIcons.CloudRain, "Simulates water streaks, droplets and foggy glass."),
            ["reflection"] = new("Reflection...", LucideIcons.FlipVertical2, "Adds a reflection to the bottom of the image."),
            ["rgb_split"] = new("RGB split...", LucideIcons.SplitSquareHorizontal, "Splits the red, green, and blue color channels."),
            ["riso_print"] = new("Riso print...", LucideIcons.Newspaper, "Simulates layered risograph inks with halftone dots and slight registration offset."),
            ["shadow"] = new("Shadow...", LucideIcons.CloudMoon, "Adds a drop shadow to the image."),
            ["sharpen"] = new("Sharpen...", LucideIcons.Crosshair, "Sharpens the image."),
            ["slice"] = new("Slice...", LucideIcons.Slice, "Slices the image."),
            ["snowfall_depth_fog"] = new("Snowfall + depth fog...", LucideIcons.CloudSnow, "Overlays procedural snowfall layers with atmospheric distance fog."),
            ["sobel_edge"] = new("Sobel edge...", LucideIcons.ScanEye, "Applies a Sobel edge detection filter."),
            ["spin_blur"] = new("Spin blur...", LucideIcons.LoaderPinwheel, "Blurs pixels along an arc around a custom center point."),
            ["stained_glass"] = new("Stained glass...", LucideIcons.PanelsTopBottom, "Turns the image into stained glass-style tiles."),
            ["surface_blur"] = new("Surface blur...", LucideIcons.BrushCleaning, "Softens regions while preserving stronger local edges."),
            ["thermal_vision"] = new("Thermal vision...", LucideIcons.ThermometerSun, "Maps image intensity to an infrared heatmap gradient."),
            ["tilt_shift"] = new("Tilt-shift (miniature)...", LucideIcons.Camera, "Adds selective blur and saturation for toy-like miniature scenes."),
            ["torn_edge"] = new("Torn edge...", LucideIcons.ScissorsLineDashed, "Adds a torn edge border effect."),
            ["unsharp_mask"] = new("Unsharp mask...", LucideIcons.Target, "Applies an unsharp mask filter."),
            ["vignette"] = new("Vignette...", LucideIcons.CircleDashed, "Applies a vignette effect."),
            ["vintage_print_damage"] = new("Vintage print damage...", LucideIcons.Newspaper, "Adds film grain, scratches, dust and faded paper burn."),
            ["watercolor_kuwahara"] = new("Watercolor / Kuwahara...", LucideIcons.Paintbrush, "Simplifies local color regions for watercolor-like strokes."),
            ["wave_edge"] = new("Wave edge...", LucideIcons.WavesLadder, "Adds a wavy edge to the image."),
            ["zoom_blur"] = new("Zoom blur...", LucideIcons.ZoomIn, "Creates radial streak blur toward a chosen center point."),

            // --- Migrated from bespoke dialogs (IEIP0003 Phase 4) ---
            ["anamorphic_lens_flare"] = new("Anamorphic lens flare...", LucideIcons.Aperture, "Adds cinematic horizontal flare streaks, warm bloom, and lens ghosts around bright areas."),
            ["etched_glass"] = new("Etched glass...", LucideIcons.Glasses, "Turns the image into frosted glass with engraved detail and refracted highlights."),
            ["liquid_mercury"] = new("Liquid mercury...", LucideIcons.Droplet, "Builds reflective silver ripples and glossy fluid-metal highlights."),
            ["night_vision"] = new("Night vision...", LucideIcons.Moon, "Simulates green phosphor night optics with glow, noise, and scanlines."),
            ["oil_slick_interference"] = new("Oil slick interference...", LucideIcons.Rainbow, "Overlays dark glossy iridescent interference colors like spilled oil."),
            ["plasma_energy_arcs"] = new("Plasma energy arcs...", LucideIcons.Zap, "Adds glowing electric plasma filaments with branchy arc turbulence."),
            ["rust_corrosion"] = new("Rust / corrosion...", LucideIcons.Stamp, "Adds oxidized rust blooms, pitting, grime, and worn edges."),
            ["smoke_overlay"] = new("Smoke overlay...", LucideIcons.Cloud, "Adds drifting layered smoke billows over the image."),
            ["vhs_tape_damage"] = new("VHS tape damage...", LucideIcons.Monitor, "Applies analog tracking jitter, chroma bleed, scanlines, and tape dropouts."),
            ["x_ray_scan"] = new("X-ray scan...", LucideIcons.ScanEye, "Rebuilds the image as glowing cyan density and edge structure like a scanner plate.")
        };
    }
}
