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

using Android.Views;
using Android.Views.InputMethods;
using AndroidTextAlignment = Android.Views.TextAlignment;

using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// Gives editors their own platform view, <see cref="SensitiveEditText"/>, and asks the keyboard not to learn from the
/// decrypted text in them.
/// </summary>
internal class AndroidEditorHandler : EditorHandler
{
    public AndroidEditorHandler()
    {
    }

    protected AndroidEditorHandler(IPropertyMapper mapper) : base(mapper)
    {
    }

    // Set up as MAUI sets up its own platform view, which is otherwise only created there.
    protected override MauiAppCompatEditText CreatePlatformView()
    {
        SensitiveEditText platformView = new(Context)
        {
            ImeOptions = ImeAction.Done | (ImeAction)ImeFlags.NoPersonalizedLearning,
            TextAlignment = AndroidTextAlignment.ViewStart,
            Gravity = GravityFlags.Top,
        };
        platformView.SetSingleLine(false);
        platformView.SetHorizontallyScrolling(false);
        return platformView;
    }
}
