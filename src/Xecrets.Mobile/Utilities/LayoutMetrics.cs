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

using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Xecrets.Mobile.Utilities;

public static class LayoutMetrics
{
    private const double _actionButtonStackMaximumWidth = 420;

    private const double _contentColumnMaximumWidth = 560;

    public static void UpdateCenteredContentLayout(
        double pageWidth,
        Layout contentRoot,
        Border contentColumn,
        VisualElement actionButtonStack,
        Thickness? contentPadding = null)
    {
        double availableContentWidth = pageWidth - contentRoot.Padding.Left - contentRoot.Padding.Right;
        if (availableContentWidth <= 0)
        {
            return;
        }

        double contentColumnWidth = Math.Min(availableContentWidth, _contentColumnMaximumWidth);
        contentColumn.WidthRequest = contentColumnWidth;

        // The button stack sits inside the card body, so it has to fit that content box, not the card's
        // outer width. The card body can supply its own padding when the card also has an unpadded header.
        // Sizing it to the outer width overflows the card by its padding and stroke, which
        // iOS hides by clipping to the border and Android shows as buttons running off the screen edge.
        double cardContentWidth = contentColumnWidth
            - (contentPadding ?? contentColumn.Padding).HorizontalThickness
            - (2 * contentColumn.StrokeThickness);
        actionButtonStack.WidthRequest = Math.Min(cardContentWidth, _actionButtonStackMaximumWidth);
    }
}
