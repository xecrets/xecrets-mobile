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

using System.Windows.Input;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Xecrets.Mobile.Controls;

/// <summary>
/// The card of a page, with a title, optional header actions and a page information button in the top row, and a
/// scrollable body below it.
/// </summary>
[ContentProperty(nameof(Body))]
internal sealed partial class PageCard
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(PageCard), string.Empty);

    public static readonly BindableProperty InformationProperty = BindableProperty.Create(
        nameof(Information), typeof(string), typeof(PageCard), string.Empty);

    public static readonly BindableProperty BodyPaddingProperty = BindableProperty.Create(
        nameof(BodyPadding), typeof(Thickness), typeof(PageCard));

    public static readonly BindableProperty HeaderActionsProperty = BindableProperty.Create(
        nameof(HeaderActions), typeof(View), typeof(PageCard),
        propertyChanged: (bindable, _, newValue) => ((PageCard)bindable).HeaderActionsHost.Content = (View?)newValue);

    public static readonly BindableProperty BodyProperty = BindableProperty.Create(
        nameof(Body), typeof(View), typeof(PageCard),
        propertyChanged: (bindable, _, newValue) => ((PageCard)bindable).BodyHost.Content = (View?)newValue);

    public PageCard()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Information
    {
        get => (string)GetValue(InformationProperty);
        set => SetValue(InformationProperty, value);
    }

    public Thickness BodyPadding
    {
        get => (Thickness)GetValue(BodyPaddingProperty);
        set => SetValue(BodyPaddingProperty, value);
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

    /// <summary>
    /// The width inside the body padding and the card stroke, for a card of the given width.
    /// </summary>
    public double BodyWidth(double cardWidth)
    {
        return cardWidth - BodyPadding.HorizontalThickness - (2 * CardBorder.StrokeThickness);
    }
}
