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

using System.Linq;
using System.Windows.Input;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Xecrets.Mobile.Controls;

/// <summary>
/// The card of a page, with a title and optional header actions in the top row, a scrollable body below it, and an
/// optional body header above and footer below the body that stay visible when the body scrolls.
/// </summary>
[ContentProperty(nameof(Body))]
internal sealed partial class PageCard
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(PageCard), string.Empty);

    public static readonly BindableProperty BodyPaddingProperty = BindableProperty.Create(
        nameof(BodyPadding), typeof(Thickness), typeof(PageCard));

    public static readonly BindableProperty HeaderActionsProperty = BindableProperty.Create(
        nameof(HeaderActions), typeof(View), typeof(PageCard),
        propertyChanged: (bindable, _, newValue) =>
        {
            PageCard card = (PageCard)bindable;
            card.HeaderActionsHost.Content = (View?)newValue;
            card.UpdateTitleRowVisibility();
        });

    // For a card whose title is needed to tell what the page is for, even when the height is compact.
    public static readonly BindableProperty IsTitleAlwaysVisibleProperty = BindableProperty.Create(
        nameof(IsTitleAlwaysVisible), typeof(bool), typeof(PageCard),
        propertyChanged: (bindable, _, _) => ((PageCard)bindable).UpdateTitleRowVisibility());

    public static readonly BindableProperty IsTitleHiddenWhenCompactProperty = BindableProperty.Create(
        nameof(IsTitleHiddenWhenCompact), typeof(bool), typeof(PageCard),
        propertyChanged: (bindable, _, _) => ((PageCard)bindable).UpdateTitleRowVisibility());

    // From the dynamic resource set by LayoutMetrics.UpdateCompactHeightResources.
    private static readonly BindableProperty IsCompactHeightProperty = BindableProperty.Create(
        "IsCompactHeight", typeof(bool), typeof(PageCard),
        propertyChanged: (bindable, _, _) => ((PageCard)bindable).UpdateTitleRowVisibility());

    public static readonly BindableProperty BodyProperty = BindableProperty.Create(
        nameof(Body), typeof(View), typeof(PageCard),
        propertyChanged: (bindable, _, newValue) => ((PageCard)bindable).BodyHost.Content = (View?)newValue);

    // The body header and the footer are set once from the page. Their rows are only added with them, so that a card
    // without them has no extra spacing.
    public static readonly BindableProperty BodyHeaderProperty = BindableProperty.Create(
        nameof(BodyHeader), typeof(View), typeof(PageCard),
        propertyChanged: (bindable, _, _) => ((PageCard)bindable).UpdateBodyHeaderPlacement());

    public static readonly BindableProperty FooterProperty = BindableProperty.Create(
        nameof(Footer), typeof(View), typeof(PageCard),
        propertyChanged: (bindable, _, newValue) =>
        {
            Grid cardGrid = ((PageCard)bindable).CardGrid;
            cardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            cardGrid.Add((View)newValue, 0, cardGrid.RowDefinitions.Count - 1);
        });

    public PageCard()
    {
        InitializeComponent();
        SetDynamicResource(IsCompactHeightProperty, "IsCompactHeight");
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public Thickness BodyPadding
    {
        get => (Thickness)GetValue(BodyPaddingProperty);
        set => SetValue(BodyPaddingProperty, value);
    }

    public bool IsTitleAlwaysVisible
    {
        get => (bool)GetValue(IsTitleAlwaysVisibleProperty);
        set => SetValue(IsTitleAlwaysVisibleProperty, value);
    }

    public bool IsTitleHiddenWhenCompact
    {
        get => (bool)GetValue(IsTitleHiddenWhenCompactProperty);
        set => SetValue(IsTitleHiddenWhenCompactProperty, value);
    }

    public View? HeaderActions
    {
        get => (View?)GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    public View? Body
    {
        get => (View?)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public View? BodyHeader
    {
        get => (View?)GetValue(BodyHeaderProperty);
        set => SetValue(BodyHeaderProperty, value);
    }

    public View? Footer
    {
        get => (View?)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>
    /// The width inside the body padding and the card stroke, for a card of the given width.
    /// </summary>
    public double BodyWidth(double cardWidth)
    {
        return cardWidth - BodyPadding.HorizontalThickness - (2 * CardBorder.StrokeThickness);
    }

    // When the height is compact, the title is left out to save height, since the breadcrumb or the header shows where
    // the user is. Header actions keep the row, and the title with them, since they need the space anyway. A card can
    // also always keep its title. The row spacing below a hidden row stays, as the space above the body. A card can
    // instead hide only the title and have the header actions over the top of the row below, raised by the row spacing
    // to where they would be in their own row.
    private void UpdateTitleRowVisibility()
    {
        bool isCompactHeight = (bool)GetValue(IsCompactHeightProperty);
        bool isOverBody = isCompactHeight && IsTitleHiddenWhenCompact && HeaderActions is not null;
        TitleRow.IsVisible = !isCompactHeight || HeaderActions is not null || IsTitleAlwaysVisible;
        TitleLabel.IsVisible = !isOverBody;
        Grid.SetRow(TitleRow, isOverBody ? 1 : 0);
        TitleRow.Margin = isOverBody ? new Thickness(0, -CardGrid.RowSpacing, 0, 0) : new Thickness(0);
        UpdateBodyHeaderPlacement();
    }

    // When the height is compact and the title row is shown, the body header is in the title row to save height. It
    // spans the row, so that it is centered in the card as the breadcrumb above it is, not in the space between the
    // title and the header actions. Otherwise it is in a row of its own below the title row.
    private void UpdateBodyHeaderPlacement()
    {
        if (BodyHeader is not View bodyHeader)
        {
            return;
        }

        bool isInTitleRow = (bool)GetValue(IsCompactHeightProperty) && TitleRow.IsVisible && TitleLabel.IsVisible;
        if (bodyHeader.Parent == (isInTitleRow ? TitleRow : CardGrid))
        {
            return;
        }

        if (isInTitleRow)
        {
            if (bodyHeader.Parent == CardGrid)
            {
                CardGrid.Remove(bodyHeader);
                CardGrid.RowDefinitions.RemoveAt(1);
                MoveRowsFrom(2, -1);
            }

            TitleRow.Add(bodyHeader, 0, 0);
            Grid.SetColumnSpan(bodyHeader, TitleRow.ColumnDefinitions.Count);
            return;
        }

        if (bodyHeader.Parent == TitleRow)
        {
            TitleRow.Remove(bodyHeader);
            Grid.SetColumnSpan(bodyHeader, 1);
        }

        // Below the title row, moving the rows below it down.
        MoveRowsFrom(1, 1);
        CardGrid.RowDefinitions.Insert(1, new RowDefinition(GridLength.Auto));
        CardGrid.Add(bodyHeader, 0, 1);
    }

    // The title row stays where UpdateTitleRowVisibility puts it.
    private void MoveRowsFrom(int row, int offset)
    {
        foreach (BindableObject child in CardGrid.Children.Cast<BindableObject>().Where(child => child != TitleRow && Grid.GetRow(child) >= row))
        {
            Grid.SetRow(child, Grid.GetRow(child) + offset);
        }
    }
}
