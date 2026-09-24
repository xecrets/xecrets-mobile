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
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.PageModels;

public partial class WelcomePageModel(
    IProfileService profileService,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService)
{
    private readonly IReadOnlyList<WelcomeLine> _welcomeLines = MobileTexts.WelcomeLines(userInterfaceService.IconMap);

    public string Heading => _welcomeLines[0].Text;

    public IReadOnlyList<WelcomeLine> Introduction =>
        [.. _welcomeLines.Skip(1).TakeWhile(line => line.Icon.Length == 0)];

    // From the first bullet on, so that the bullet section can be set apart from the introduction.
    public IReadOnlyList<WelcomeLine> Lines =>
        [.. _welcomeLines.Skip(1).SkipWhile(line => line.Icon.Length == 0)];

    [ObservableProperty]
    public partial bool IsDontShowAgainAvailable { get; private set; }

    [ObservableProperty]
    public partial bool DontShowAgain { get; set; }

    // As the Shell root before a profile exists, the page leads on to profile creation, so that backing out of that
    // returns here. Pushed on top of another page, it is initialized with a payload and returns there.
    private bool _isIntroduction = true;

    public void Initialize(bool isDontShowAgainAvailable)
    {
        _isIntroduction = false;
        IsDontShowAgainAvailable = isDontShowAgainAvailable;
    }

    [RelayCommand]
    private async Task Ok()
    {
        if (_isIntroduction)
        {
            await UserInterfaceService.NavigateToAsync(AppDestination.CreateProfile);
            return;
        }

        if (IsDontShowAgainAvailable && DontShowAgain)
        {
            await profileService.SetDontShowAgainAsync(Models.DontShowAgain.WelcomeInformation);
        }

        await UserInterfaceService.GoBackAsync();
    }
}
