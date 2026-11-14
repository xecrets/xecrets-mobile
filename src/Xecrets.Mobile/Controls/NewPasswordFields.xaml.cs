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

using Microsoft.Maui.Controls;

namespace Xecrets.Mobile.Controls;

/// <summary>
/// The new password of the profile, its strength and its confirmation, bound to an
/// <see cref="Models.Abstractions.INewPasswordPageModel"/>.
/// </summary>
internal sealed partial class NewPasswordFields
{
    /// <summary>
    /// Identifies the <see cref="ReturnCommand"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ReturnCommandProperty = BindableProperty.Create(
        nameof(ReturnCommand),
        typeof(ICommand),
        typeof(NewPasswordFields));

    /// <summary>
    /// Initializes a new instance of the <see cref="NewPasswordFields"/> class, bound to the
    /// <see cref="Models.Abstractions.INewPasswordPageModel"/> that is its binding context.
    /// </summary>
    public NewPasswordFields()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The entry of the password.
    /// </summary>
    public PasswordEntry PasswordEntry => PasswordEntryControl;

    /// <summary>
    /// The entry of the confirmation.
    /// </summary>
    public PasswordEntry ConfirmPasswordEntry => ConfirmPasswordEntryControl;

    /// <summary>
    /// The text field of the entry of the password.
    /// </summary>
    public Entry PasswordTextEntry => PasswordEntryControl.Entry;

    /// <summary>
    /// Runs when the return key is pressed in the confirmation, if the command can execute.
    /// </summary>
    public ICommand? ReturnCommand
    {
        get => (ICommand?)GetValue(ReturnCommandProperty);
        set => SetValue(ReturnCommandProperty, value);
    }
}
