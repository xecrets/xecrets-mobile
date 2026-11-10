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
using Android.Views;

using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;

using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;

using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
using Window = Android.Views.Window;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// A Shell renderer whose flyout renderer ignores drawer updates after its native view is gone and keeps the status bar
/// icons visible over the flyout, and whose toolbar shows the icons of the overflow menu items.
/// </summary>
internal sealed class AndroidShellRenderer : ShellRenderer
{
    protected override IShellFlyoutRenderer CreateShellFlyoutRenderer() =>
        new GuardedShellFlyoutRenderer(this, AndroidContext);

    protected override IShellToolbarTracker CreateTrackerForToolbar(Toolbar toolbar) =>
        new OverflowIconsShellToolbarTracker(this, toolbar, ((IShellContext)this).CurrentDrawerLayout);

    private sealed class OverflowIconsShellToolbarTracker(
        IShellContext shellContext,
        Toolbar toolbar,
        DrawerLayout drawerLayout) : ShellToolbarTracker(shellContext, toolbar, drawerLayout)
    {
        protected override void UpdateToolbarItems(Toolbar toolbar, Page page)
        {
            base.UpdateToolbarItems(toolbar, page);
            toolbar.Menu!.ShowIcons();
        }

        // The base tints all icons with the Shell foreground color, which is meant for the toolbar itself and not for
        // the overflow menu, so the overflow menu icons keep their own color.
        protected override void UpdateMenuItemIcon(Context context, IMenuItem menuItem, ToolbarItem toolBarItem)
        {
            if (toolBarItem.Order != ToolbarItemOrder.Secondary)
            {
                base.UpdateMenuItemIcon(context, menuItem, toolBarItem);
                return;
            }
            menuItem.SetIcon(toolBarItem.IconImageSource, MauiContext);
        }
    }

    private sealed class GuardedShellFlyoutRenderer : ShellFlyoutRenderer
    {
        // The flyout reaches under the status bar, and its light background in the light theme hides the light status
        // bar icons that suit the header. The icons are dark while the flyout is mostly open, and the appearance from
        // before is restored when it closes.
        private const float DarkStatusBarIconsSlideOffset = 0.5f;

        private readonly Context _context;

        private float _slideOffset;

        private bool? _lightStatusBarsBeforeFlyout;

        public GuardedShellFlyoutRenderer(IShellContext shellContext, Context context)
            : base(shellContext, context)
        {
            _context = context;
            DrawerSlide += OnDrawerSlideStatusBar;
            DrawerOpened += OnDrawerOpenedStatusBar;
            DrawerClosed += OnDrawerClosedStatusBar;
            Application.Current!.RequestedThemeChanged += OnRequestedThemeChanged;
        }

        // MAUI can keep notifying a stale Shell's flyout renderer after its native view is gone, for example on a
        // theme change after the activity was recreated, and it then throws ObjectDisposedException.
        protected override void UpdateDrawerLockMode(FlyoutBehavior behavior)
        {
            if (!PeerReference.IsValid)
            {
                return;
            }
            base.UpdateDrawerLockMode(behavior);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DrawerSlide -= OnDrawerSlideStatusBar;
                DrawerOpened -= OnDrawerOpenedStatusBar;
                DrawerClosed -= OnDrawerClosedStatusBar;
                Application.Current!.RequestedThemeChanged -= OnRequestedThemeChanged;
            }
            base.Dispose(disposing);
        }

        private void OnDrawerSlideStatusBar(object? sender, DrawerSlideEventArgs e)
        {
            _slideOffset = e.SlideOffset;
            UpdateStatusBarIcons();
        }

        private void OnDrawerOpenedStatusBar(object? sender, DrawerOpenedEventArgs e)
        {
            _slideOffset = 1;
            UpdateStatusBarIcons();
        }

        private void OnDrawerClosedStatusBar(object? sender, DrawerClosedEventArgs e)
        {
            _slideOffset = 0;
            UpdateStatusBarIcons();
        }

        private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => UpdateStatusBarIcons();

        private void UpdateStatusBarIcons()
        {
            Window window = _context.GetActivity()!.Window!;
            WindowInsetsControllerCompat controller = WindowCompat.GetInsetsController(window, window.DecorView)!;
            bool darkIcons = _slideOffset >= DarkStatusBarIconsSlideOffset
                && Application.Current!.RequestedTheme == AppTheme.Light;
            if (darkIcons && _lightStatusBarsBeforeFlyout is null)
            {
                _lightStatusBarsBeforeFlyout = controller.AppearanceLightStatusBars;
                controller.AppearanceLightStatusBars = true;
            }
            else if (!darkIcons && _lightStatusBarsBeforeFlyout is bool lightStatusBars)
            {
                controller.AppearanceLightStatusBars = lightStatusBars;
                _lightStatusBarsBeforeFlyout = null;
            }
        }
    }
}
