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
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace Xecrets.Mobile.Controls;

/// <summary>
/// An entry of text to search for in decrypted text, which is as sensitive as the text itself. Like the editors of the
/// text, it is kept from the services that the text is not to be passed to, and the keyboard is asked not to learn from
/// it, which takes platform specific handlers and mappings. Whenever it is shown it takes the focus, with its text
/// selected, so that typing starts a new search, as in the desktop app.
/// </summary>
internal sealed class SearchTextEntry : Entry
{
    // MAUI shows the platform view of each element in the mapping of its own visibility, and when a container is shown,
    // it maps the container before the elements in it, so the entry can only take the focus once its own is mapped.
    static SearchTextEntry()
    {
        EntryHandler.Mapper.AppendToMapping(nameof(IView.Visibility), static (_, view) =>
        {
            if (view is SearchTextEntry entry && view.Visibility == Visibility.Visible)
            {
                entry.FocusWithTextSelected();
            }
        });
    }

    // Selected after the focus is taken, which on iOS puts the cursor at the end.
    private void FocusWithTextSelected()
    {
        Focus();
        CursorPosition = 0;
        SelectionLength = Text.Length;
    }
}
