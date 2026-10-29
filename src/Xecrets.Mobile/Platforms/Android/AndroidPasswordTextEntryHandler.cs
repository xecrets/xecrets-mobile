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
using Android.Text.Method;

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// Android copies and cuts text without marking it as sensitive, so this marks a shown password copied or cut from a
/// password entry as sensitive, so that the system's confirmation of the copy does not show it.
/// </summary>
internal sealed class AndroidPasswordTextEntryHandler : EntryHandler
{
    protected override MauiAppCompatEditText CreatePlatformView() => new SensitiveCopyEditText(Context);

    private sealed class SensitiveCopyEditText : MauiAppCompatEditText
    {
        public SensitiveCopyEditText(Context context) : base(context)
        {
        }

        // Every copy and cut ends up here: the selection toolbar, keyboard shortcuts, the keyboard app and
        // accessibility services.
        public override bool OnTextContextMenuItem(int id)
        {
            // A hidden password is copied as the dots it is shown as, so only a shown one needs marking.
            if ((id != global::Android.Resource.Id.Copy && id != global::Android.Resource.Id.Cut)
                || TransformationMethod is PasswordTransformationMethod)
            {
                return base.OnTextContextMenuItem(id);
            }

            int start = Math.Max(0, Math.Min(SelectionStart, SelectionEnd));
            int end = Math.Max(0, Math.Max(SelectionStart, SelectionEnd));
            Context!.CopySensitiveText(Text![start..end]);

            // Collapsing the selection closes the selection toolbar, as Android does after a copy or cut.
            if (id == global::Android.Resource.Id.Cut)
            {
                EditableText!.Delete(start, end);
            }
            else
            {
                SetSelection(end);
            }

            return true;
        }
    }
}
