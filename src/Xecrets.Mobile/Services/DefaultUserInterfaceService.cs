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

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

using Fonts;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile.Services;

internal class DefaultUserInterfaceService(IBuildInformation buildInformation, EzAppShell ezAppShell) : IUserInterfaceService
{
    // The navigations through this service that are not done yet.
    private int _navigationCount;

    protected Shell AppShell => ezAppShell.Shell;

    // The shell shown in the app's only window, or null while there is none, as before the shell is created or after
    // the window is closed. Incoming files are only processed by a shell that is shown.
    private static Shell? WindowShell => Application.Current?.Windows.Count == 1
        ? Application.Current.Windows[0].Page as Shell
        : null;

    private static string? CurrentRoute => WindowShell?.CurrentItem?.CurrentItem?.CurrentItem is { } shellContent
        ? Routing.GetRoute(shellContent)
        : null;

    public bool IsShellAvailable => WindowShell is not null;

    public string CurrentLocation => AppShell.CurrentState.Location.OriginalString;

    public bool IsNavigating => _navigationCount > 0;

    public IReadOnlyDictionary<string, string> IconMap { get; } = new Dictionary<string, string>
    {
        ["(c)"] = FluentUI.person_add_24_regular,
        ["(e)"] = FluentUI.lock_closed_24_regular,
        ["(f)"] = FluentUI.folder_24_regular,
        ["(v)"] = FluentUI.lock_open_24_regular,
        ["(s)"] = FluentUI.share_24_regular,
        ["(p)"] = FluentUI.password_24_regular,
        ["(w)"] = FluentUI.delete_24_regular,
        ["(o)"] = FluentUI.cloud_off_24_regular,
    };

    public bool CanProcessIncomingFiles =>
        CurrentRoute is "home" or "login" or "introduction";

    public bool CanReceiveIncomingFiles =>
        WindowShell is null ||
        CurrentRoute is "startup" or "home" or "login" or "introduction";

    public Task InvokeOnMainThreadAsync(Func<Task> action) => MainThread.InvokeOnMainThreadAsync(action);

    public Task DisplayMessageAsync(string message) =>
        AppShell.DisplayAlertAsync(MobileTexts.DisplayNameProgram, message, MobileTexts.ButtonOk);

    public Task<bool> DisplayConfirmationAsync(string message, string accept, string cancel) =>
        AppShell.DisplayAlertAsync(
            MobileTexts.DisplayNameProgram,
            message,
            accept,
            cancel);

    public async Task<SaveChangesChoice> DisplaySaveChangesAsync(string message)
    {
        string? choice = await AppShell.DisplayActionSheetAsync(
            message,
            MobileTexts.LabelCancel,
            MobileTexts.ButtonDiscard,
            MobileTexts.ButtonSave);

        // Dismissing the action sheet gives the cancel text on some platforms and null on others.
        return choice == MobileTexts.ButtonSave ? SaveChangesChoice.Save
            : choice == MobileTexts.ButtonDiscard ? SaveChangesChoice.Discard
            : SaveChangesChoice.Cancel;
    }

    public Task<string?> DisplayPromptAsync(string message, string initialValue) =>
        AppShell.DisplayPromptAsync(
            MobileTexts.DisplayNameProgram,
            message,
            MobileTexts.ButtonOk,
            MobileTexts.LabelCancel,
            initialValue: initialValue);

    public virtual Task DisplayTransientMessageAsync(string message) =>
        Toast.Make(message, ToastDuration.Long).Show();

    public Task NavigateToAsync(AppDestination destination, object? parameter)
    {
        string route = GetRoute(destination);
        if (parameter is not null)
        {
            return NavigateToRouteAsync(route, parameter);
        }

        Page? currentPage = AppShell.CurrentPage;
        if (currentPage is not null && Routing.GetRoute(currentPage) == route)
        {
            return Task.CompletedTask;
        }

        return NavigateToRouteAsync(route);
    }

    public Task NavigateReplacingAsync(AppDestination destination, object? parameter)
    {
        string route = $"../{GetRoute(destination)}";
        return parameter is null
            ? NavigateToRouteAsync(route)
            : NavigateToRouteAsync(route, parameter);
    }

    private static string GetRoute(AppDestination destination)
    {
        return destination switch
        {
            AppDestination.Crash => "//crash",
            AppDestination.Login => "//login",
            AppDestination.Introduction => "//introduction",
            AppDestination.CreateProfile => "create-profile",
            AppDestination.Home => "//home",
            AppDestination.Preview => "preview",
            AppDestination.View => "view",
            AppDestination.Edit => "edit",
            AppDestination.EncryptResult => "encrypt-result",
            AppDestination.EncryptToShare => "encrypt-to-share",
            AppDestination.WorkFolders => "work-folders",
            AppDestination.EnterPassword => "enter-password",
            AppDestination.About => "about",
            AppDestination.ThirdPartyLicenses => "third-party-licenses",
            AppDestination.Debug => "debug",
            AppDestination.CacheBrowser => "cache-browser",
            AppDestination.SuggestPassword => "suggest-password",
            AppDestination.RecentFiles => "recent-files",
            AppDestination.Welcome => "welcome",
            AppDestination.VerifyPassword => "verify-password",
            AppDestination.ChangeProfile => "change-profile",
            AppDestination.ResetPassword => "reset-password",
            AppDestination.Startup => "//startup",
            _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, @"Unknown app destination."),
        };
    }

    public Task GoBackAsync(object? parameter) => parameter is null
        ? NavigateToRouteAsync("..")
        : CountNavigationAsync(() => AppShell.GoToAsync(
            "..",
            new ShellNavigationQueryParameters
            {
                { nameof(NavigationParameter.Payload), parameter },
            }));

    public Task OpenBrowserAsync(string url) => Launcher.Default.OpenAsync(
        url.ToSite(buildInformation.IsDebug || buildInformation.IsBeta));

    public virtual async Task SetSensitiveClipboardTextAsync(string text, bool confirm)
    {
        await Clipboard.Default.SetTextAsync(text);
        if (confirm)
        {
            await DisplayTransientMessageAsync(MobileTexts.DialogTextCopiedToClipboard);
        }
    }

    private Task NavigateToRouteAsync(string route) => CountNavigationAsync(() => AppShell.GoToAsync(route));

    private Task NavigateToRouteAsync(string route, object parameter) =>
        CountNavigationAsync(() => AppShell.GoToAsync(
            route,
            new Dictionary<string, object>
            {
                { nameof(NavigationParameter.Payload), parameter },
            }));

    // Shell tells that a navigation is proposed before it is done, so it is counted until it is.
    private async Task CountNavigationAsync(Func<Task> navigateAsync)
    {
        _navigationCount++;
        try
        {
            await navigateAsync();
        }
        finally
        {
            _navigationCount--;
        }
    }
}
