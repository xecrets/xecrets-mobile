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

using Android.Content;
using Android.Views;
using Android.Views.TextClassifiers;

using Microsoft.Maui.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// The platform view of an editor, which shows decrypted text. It is kept out of autofill, content capture, smart text
/// selection and accessibility services other than accessibility tools, so that the text is not passed to them, and
/// text copied from it is marked as sensitive.
/// </summary>
internal sealed class SensitiveEditText : MauiAppCompatEditText
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveEditText"/> class, kept out of the services that the text
    /// is not to be passed to.
    /// </summary>
    /// <param name="context">The context of the view.</param>
    public SensitiveEditText(Context context) : base(context)
    {
        ImportantForAutofill = ImportantForAutofill.NoExcludeDescendants;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            ImportantForContentCapture = (int)ViewImportantForContentCapture.NoExcludeDescendants;
        }

        // Smart text selection passes the selected text, and the surrounding text, to the system's text classifier.
        TextClassifier = ITextClassifier.NoOp!;

        // Only services that declare themselves accessibility tools, such as screen readers, are then given the text.
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            SetAccessibilityDataSensitive(AccessibilityDataSensitive.Yes);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Always none, since Android 14 and later autofill also views that are not important for autofill, but never
    /// views without an autofill type.
    /// </remarks>
    public override AutofillType AutofillType => AutofillType.None;

    /// <inheritdoc/>
    /// <remarks>
    /// Text that is copied or cut is marked as sensitive.
    /// </remarks>
    public override bool OnTextContextMenuItem(int id) =>
        this.TryCopyOrCutAsSensitive(id) || base.OnTextContextMenuItem(id);
}
