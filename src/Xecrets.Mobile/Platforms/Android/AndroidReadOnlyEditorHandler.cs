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

using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// MAUI makes a read-only editor unfocusable, so its text cannot be selected. This one is selectable instead, and
/// has no key listener, so it still cannot be changed, does not show the keyboard and offers no cut or paste.
/// </summary>
internal sealed class AndroidReadOnlyEditorHandler() : EditorHandler(ReadOnlyMapper)
{
    // These mappings would make the editor unfocusable again or, by setting the input type, put a key listener back.
    private static readonly IPropertyMapper<IEditor, AndroidReadOnlyEditorHandler> ReadOnlyMapper =
        new PropertyMapper<IEditor, AndroidReadOnlyEditorHandler>(EditorHandler.Mapper)
        {
            [nameof(IEditor.IsReadOnly)] = static (_, _) => { },
            [nameof(IEditor.IsTextPredictionEnabled)] = static (_, _) => { },
            [nameof(IEditor.IsSpellCheckEnabled)] = static (_, _) => { },
            [nameof(IEditor.Keyboard)] = static (_, _) => { },
        };

    // MAUI only listens for text changes and sets the text after this, so making it selectable, which sets the text
    // again, does not affect the editor's text.
    protected override MauiAppCompatEditText CreatePlatformView()
    {
        MauiAppCompatEditText platformView = base.CreatePlatformView();
        platformView.KeyListener = null;
        platformView.SetTextIsSelectable(true);
        return platformView;
    }
}
