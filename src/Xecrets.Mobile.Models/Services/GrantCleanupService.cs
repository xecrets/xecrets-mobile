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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Data;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Releases the grants the platform holds that are no longer referenced by My folders or the recent files of any
/// profile on the device. A folder grant is only held by My folders, and a grant to a file by itself only by the recent
/// files. Should the lists fail to load, nothing is referenced, and all grants are released.
/// </summary>
public sealed class GrantCleanupService(
    MobileDataStore dataStore,
    IFileAccess fileAccess,
    ICrashLogService crashLogService)
{
    public async Task RunAsync()
    {
        GrantReferences references;
        try
        {
            references = await dataStore.GetGrantReferencesAsync();
        }
        catch (Exception ex)
        {
            crashLogService.WriteCrashLog("My folders and recent files could not be loaded; releasing all grants.", ex);
            references = GrantReferences.None;
        }

        try
        {
            await fileAccess.ReleaseUnusedGrantsAsync(references);
        }
        catch (Exception ex)
        {
            // The next start or sign out tries again, and a grant kept a while longer does no harm.
            crashLogService.WriteCrashLog("Unused grants could not be released.", ex);
        }
    }
}
