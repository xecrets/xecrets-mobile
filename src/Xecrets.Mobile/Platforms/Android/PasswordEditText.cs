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

using Android.Content;
using Android.Text.Method;
using Android.Views;

using Microsoft.Maui.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// The platform view of a password entry. Android copies and cuts text without marking it as sensitive, so this marks
/// a shown password copied or cut from it as sensitive, so that the system's confirmation of the copy does not show
/// it. It can also be kept out of autofill entirely.
/// </summary>
/// <param name="context">The context of the view.</param>
internal sealed class PasswordEditText(Context context) : MauiAppCompatEditText(context)
{
    /// <summary>
    /// True to keep the entry out of autofill, so that nothing is suggested, filled or offered to be saved, false
    /// otherwise.
    /// </summary>
    public bool IsAutofillExcluded { get; set; }

    /// <inheritdoc/>
    /// <remarks>
    /// None while <see cref="IsAutofillExcluded"/> is true. Since Android 14, autofill also triggers on views that are
    /// not important for autofill, but never on views without an autofill type.
    /// </remarks>
    public override AutofillType AutofillType => IsAutofillExcluded ? AutofillType.None : base.AutofillType;

    /// <inheritdoc/>
    /// <remarks>
    /// A shown password that is copied or cut is marked as sensitive. A hidden password is copied as the dots it is
    /// shown as, so it needs no marking.
    /// </remarks>
    public override bool OnTextContextMenuItem(int id) =>
        (TransformationMethod is not PasswordTransformationMethod && this.TryCopyOrCutAsSensitive(id))
        || base.OnTextContextMenuItem(id);
}
