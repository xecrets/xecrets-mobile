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

namespace Xecrets.Mobile.Abstractions;

/// <summary>
/// The attached purpose of an entry for a password, which opts the entry in to the password configuration of the
/// handler mappings of each platform.
/// </summary>
internal static class PasswordEntryProperties
{
    /// <summary>
    /// The key of the handler mapping that applies the password configuration of the platform, such as autofill hints
    /// and content type, to the entries that have a purpose.
    /// </summary>
    public static string ConfigureMapperKey => "Xecrets.ConfigurePasswordEntry";

    private static readonly BindableProperty _purposeProperty = BindableProperty.CreateAttached(
        "Purpose", typeof(PasswordEntryPurpose?), typeof(PasswordEntryProperties), null);

    /// <summary>
    /// Gets the purpose of an entry.
    /// </summary>
    /// <param name="view">The entry.</param>
    /// <returns>The purpose, or null if the entry is not for a password.</returns>
    public static PasswordEntryPurpose? GetPurpose(BindableObject view) =>
        (PasswordEntryPurpose?)view.GetValue(_purposeProperty);

    /// <summary>
    /// Sets the purpose of an entry.
    /// </summary>
    /// <param name="view">The entry.</param>
    /// <param name="value">The purpose, or null if the entry is not for a password.</param>
    public static void SetPurpose(BindableObject view, PasswordEntryPurpose? value) =>
        view.SetValue(_purposeProperty, value);
}
