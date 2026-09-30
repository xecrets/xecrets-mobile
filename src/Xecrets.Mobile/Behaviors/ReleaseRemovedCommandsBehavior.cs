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

using System.Linq;

using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Xecrets.Mobile.Behaviors;

/// <summary>
/// Clears the command bindings of children removed from a layout, such as rows discarded by a
/// <see cref="BindableLayout"/>. A removed row is otherwise still subscribed to its commands, and when they notify,
/// MAUI reapplies the bindings of the detached row, which fails.
/// </summary>
public sealed partial class ReleaseRemovedCommandsBehavior : Behavior<Layout>
{
    protected override void OnAttachedTo(Layout bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.ChildRemoved += OnChildRemoved;
    }

    protected override void OnDetachingFrom(Layout bindable)
    {
        bindable.ChildRemoved -= OnChildRemoved;
        base.OnDetachingFrom(bindable);
    }

    // MAUI has no public way to enumerate the bindings of an element, so the command properties of the controls and
    // gesture recognizers are listed here. Every property can be tried on every object, since RemoveBinding does
    // nothing without a binding, and IsSet is false for a property the object does not have.
    private static readonly BindableProperty[] CommandProperties =
    [
        Button.CommandProperty,
        ImageButton.CommandProperty,
        CheckBox.CommandProperty,
        RefreshView.CommandProperty,
        SearchBar.SearchCommandProperty,
        Entry.ReturnCommandProperty,
        Slider.DragStartedCommandProperty,
        Slider.DragCompletedCommandProperty,
        SwipeItemView.CommandProperty,
        MenuItem.CommandProperty,
        SelectableItemsView.SelectionChangedCommandProperty,
        ItemsView.RemainingItemsThresholdReachedCommandProperty,
        CarouselView.CurrentItemChangedCommandProperty,
        CarouselView.PositionChangedCommandProperty,
        TapGestureRecognizer.CommandProperty,
        SwipeGestureRecognizer.CommandProperty,
        PointerGestureRecognizer.PointerEnteredCommandProperty,
        PointerGestureRecognizer.PointerExitedCommandProperty,
        PointerGestureRecognizer.PointerMovedCommandProperty,
        PointerGestureRecognizer.PointerPressedCommandProperty,
        PointerGestureRecognizer.PointerReleasedCommandProperty,
        DragGestureRecognizer.DragStartingCommandProperty,
        DragGestureRecognizer.DropCompletedCommandProperty,
        DropGestureRecognizer.DragOverCommandProperty,
        DropGestureRecognizer.DragLeaveCommandProperty,
        DropGestureRecognizer.DropCommandProperty,
    ];

    private static void OnChildRemoved(object? sender, ElementEventArgs e) => Release(e.Element);

    private static void Release(Element element)
    {
        Clear(element);

        if (element is View view)
        {
            foreach (BindableObject recognizer in view.GestureRecognizers.OfType<BindableObject>())
            {
                Clear(recognizer);
            }
        }

        foreach (Element child in ((IVisualTreeElement)element).GetVisualChildren().OfType<Element>())
        {
            Release(child);
        }
    }

    // Removing the binding keeps its last value, and ClearValue does not clear a bound value, so a set command is set to
    // null to end the subscription. Setting it only when set avoids adding empty entries to the property store.
    private static void Clear(BindableObject bindable)
    {
        foreach (BindableProperty commandProperty in CommandProperties)
        {
            bindable.RemoveBinding(commandProperty);
            if (bindable.IsSet(commandProperty))
            {
                bindable.SetValue(commandProperty, null);
            }
        }
    }
}
