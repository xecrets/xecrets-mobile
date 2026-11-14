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

using Xecrets.Words.Model;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// A page model with a new password, its strength and its confirmation.
/// </summary>
public interface INewPasswordPageModel
{
    /// <summary>
    /// The password entered.
    /// </summary>
    string Password { get; set; }

    /// <summary>
    /// The confirmation of the password entered.
    /// </summary>
    string ConfirmPassword { get; set; }

    /// <summary>
    /// The strength of <see cref="Password"/>, from 0 to 1.
    /// </summary>
    double PasswordStrength { get; }

    /// <summary>
    /// The color that shows the strength of <see cref="Password"/>.
    /// </summary>
    StrengthColor PasswordStrengthColor { get; }

    /// <summary>
    /// True if a new password is entered, with its strength and confirmation, false if only an existing password is
    /// entered.
    /// </summary>
    bool IsEditable { get; }
}
