#region Copyright and GPL License

/*
 * Xecrets Ez Mobile - Copyright © 2026 Svante Seleborg, All Rights Reserved.
 *
 * This code file is part of Xecrets Ez Mobile, an application that uses the Xecrets.Net library, parts of which in turn
 * are derived from AxCrypt as licensed under GPL v3 or later. This code is not derived from AxCrypt. It is separately
 * authored and copyrighted, and licensed only as follows unless explicitly licensed otherwise.
 *
 * Xecrets Ez Mobile is free software: you can redistribute it and/or modify it under the terms of the GNU General
 * Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any
 * later version.
 *
 * No additional permission is granted beyond that license. If you incorporate this code into a larger work and
 * distribute that work to others, you are responsible for complying with the GNU General Public License version 3 or
 * later. See https://www.gnu.org/licenses/ for more information.
 *
 * Xecrets Ez Mobile is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the
 * implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more
 * details.
 *
 * You should have received a copy of the GNU General Public License along with Xecrets Ez Mobile. If not, see
 * <https://www.gnu.org/licenses/>.
 *
 * The source repository can be found at https://github.com/xecrets/xecrets-mobile please go there for more information,
 * suggestions and contributions. You may also visit https://www.axantum.com for more information about the author.
 */

#endregion Copyright and GPL License

namespace Xecrets.Mobile.Models.Utilities;

/// <summary>
/// Reformats the notices file for on-screen display. The file is plain text hard-wrapped for a
/// fixed width, which wraps badly on a narrow screen, so the line breaks within a paragraph are
/// removed. The text is left to wrap to whatever width it is shown at. Headings keep their
/// own line, and the rules that frame the section headings are dropped.
/// </summary>
public static class ThirdPartyNoticesFormatter
{
    /// <summary>
    /// The longest a line may be to be taken for a heading rather than the first line of a
    /// paragraph. Long enough for the license section headings, short enough to leave the
    /// all-uppercase warranty disclaimers as the paragraphs they are.
    /// </summary>
    private const int _maximumHeadingLength = 40;

    public static string Format(string notices)
    {
        List<string> blocks = [];
        List<string> paragraphLines = [];

        foreach (string rawLine in notices.ReplaceLineEndings("\n").Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || IsSectionRule(line))
            {
                AddParagraph(blocks, paragraphLines);
                continue;
            }

            // A heading only counts as one where a paragraph would otherwise begin, so that an
            // uppercase line continuing a paragraph stays part of it.
            if (paragraphLines.Count == 0 && IsHeading(line))
            {
                blocks.Add(line);
                continue;
            }

            paragraphLines.Add(line);
        }

        AddParagraph(blocks, paragraphLines);

        return string.Join(Environment.NewLine + Environment.NewLine, blocks);
    }

    private static void AddParagraph(List<string> blocks, List<string> paragraphLines)
    {
        if (paragraphLines.Count == 0)
        {
            return;
        }

        blocks.Add(string.Join(' ', paragraphLines));
        paragraphLines.Clear();
    }

    private static bool IsSectionRule(string line) =>
        line.Length > 0 && line.All(character => character == '=');

    private static bool IsHeading(string line) =>
        line.Length <= _maximumHeadingLength &&
        line.Any(char.IsLetter) &&
        !line.Any(char.IsLower);
}
