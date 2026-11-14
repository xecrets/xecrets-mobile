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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;

using WindowsPage = Microsoft.Maui.Controls.PlatformConfiguration.WindowsSpecific.Page;

using System.Runtime.Versioning;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Controls;
using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Platforms.Windows;

/// <summary>
/// The configuration of the MAUI app for Windows.
/// </summary>
[SupportedOSPlatform("windows10.0.19041")]
internal static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers the Windows implementations of the platform services, the Windows handlers of the controls, and the
    /// mapping that gives the items of the overflow menu their icons.
    /// </summary>
    /// <param name="builder">The builder of the app.</param>
    /// <returns>The builder.</returns>
    public static MauiAppBuilder ConfigurePlatform(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<IFileService, WindowsFileService>();
        builder.Services.AddSingleton<IFileAccess, WindowsFileAccess>();
        builder.Services.AddSingleton<IDeviceIdentity, WindowsDeviceIdentity>();
        builder.Services.AddSingleton<IWorkFolderFileLauncher, WindowsWorkFolderFileLauncher>();
        builder.Services.AddSingleton<IUserInterfaceService, WindowsUserInterfaceService>();
        builder.Services.AddSingleton<IPlatformServices, WindowsServices>();
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<FileStateSelector, WindowsFileStateSelectorHandler>();
            handlers.AddHandler<OverflowMenuButton, WindowsOverflowMenuButtonHandler>();
        });
        MapToolbarDynamicOverflow();
        return builder;
    }

    /// <summary>
    /// The items of the overflow menu in the page header only get their icons when the command bar has dynamic
    /// overflow when the menu is made. MAUI maps the dynamic overflow of the toolbar only under the name of the page
    /// property, so the value Shell sets once a page is shown never reaches the command bar, which keeps the value from
    /// before there was a page. Shell sets it after the items, so the menu is made again.
    /// </summary>
    private static void MapToolbarDynamicOverflow() =>
        ToolbarHandler.Mapper.AppendToMapping(nameof(Toolbar.DynamicOverflowEnabled), (handler, _) =>
        {
            handler.UpdateValue(WindowsPage.ToolbarDynamicOverflowEnabledProperty.PropertyName);
            handler.UpdateValue(nameof(Toolbar.ToolbarItems));
        });
}
