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

using Fonts;

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Xecrets.Mobile.Controls;

public sealed partial class PasswordEntry
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(PasswordEntry),
        string.Empty,
        BindingMode.TwoWay);

    private bool _isPasswordVisible;

    public PasswordEntry()
    {
        InitializeComponent();
        PasswordTextEntry.SetBinding(
            Entry.TextProperty,
            static (PasswordEntry passwordEntry) => passwordEntry.Text,
            BindingMode.TwoWay,
            source: this);
    }

    public Entry Entry => PasswordTextEntry;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => PasswordTextEntry.Placeholder;
        set => PasswordTextEntry.Placeholder = value;
    }

    public ReturnType ReturnType
    {
        get => PasswordTextEntry.ReturnType;
        set => PasswordTextEntry.ReturnType = value;
    }

    private static FontImageSource CreateVisibilityIcon(string glyph)
    {
        FontImageSource imageSource = new()
        {
            Glyph = glyph,
            FontFamily = FluentUI.FontFamily,
            Size = 24,
        };
        imageSource.SetAppThemeColor(
            FontImageSource.ColorProperty,
            (Color)Application.Current!.Resources["DarkOnLightBackground"],
            (Color)Application.Current.Resources["LightOnDarkBackground"]);
        return imageSource;
    }

    private void OnVisibilityButtonClicked(object? sender, EventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        PasswordTextEntry.IsPassword = !_isPasswordVisible;
        VisibilityButton.Source = CreateVisibilityIcon(_isPasswordVisible
            ? FluentUI.eye_24_regular
            : FluentUI.eye_off_24_regular);
    }
}
