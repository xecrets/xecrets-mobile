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

using JetBrains.Annotations;

using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// The folders and files of the app on the device, and the handing of files to other apps and viewers of the platform.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// The identifier of the platform, such as "android" or "ios".
    /// </summary>
    [PublicAPI] string PlatformId { get; }

    /// <summary>
    /// The full path of the folder of the app data.
    /// </summary>
    string AppDataDirectory { get; }

    /// <summary>
    /// The full path of the folder of the temporary files of the app, which the platform may clear at any time.
    /// </summary>
    string CacheDirectory { get; }

    /// <summary>
    /// The name of <see cref="DeviceDirectory"/> in <see cref="AppDataDirectory"/>.
    /// </summary>
    const string DeviceDirectoryName = "Device";

    /// <summary>
    /// The full path of the folder of the data that only applies to the installation on this device, which is kept out
    /// of backups and transfers.
    /// </summary>
    string DeviceDirectory => Path.Combine(AppDataDirectory, DeviceDirectoryName);

    /// <summary>
    /// The full path of the file of the app settings and the profiles.
    /// </summary>
    string DataFilePath => Path.Combine(AppDataDirectory, "xecrets-data.json");

    /// <summary>
    /// The full path of the file of the device-specific settings, in <see cref="DeviceDirectory"/>.
    /// </summary>
    string DeviceSettingsFilePath => Path.Combine(DeviceDirectory, "xecrets-device.json");

    /// <summary>
    /// The full path of the file of the save in progress, in <see cref="DeviceDirectory"/>.
    /// </summary>
    string SaveInProgressFilePath => Path.Combine(DeviceDirectory, "xecrets-save-in-progress.json");

    /// <summary>
    /// Lets the user open a file in another app.
    /// </summary>
    /// <param name="filePath">The full path of the file.</param>
    /// <param name="displayName">The name of the file to show.</param>
    /// <returns>True if the file was opened, and is then still in use, false if it was not opened.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    Task<bool> OpenInAsync(string filePath, string displayName);

    /// <summary>
    /// Lets the user send a file to another app.
    /// </summary>
    /// <param name="filePath">The full path of the file.</param>
    /// <param name="displayName">The name of the file to show.</param>
    /// <param name="contentType">The MIME type of the file, or "" to tell it from the name.</param>
    /// <returns>True if the file is still in use, false if it is no longer used.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    Task<bool> SendToAsync(string filePath, string displayName, string contentType);

    /// <summary>
    /// Tells if a viewer of the platform can show a decrypted file.
    /// </summary>
    /// <param name="file">The decrypted file.</param>
    /// <returns>True if a viewer of the platform can show the file, false otherwise.</returns>
    Task<bool> CanViewFileAsync(DecryptedFileInfo file);

    /// <summary>
    /// Shows a decrypted file in a viewer of the platform.
    /// </summary>
    /// <param name="file">The decrypted file, for which <see cref="CanViewFileAsync"/> returns true.</param>
    /// <returns>True if the file is still in use, false if it is no longer used.</returns>
    Task<bool> ViewFileAsync(DecryptedFileInfo file);

    /// <summary>
    /// Tells if a reference to an incoming file refers to a file that the app itself handed to another app or viewer,
    /// rather than to a file from another app.
    /// </summary>
    /// <param name="reference">The platform reference to the incoming file: a content uri on Android and a path on
    /// other platforms.</param>
    /// <returns>True if the file is one the app handed off, false otherwise.</returns>
    bool IsSelfHandoffReference(string reference);

    /// <summary>
    /// Keeps a file out of the backups the platform makes of the app data, unless it is already kept out otherwise.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    void ExcludeFromBackup(string path)
    {
    }
}
