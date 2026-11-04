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

using Xecrets.Core.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Data;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Deletes all local data, the profile, the application settings, the cache and the crash logs, and starts the app
/// over. Nothing is written until a new profile is created, so the app's folders are left empty.
/// </summary>
public sealed class LocalProfileDeletionService(
    IPreviewService previewService,
    DecryptionPasswordRequestState passwordRequestState,
    ITransientFileService transientFileService,
    GrantCleanupService grantCleanupService,
    MobileDataStore dataStore,
    IDeviceSettings deviceSettings,
    IFileService fileService,
    ICrashLogService crashLogService,
    IProfileService profileService,
    IUserInterfaceService userInterfaceService)
{
    public async Task DeleteAsync()
    {
        previewService.Current.Clear();
        passwordRequestState.Clear();
        await transientFileService.WipeTrackedFilesAsync();
        profileService.SignOut();
        await dataStore.ResetStoreAsync();

        // The settings file is deleted with the store, but the values are also held in memory.
        ((IClear)deviceSettings).Clear(persist: false);

        // After the reset, nothing references the grants, so all are released.
        await grantCleanupService.RunAsync();

        // The reset and the release leave the folder for the device-specific data empty, and it's created again as
        // needed. The wipe already removed the cache folder.
        DeleteDirectory(fileService.DeviceDirectory);

        // Last, since the steps before may log.
        crashLogService.DeleteAll();

        // From the very beginning, as when first installed. Without a profile, the startup page goes on to the
        // introduction, and the default theme is applied, since the settings are gone.
        await userInterfaceService.NavigateToAsync(AppDestination.Startup);
    }

    private void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort, as for the wiping.
            crashLogService.WriteSwallowedException(ex);
        }
    }
}
