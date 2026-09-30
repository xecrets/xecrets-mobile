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

using System;
using System.Globalization;

using Microsoft.Maui.Controls;

using Xecrets.Texts;

namespace Xecrets.Mobile.Utilities;

// Shortens a path to about the number of characters that fit the given width at the given font size, keeping its
// end. The values are the path, the available width and the font size. The converter parameter enables shortening, so
// that platforms which can truncate at the head themselves get the path unchanged.
public sealed class PathEllipsisConverter : IMultiValueConverter
{
    // A typical average character width of a proportional UI font, as a fraction of the font size.
    private const double _averageCharacterWidthFactor = 0.50;

    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        // Each value arrives through a proxy property that defaults to null. If so, return null.
        string? path = (string?)values[0];
        if (path is null)
        {
            return null;
        }

        bool shorten = bool.Parse((string)parameter!);
        double width = (double)values[1]!;

        // The width is -1 until the label has been laid out.
        if (!shorten || width < 0)
        {
            return path;
        }

        double fontSize = (double)values[2]!;
        return path.PathEllipsis((int)(width / (fontSize * _averageCharacterWidthFactor)));
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
