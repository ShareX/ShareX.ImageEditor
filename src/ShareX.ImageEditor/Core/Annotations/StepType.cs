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

using System.Globalization;
using System.Text;

namespace ShareX.ImageEditor.Core.Annotations;

public enum StepType
{
    Numeric,
    UppercaseLetter,
    LowercaseLetter,
    UppercaseRoman,
    LowercaseRoman
}

public static class StepTypeFormatter
{
    private static readonly (int Value, string Symbol)[] RomanSymbols =
    {
        (1000, "M"),
        (900, "CM"),
        (500, "D"),
        (400, "CD"),
        (100, "C"),
        (90, "XC"),
        (50, "L"),
        (40, "XL"),
        (10, "X"),
        (9, "IX"),
        (5, "V"),
        (4, "IV"),
        (1, "I")
    };

    public static string Format(int value, StepType stepType)
    {
        if (value <= 0)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        return stepType switch
        {
            StepType.Numeric => value.ToString(CultureInfo.InvariantCulture),
            StepType.UppercaseLetter => FormatAlphabetic(value, upperCase: true),
            StepType.LowercaseLetter => FormatAlphabetic(value, upperCase: false),
            StepType.UppercaseRoman => FormatRoman(value, upperCase: true),
            StepType.LowercaseRoman => FormatRoman(value, upperCase: false),
            _ => value.ToString(CultureInfo.InvariantCulture)
        };
    }

    public static string GetPreviewText(StepType stepType)
    {
        return stepType switch
        {
            StepType.Numeric => "1, 2, 3 ...",
            StepType.UppercaseLetter => "A, B, C ...",
            StepType.LowercaseLetter => "a, b, c ...",
            StepType.UppercaseRoman => "I, II, III ...",
            StepType.LowercaseRoman => "i, ii, iii ...",
            _ => "1, 2, 3 ..."
        };
    }

    private static string FormatAlphabetic(int value, bool upperCase)
    {
        var builder = new StringBuilder();
        int current = value;

        while (current > 0)
        {
            current--;
            builder.Insert(0, (char)('A' + (current % 26)));
            current /= 26;
        }

        string formatted = builder.ToString();
        return upperCase ? formatted : formatted.ToLowerInvariant();
    }

    private static string FormatRoman(int value, bool upperCase)
    {
        var builder = new StringBuilder();
        int remaining = value;

        foreach ((int romanValue, string symbol) in RomanSymbols)
        {
            while (remaining >= romanValue)
            {
                builder.Append(symbol);
                remaining -= romanValue;
            }
        }

        string formatted = builder.ToString();
        return upperCase ? formatted : formatted.ToLowerInvariant();
    }
}
