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

using System.Threading.Tasks;
using System;

using CommunityToolkit.Mvvm.Input;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

using Xecrets.Common.Abstractions;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile;

public partial class AppShell
{
    private readonly IUserInterfaceService _userInterfaceService;
    private readonly SessionExitService _sessionExitService;
    private readonly IXecretsDataStore _dataStore;
    private readonly StartupPageModel _startupPageModel;

    public AppShell(
        IUserInterfaceService userInterfaceService,
        SessionExitService sessionExitService,
        IBuildInformation buildInformation,
        IXecretsDataStore dataStore,
        StartupPageModel startupPageModel)
    {
        _userInterfaceService = userInterfaceService;
        _sessionExitService = sessionExitService;
        _dataStore = dataStore;
        _startupPageModel = startupPageModel;
        InitializeComponent();
        SetFlyoutItemIsVisible(DebugMenuItem, buildInformation.IsDebug || buildInformation.IsBeta);
        UpdateThemeButtons(ThemePreference.System);
    }

    [RelayCommand]
    private async Task NavigatedAsync(ShellNavigatedEventArgs args)
    {
        if (args.Current.Location.OriginalString != "//startup")
        {
            return;
        }

        ApplyTheme(await GetThemeAsync());
        await _startupPageModel.InitializeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task SetThemeAsync(ThemePreference preference)
    {
        ApplyTheme(preference);
        await using IEditScope<ApplicationSettings> settings =
            (await _dataStore.OpenApplicationSettingsAsync()).BeginEdit();
        settings.Value.Theme = preference.ToString();
    }

    [RelayCommand]
    private async Task AboutAsync()
    {
        FlyoutIsPresented = false;
        await _userInterfaceService.NavigateToAsync(AppDestination.About);
    }

    [RelayCommand]
    private async Task DebugAsync()
    {
        FlyoutIsPresented = false;
        await _userInterfaceService.NavigateToAsync(AppDestination.Debug);
    }

    [RelayCommand]
    private async Task ExitAsync()
    {
        FlyoutIsPresented = false;
        await _sessionExitService.ExitAsync();
    }

    private void ApplyTheme(ThemePreference preference)
    {
        Application.Current!.UserAppTheme = preference switch
        {
            ThemePreference.Light => AppTheme.Light,
            ThemePreference.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };
        UpdateThemeButtons(preference);
    }

    private async Task<ThemePreference> GetThemeAsync()
    {
        await using IEditScope<ApplicationSettings> settings =
            (await _dataStore.OpenApplicationSettingsAsync()).BeginEdit();
        return Enum.TryParse(settings.Value.Theme, out ThemePreference preference)
            ? preference
            : ThemePreference.System;
    }

    private void UpdateThemeButtons(ThemePreference preference)
    {
        LightThemeButton.Opacity = preference == ThemePreference.Light ? 1 : 0.5;
        SystemThemeButton.Opacity = preference == ThemePreference.System ? 1 : 0.5;
        DarkThemeButton.Opacity = preference == ThemePreference.Dark ? 1 : 0.5;
    }
}
