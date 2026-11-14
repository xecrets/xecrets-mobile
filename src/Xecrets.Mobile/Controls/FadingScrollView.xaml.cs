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

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Xecrets.Mobile.Controls;

/// <summary>
/// A scroll view with a fade and a chevron over its top and bottom edges while there is more to scroll to in that
/// direction. The fade is to the background the scroll view is on, given by <see cref="FadeColor"/>.
/// </summary>
[ContentProperty(nameof(ScrollContent))]
internal sealed partial class FadingScrollView
{
    /// <summary>
    /// Identifies the <see cref="ScrollContent"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ScrollContentProperty = BindableProperty.Create(
        nameof(ScrollContent), typeof(View), typeof(FadingScrollView),
        propertyChanged: (bindable, _, newValue) => ((FadingScrollView)bindable).Scroll.Content = (View?)newValue);

    /// <summary>
    /// Identifies the <see cref="FadeColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty FadeColorProperty = BindableProperty.Create(
        nameof(FadeColor), typeof(Color), typeof(FadingScrollView), Colors.White,
        propertyChanged: (bindable, _, newValue) => ((FadingScrollView)bindable).UpdateFadeColor((Color)newValue));

    /// <summary>
    /// Initializes a new instance of the <see cref="FadingScrollView"/> class, with the fades following the scroll
    /// position, the size and the content size.
    /// </summary>
    public FadingScrollView()
    {
        InitializeComponent();
        UpdateFadeColor(FadeColor);
        Scroll.Scrolled += (_, _) => UpdateScrollFades();
        Scroll.SizeChanged += (_, _) => UpdateScrollFades();
        Scroll.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ScrollView.ContentSize))
            {
                UpdateScrollFades();
            }
        };
    }

    /// <summary>
    /// The content that is scrolled.
    /// </summary>
    public View? ScrollContent
    {
        get => (View?)GetValue(ScrollContentProperty);
        set => SetValue(ScrollContentProperty, value);
    }

    /// <summary>
    /// The background color the scroll view is on, which the edges fade to. White unless set.
    /// </summary>
    public Color FadeColor
    {
        get => (Color)GetValue(FadeColorProperty);
        set => SetValue(FadeColorProperty, value);
    }

    // From the background at the edge to the same color without opacity, so that there is no gray band.
    private void UpdateFadeColor(Color color)
    {
        TopFadeEdge.Color = color;
        TopFadeInner.Color = color.WithAlpha(0);
        BottomFadeInner.Color = color.WithAlpha(0);
        BottomFadeEdge.Color = color;
    }

    // A fade at the top when scrolled down, and at the bottom when there is more below. The margin keeps a fade from
    // flickering at the very end of a scroll, where the position can stop a fraction short.
    private void UpdateScrollFades()
    {
        const double margin = 1;
        TopScrollFade.IsVisible = Scroll.ScrollY > margin;
        BottomScrollFade.IsVisible = Scroll.ScrollY + Scroll.Height < Scroll.ContentSize.Height - margin;
    }
}
