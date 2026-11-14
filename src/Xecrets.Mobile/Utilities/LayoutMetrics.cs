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
using Microsoft.Maui.Devices;

using Xecrets.Mobile.Controls;

namespace Xecrets.Mobile.Utilities;

/// <summary>
/// The layout of the pages that depends on the size of the page and the device: whether the height is compact, and the
/// widths of the content column, the card and the action buttons, centered on the page.
/// </summary>
internal static class LayoutMetrics
{
    private const double _actionButtonStackMaximumWidth = 420;

    private const double _contentColumnMaximumWidth = 960;

    // A form, such as a password entry, is narrower than a list, since a field much wider than what is typed into it
    // only spreads out the form on a tablet or a phone in landscape.
    private const double _formContentColumnMaximumWidth = 600;

    /// <summary>
    /// Tells if there is little height for the content, as on a phone in landscape.
    /// </summary>
    /// <param name="width">The width of the page or window.</param>
    /// <param name="height">The height of the page or window.</param>
    /// <returns>True if the height is compact, false otherwise.</returns>
    public static bool IsCompactHeight(double width, double height)
    {
        return DeviceInfo.Current.Idiom == DeviceIdiom.Phone && width > height;
    }

    /// <summary>
    /// Sets the dynamic resources that styles and controls use to save height, from their regular or compact variants.
    /// </summary>
    /// <param name="resources">The resources of the app.</param>
    /// <param name="isCompactHeight">True if the height is compact, false otherwise.</param>
    public static void UpdateCompactHeightResources(ResourceDictionary resources, bool isCompactHeight)
    {
        resources["IsCompactHeight"] = isCompactHeight;
        resources["EntryMinimumHeight"] = isCompactHeight
            ? (double)resources["CompactEntryMinimumHeight"]
            : (double)(OnIdiom<double>)resources["RegularEntryMinimumHeight"];
        resources["PasswordStrengthMargin"] = isCompactHeight
            ? (Thickness)resources["CompactPasswordStrengthMargin"]
            : (Thickness)(OnIdiom<Thickness>)resources["RegularPasswordStrengthMargin"];
        resources["FormSpacing"] = isCompactHeight
            ? (double)resources["CompactFormSpacing"]
            : (double)(OnIdiom<double>)resources["LayoutSpacing"];
        resources["FormMargin"] = isCompactHeight
            ? (Thickness)resources["CompactFormMargin"]
            : (Thickness)resources["RegularFormMargin"];
        resources["DetailRowSpacing"] = isCompactHeight
            ? (double)resources["CompactDetailRowSpacing"]
            : (double)resources["RegularDetailRowSpacing"];
        resources["CardBodyHeaderPadding"] = isCompactHeight
            ? (Thickness)resources["CompactCardBodyHeaderPadding"]
            : (Thickness)resources["RegularCardBodyHeaderPadding"];
        resources["Title1FontSize"] = isCompactHeight
            ? (double)resources["CompactFontSizeTitle1"]
            : (double)(OnIdiom<double>)resources["FontSizeTitle1"];
    }

    /// <summary>
    /// Sets the dynamic resources that depend on the display density, since the platform draws some parts in whole pixels.
    /// </summary>
    /// <param name="resources">The resources of the app.</param>
    /// <param name="density">The display density.</param>
    public static void UpdateDensityResources(ResourceDictionary resources, double density)
    {
        // The platform rounds the inset of the underline down to whole pixels, so the meter is inset as much for their
        // ends to line up at any density.
        double inset = Math.Floor((double)resources["TextFieldUnderlineInset"] * density) / density;
        resources["PasswordStrengthInset"] = new Thickness(inset, 0);
    }

    /// <summary>
    /// Sizes the card and the action buttons of a page with a form, which is narrower than other content, since a
    /// field much wider than what is typed into it only spreads out the form.
    /// </summary>
    /// <param name="pageWidth">The width of the page.</param>
    /// <param name="contentRoot">The layout of the content, whose padding is kept.</param>
    /// <param name="card">The card of the page.</param>
    /// <param name="actionButtonStack">The action buttons, in the body of the card.</param>
    public static void UpdateCenteredFormLayout(double pageWidth, Layout contentRoot, PageCard card, View actionButtonStack)
    {
        UpdateCenteredLayout(pageWidth, contentRoot, card, actionButtonStack, _formContentColumnMaximumWidth, _actionButtonStackMaximumWidth);
    }

