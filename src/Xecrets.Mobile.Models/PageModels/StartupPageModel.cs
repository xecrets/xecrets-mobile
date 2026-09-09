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

using System.Diagnostics;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.PageModels;

public partial class StartupPageModel(
    IProfileService profileService,
    IIncomingFileService incomingFileService,
    IBuildInformation buildInformation,
    ICrashLogService crashLogService,
    IUserInterfaceService userInterfaceService)
    : ObservableObject
{
    private readonly TimeSpan _minimumStartupDuration = buildInformation.IsDebug
        ? TimeSpan.FromSeconds(2)
        : TimeSpan.FromSeconds(1);

    private bool _isNavigating;

    [RelayCommand]
    private async Task Initialize()
    {
        if (_isNavigating)
        {
            return;
        }

        long startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            _isNavigating = true;

            if (crashLogService.HasPendingCrashLog)
            {
                await userInterfaceService.NavigateToAsync(AppDestination.Crash);
                return;
            }

            await NavigateToNormalStartAsync(startTimestamp);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    [RelayCommand]
    private async Task ContinueAfterCrash()
    {
        if (_isNavigating)
        {
            return;
        }

        try
        {
            _isNavigating = true;
            await NavigateToNormalStartAsync(null);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private async Task NavigateToNormalStartAsync(long? startTimestamp)
    {
        AppDestination destination = profileService.IsAuthenticated
            ? AppDestination.Home
            : await profileService.HasProfileAsync() ? AppDestination.Login : AppDestination.CreateProfile;

        if (startTimestamp.HasValue)
        {
            await DelayUntilMinimumStartupDurationAsync(startTimestamp.Value);
        }

        await userInterfaceService.NavigateToAsync(destination);

        await incomingFileService.ProcessPendingAsync();
    }

    private async Task DelayUntilMinimumStartupDurationAsync(long startTimestamp)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        TimeSpan remaining = _minimumStartupDuration - elapsed;

        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
    }
}
