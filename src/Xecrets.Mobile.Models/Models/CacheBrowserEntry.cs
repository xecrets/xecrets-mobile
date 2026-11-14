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

using System.Globalization;

using CommunityToolkit.Mvvm.Input;

namespace Xecrets.Mobile.Models.Models;

/// <summary>
/// A file or folder in the cache, as listed on the debug page that browses it.
/// </summary>
/// <param name="FilePath">The full path of the file or folder.</param>
/// <param name="DisplayName">The name to show.</param>
/// <param name="IsDirectory">True for a folder, false for a file.</param>
/// <param name="Size">The size of a file in bytes, not used for a folder.</param>
/// <param name="LastWriteTimeUtc">When the file or folder was last written.</param>
/// <param name="OpenCommand">Opens the entry.</param>
public sealed record CacheBrowserEntry(
    string FilePath,
    string DisplayName,
    bool IsDirectory,
    long Size,
    DateTime LastWriteTimeUtc,
    IAsyncRelayCommand<CacheBrowserEntry> OpenCommand)
{
    /// <summary>
    /// The local time of the last write, and the size of a file or that it is a folder.
    /// </summary>
    public string Details =>
        $"{LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} · " +
        (IsDirectory ? "Folder" : $"{Size:N0} bytes");
}