    /// <summary>
    /// Sizes the card and the two columns of action buttons of a page, each column at most as wide as a single column
    /// of buttons.
    /// </summary>
    /// <param name="pageWidth">The width of the page.</param>
    /// <param name="contentRoot">The layout of the content, whose padding is kept.</param>
    /// <param name="card">The card of the page.</param>
    /// <param name="actionButtonGrid">The two column grid of the action buttons, in the body of the card.</param>
    public static void UpdateCenteredTwoColumnLayout(double pageWidth, Layout contentRoot, PageCard card, Grid actionButtonGrid)
    {
        UpdateCenteredLayout(pageWidth, contentRoot, card, actionButtonGrid, _contentColumnMaximumWidth, (2 * _actionButtonStackMaximumWidth) + actionButtonGrid.ColumnSpacing);
    }

    /// <summary>
    /// Sizes the card and the buttons in icon rows, with an icon button on each side, so that the buttons are as wide
    /// as other action buttons.
    /// </summary>
    /// <param name="pageWidth">The width of the page.</param>
    /// <param name="contentRoot">The layout of the content, whose padding is kept.</param>
    /// <param name="card">The card of the page.</param>
    /// <param name="actionButtonStack">The icon rows, in the body of the card.</param>
    /// <param name="iconRowMargin">The margin of a standalone button in the rows, which lines it up with the row
    /// buttons.</param>
    public static void UpdateCenteredIconRowLayout(double pageWidth, Layout contentRoot, PageCard card, View actionButtonStack, Thickness iconRowMargin)
    {
        UpdateCenteredLayout(pageWidth, contentRoot, card, actionButtonStack, _contentColumnMaximumWidth, _actionButtonStackMaximumWidth + iconRowMargin.HorizontalThickness);
    }

    /// <summary>
    /// Sizes the card and the action buttons of a page.
    /// </summary>
    /// <param name="pageWidth">The width of the page.</param>
    /// <param name="contentRoot">The layout of the content, whose padding is kept.</param>
    /// <param name="card">The card of the page.</param>
    /// <param name="actionButtonStack">The action buttons, in the body of the card.</param>
    /// <param name="actionButtonStackMaximumWidth">The largest width of the action buttons.</param>
    public static void UpdateCenteredContentLayout(
        double pageWidth,
        Layout contentRoot,
        PageCard card,
        View actionButtonStack,
        double actionButtonStackMaximumWidth = _actionButtonStackMaximumWidth)
    {
        UpdateCenteredLayout(pageWidth, contentRoot, card, actionButtonStack, _contentColumnMaximumWidth, actionButtonStackMaximumWidth);
    }

    private static void UpdateCenteredLayout(
        double pageWidth,
        Layout contentRoot,
        PageCard card,
        View actionButtonStack,
        double contentColumnMaximumWidth,
        double actionButtonStackMaximumWidth)
    {
        double availableContentWidth = pageWidth - contentRoot.Padding.Left - contentRoot.Padding.Right;
        if (availableContentWidth <= 0)
        {
            return;
        }

        double contentColumnWidth = Math.Min(availableContentWidth, contentColumnMaximumWidth);
        card.WidthRequest = contentColumnWidth;

        // The button stack sits inside the card body, so it has to fit that content box, not the card's
        // outer width. Sizing it to the outer width overflows the card by its padding and stroke, which
        // iOS hides by clipping to the border and Android shows as buttons running off the screen edge.
        double cardContentWidth = card.BodyWidth(contentColumnWidth) - actionButtonStack.Margin.HorizontalThickness;
        UpdateActionButtonStackWidth(cardContentWidth, actionButtonStack, actionButtonStackMaximumWidth);
    }

    /// <summary>
    /// Sizes action buttons to the width available, up to a largest width. Nothing is done before there is a width.
    /// </summary>
    /// <param name="availableWidth">The width available.</param>
    /// <param name="actionButtonStack">The action buttons.</param>
    /// <param name="actionButtonStackMaximumWidth">The largest width of the action buttons.</param>
    public static void UpdateActionButtonStackWidth(
        double availableWidth,
        VisualElement actionButtonStack,
        double actionButtonStackMaximumWidth = _actionButtonStackMaximumWidth)
    {
        if (availableWidth <= 0)
        {
            return;
        }

        actionButtonStack.WidthRequest = Math.Min(availableWidth, actionButtonStackMaximumWidth);
    }

    /// <summary>
    /// Sizes two action buttons side by side, each at most as wide as a single column of buttons.
    /// </summary>
    /// <param name="availableWidth">The width available.</param>
    /// <param name="actionButtonGrid">The grid of the two buttons.</param>
    public static void UpdateTwoColumnActionButtonWidth(double availableWidth, Grid actionButtonGrid)
    {
        UpdateActionButtonStackWidth(availableWidth, actionButtonGrid, (2 * _actionButtonStackMaximumWidth) + actionButtonGrid.ColumnSpacing);
    }
}
