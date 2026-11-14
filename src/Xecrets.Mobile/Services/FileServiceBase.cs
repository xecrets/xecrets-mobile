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
using System.IO;
using System.Threading.Tasks;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Services;

/// <summary>
/// The parts of <see cref="IFileService"/> that are shared by the platforms, using MAUI where it can.
/// </summary>
internal abstract class FileServiceBase : IFileService
{
    /// <inheritdoc/>
    public abstract string PlatformId { get; }

    /// <inheritdoc/>
    public string AppDataDirectory => FileSystem.AppDataDirectory;

    /// <inheritdoc/>
    public string CacheDirectory => Path.Combine(FileSystem.CacheDirectory, "XecretsEz");

    /// <inheritdoc/>
    /// <remarks>
    /// Opens the file with the MAUI <see cref="Launcher"/>.
    /// </remarks>
    public virtual async Task<bool> OpenInAsync(string filePath, string displayName)
    {
        EnsureReadableFile(filePath);

        return await Launcher.Default.OpenAsync(new OpenFileRequest(displayName, new ReadOnlyFile(filePath)));
    }

    /// <inheritdoc/>
    public abstract Task<bool> SendToAsync(string filePath, string displayName, string contentType);

    /// <summary>
    /// Lets the user send a file to another app with the MAUI <see cref="Share"/>.
    /// </summary>
    /// <param name="filePath">The full path of the file.</param>
    /// <param name="displayName">The title of the share.</param>
    /// <param name="contentType">The MIME type of the file, or "" to tell it from the name.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    protected static async Task ShareAsync(string filePath, string displayName, string contentType)
    {
        EnsureReadableFile(filePath);

        ShareFile shareFile = string.IsNullOrWhiteSpace(contentType)
            ? new ShareFile(filePath)
            : new ShareFile(filePath, contentType);

        await Share.Default.RequestAsync(
            new ShareFileRequest
            {
                Title = displayName,
                File = shareFile,
            });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// There is no viewer, unless a platform overrides this.
    /// </remarks>
    public virtual Task<bool> CanViewFileAsync(DecryptedFileInfo file) => Task.FromResult(false);

    /// <inheritdoc/>
    /// <remarks>
    /// The file is opened as by <see cref="OpenInAsync"/>, unless a platform overrides this.
    /// </remarks>
    public virtual Task<bool> ViewFileAsync(DecryptedFileInfo file) => OpenInAsync(file.FilePath, file.OriginalFileName);

    /// <inheritdoc/>
    /// <remarks>
    /// For platforms where the reference is a path. The app hands off its own files in the XecretsHandoff folder of
    /// <see cref="CacheDirectory"/>, so a path there is always a file the app handed off.
    /// </remarks>
    public virtual bool IsSelfHandoffReference(string reference) =>
        reference.StartsWith(Path.Combine(CacheDirectory, "XecretsHandoff"), StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    /// <remarks>
    /// Does nothing, for platforms that make no backups of the app data, such as Windows, or where the exclusions are
    /// declared in the manifest, such as Android.
    /// </remarks>
    public virtual void ExcludeFromBackup(string path)
    {
    }

    /// <summary>
    /// Checks that a file exists.
    /// </summary>
    /// <param name="filePath">The full path of the file.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    protected static void EnsureReadableFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(@"The file was not found.", filePath);
        }
    }
}
