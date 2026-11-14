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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.PageModels;

/// <summary>
/// The page model of the page about the app.
/// </summary>
public partial class AboutPageModel(
    IUserInterfaceService userInterfaceService,
    IBuildInformation buildInformation,
    IProfileService profileService,
    DebugMenu debugMenu) : PageModelBase(userInterfaceService), IBreadcrumbPageModel
{
    private const int VersionTapsToEnableDebugMenu = 7;

    private int _versionTapCount;

    /// <summary>
    /// How many extra passwords the profile signed in has, or "" if it has none or is not signed in.
    /// </summary>
    [ObservableProperty]
    public partial string ExtraPasswordsInfo { get; set; } = string.Empty;

    /// <summary>
    /// How many extra private keys the profile signed in has, and how many of them are decrypted, or "" if it has none
    /// or is not signed in.
    /// </summary>
    [ObservableProperty]
    public partial string PrivateKeysInfo { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Breadcrumb => BreadcrumbFormatter.Format(FlowOrigin.Home, MobileTexts.BreadcrumbAbout);

    /// <inheritdoc cref="IBuildInformation.ProductName"/>
    public string ProductName => buildInformation.ProductName;

    /// <inheritdoc cref="IBuildInformation.Version"/>
    public string Version => buildInformation.Version;

    /// <inheritdoc cref="IBuildInformation.Platform"/>
    public string Platform => buildInformation.Platform;

    /// <inheritdoc cref="IBuildInformation.Publisher"/>
    public string Publisher => buildInformation.Publisher;

    /// <inheritdoc cref="IBuildInformation.Copyright"/>
    public string Copyright => buildInformation.Copyright;

    /// <inheritdoc cref="IBuildInformation.License"/>
    public string License => buildInformation.License;

    /// <inheritdoc cref="IBuildInformation.SourceCodeUrl"/>
    public string SourceCodeUrl => buildInformation.SourceCodeUrl;

    /// <summary>
    /// The notice about the third-party material in the app.
    /// </summary>
    public string ThirdPartyNotice => MobileTexts.AboutPageThirdPartyNotice;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        _versionTapCount = 0;
        if (!profileService.IsAuthenticated)
        {
            return;
        }

        int passwordCount = profileService.GetExtraPasswords().Count;
        if (passwordCount > 0)
        {
            ExtraPasswordsInfo = MobileTexts.AboutDecryptionPasswords(passwordCount);
        }

        PrivateKeyCounts keys = await profileService.GetPrivateKeyCountsAsync();
        if (keys.Total > 0)
        {
            PrivateKeysInfo = MobileTexts.AboutPrivateKeys(keys.Decryptable, keys.Total);
        }
    }

    // Enough taps on the version, since the page appeared, ask to enable the debug menu in a release build. The texts
    // are literals, like the texts of the debug page.
    [RelayCommand]
    private async Task VersionTappedAsync()
    {
        if (debugMenu.IsEnabled || ++_versionTapCount < VersionTapsToEnableDebugMenu)
        {
            return;
        }

        _versionTapCount = 0;
        if (await UserInterfaceService.DisplayConfirmationAsync("Enable debug menu?", "Yes", "No"))
        {
            debugMenu.IsEnabled = true;
        }
    }

    [RelayCommand]
    private Task OpenBrowser(string url) => UserInterfaceService.OpenBrowserAsync(url);
}
