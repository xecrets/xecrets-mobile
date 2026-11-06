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

using System.Collections.Generic;
using System.Linq;

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

namespace Xecrets.Mobile.Utilities;

/// <summary>
/// Moves the action buttons of a page between a single column stack and a two column grid. On a phone in landscape a
/// single column is too tall for the screen, so the buttons are then shown in order in two columns, without the
/// dividers of the stack. The grid starts hidden and empty, and the stack is restored as it was.
/// </summary>
internal sealed class ActionButtonColumns
{
    private readonly Layout _stack;
    private readonly Grid _grid;
    private readonly List<(View Button, int Index)> _buttons;

    public ActionButtonColumns(Layout stack, Grid grid)
    {
        _stack = stack;
        _grid = grid;
        _buttons = [.. stack.Children
            .Select((child, index) => (Child: (View)child, Index: index))
            .Where(item => item.Child is not BoxView)];

        for (int i = 0; i < (_buttons.Count + 1) / 2; ++i)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
    }

    public bool IsTwoColumns { get; private set; }

    public void Update(double pageWidth, double pageHeight)
    {
        bool isTwoColumns = DeviceInfo.Current.Idiom == DeviceIdiom.Phone && pageWidth > pageHeight;
        if (isTwoColumns == IsTwoColumns)
        {
            return;
        }

        IsTwoColumns = isTwoColumns;
        if (isTwoColumns)
        {
            for (int i = 0; i < _buttons.Count; ++i)
            {
                View button = _buttons[i].Button;
                _ = _stack.Remove(button);
                _grid.Add(button, i % 2, i / 2);
            }
        }
        else
        {
            _grid.Clear();
            foreach ((View button, int index) in _buttons)
            {
                _stack.Insert(index, button);
            }
        }

        _stack.IsVisible = !isTwoColumns;
        _grid.IsVisible = isTwoColumns;
    }
}
