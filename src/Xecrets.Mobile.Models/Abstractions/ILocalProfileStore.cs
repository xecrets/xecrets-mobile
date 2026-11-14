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

using Xecrets.Common.Models;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// Keeps the stored data of each local profile: what goes with the profile, and what only applies to it on this
/// device.
/// </summary>
internal interface ILocalProfileStore
{
    /// <summary>
    /// Reads the stored data of a local profile.
    /// </summary>
    /// <param name="userId">The id of an existing local profile.</param>
    /// <returns>The stored data of the profile.</returns>
    Task<LocalProfileData> ReadUserAsync(UserId userId);

    /// <summary>
    /// Updates the stored data of a local profile and persists it.
    /// </summary>
    /// <param name="userId">The id of an existing local profile.</param>
    /// <param name="update">Changes the data given to it.</param>
    Task UpdateUserAsync(UserId userId, Action<LocalProfileData> update);

    /// <summary>
    /// Reads the settings of a local profile that only apply to it on this device.
    /// </summary>
    /// <param name="userId">The id of the local profile.</param>
    /// <returns>The settings, or new settings if there are none.</returns>
    Task<ProfileDeviceSettings> ReadProfileDeviceSettingsAsync(UserId userId);

    /// <summary>
    /// Updates the settings of a local profile that only apply to it on this device, and persists them. Settings are
    /// created for the profile if there are none.
    /// </summary>
    /// <param name="userId">The id of the local profile.</param>
    /// <param name="update">Changes the settings given to it.</param>
    Task UpdateProfileDeviceSettingsAsync(UserId userId, Action<ProfileDeviceSettings> update);
}
