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

using System.Security.Cryptography;
using System.Text;

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Data;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Keeps the device-specific settings to the installation on this device. They are kept in a directory that is kept
/// out of backups and transfers, so a restore from a backup or a transfer to a new device does not bring them along.
/// Should they come along anyway, or have been written for another <see cref="DeviceSettingsVersion"/>, they are
/// replaced with new ones. To tell, they hold a salted hash of the device identifier.
/// </summary>
public sealed class InstallationService(MobileDataStore dataStore, IFileService fileService, IDeviceIdentity deviceIdentity)
{
    /// <summary>
    /// The version of the device-specific settings. Increasing it resets them once on every device.
    /// </summary>
    internal const int DeviceSettingsVersion = 1;

    public async Task EnsureAsync()
    {
        Directory.CreateDirectory(fileService.DeviceDirectory);
        fileService.ExcludeFromBackup(fileService.DeviceDirectory);

        DeviceSettings settings = await dataStore.ReadDeviceSettingsAsync();
        string deviceId = deviceIdentity.DeviceId;
        if (settings.Version != DeviceSettingsVersion || IsOtherDevice(settings, deviceId))
        {
            await dataStore.ReplaceDeviceSettingsAsync(NewSettings(deviceId));
        }
    }

    // A device identifier that is not known, now or when the settings were written, tells nothing.
    private static bool IsOtherDevice(DeviceSettings settings, string deviceId) =>
        deviceId.Length > 0 && settings.DeviceId.Length > 0 &&
        HashDeviceId(settings.DeviceIdSalt, deviceId) != settings.DeviceId;

    private static DeviceSettings NewSettings(string deviceId)
    {
        string salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        return new DeviceSettings
        {
            Version = DeviceSettingsVersion,
            DeviceIdSalt = salt,
            DeviceId = deviceId.Length > 0 ? HashDeviceId(salt, deviceId) : string.Empty,
        };
    }

    private static string HashDeviceId(string salt, string deviceId) =>
        Convert.ToHexString(SHA256.HashData([.. Convert.FromHexString(salt), .. Encoding.UTF8.GetBytes(deviceId)]));
}
