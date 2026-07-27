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
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

using AppTexts = Xecrets.Texts.Texts;

namespace Xecrets.Mobile.Services;

public sealed class UserInterfaceService : IUserInterfaceService
{
    public bool IsShellAvailable => Shell.Current is not null;

    public bool CanProcessIncomingFiles =>
        Shell.Current?.CurrentPage is { } currentPage &&
        Routing.GetRoute(currentPage) is not "startup" and not "crash";

    public Task InvokeOnMainThreadAsync(Func<Task> action) => MainThread.InvokeOnMainThreadAsync(action);

    public Task DisplayMessageAsync(string message) =>
        Shell.Current!.DisplayAlertAsync(AppTexts.DisplayNameProgram, message, AppTexts.ButtonOk);

    public Task NavigateToAsync(AppDestination destination)
    {
        string route = GetRoute(destination);
        Page? currentPage = Shell.Current!.CurrentPage;
        if (currentPage is not null && Routing.GetRoute(currentPage) == route)
        {
            return Task.CompletedTask;
        }

        return NavigateToRouteAsync(route);
    }

    public Task NavigateToAsync(AppDestination destination, object parameter) =>
        NavigateToRouteAsync(
            GetRoute(destination),
            new Dictionary<string, object>
            {
                { nameof(NavigationParameter.Payload), parameter },
            });

    private static string GetRoute(AppDestination destination)
    {
        return destination switch
        {
            AppDestination.Crash => "//crash",
            AppDestination.Login => "//login",
            AppDestination.CreateProfile => "//create-profile",
            AppDestination.Home => "//home",
            AppDestination.Preview => "preview",
            AppDestination.View => "view",
            AppDestination.Edit => "edit",
            AppDestination.EncryptResult => "encrypt-result",
            AppDestination.EncryptToShare => "encrypt-to-share",
            AppDestination.EnterPassword => "enter-password",
            AppDestination.About => "about",
            AppDestination.ThirdPartyLicenses => "third-party-licenses",
            AppDestination.Debug => "debug",
            _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, @"Unknown app destination."),
        };
    }

    public Task GoBackAsync() => NavigateToRouteAsync("..");

    public Task OpenBrowserAsync(string url) => Launcher.Default.OpenAsync(url);

    private static Task NavigateToRouteAsync(string route) => Shell.Current!.GoToAsync(route);

    private static Task NavigateToRouteAsync(
        string route,
        IDictionary<string, object> parameters) =>
        Shell.Current!.GoToAsync(route, parameters);
}
