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
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Models;

/// <summary>
/// A file on the list of recent files.
/// </summary>
/// <param name="File">The file, as listed.</param>
/// <param name="IsNameEncrypted">True if the name of the file is that of an encrypted file, false otherwise.</param>
/// <param name="Operation">The operation that put the file on the list.</param>
public sealed record RecentFileEntry(
    FileReference File,
    bool IsNameEncrypted,
    RecentFileOperation Operation)
{
    /// <summary>
    /// The id of the file.
    /// </summary>
    public string Id => File.Id;

    /// <summary>
    /// Where the file is stored, as far as it is known.
    /// </summary>
    public RootProvider Root => File.Root;

    /// <summary>
    /// The names of the folders leading to the file below <see cref="Root"/>, as far as they are known.
    /// </summary>
    public IReadOnlyList<string> FolderPath => File.FolderPath;

    /// <summary>
    /// The name of the file, as far as it is known.
    /// </summary>
    public string DisplayName => File.Name.Length > 0 ? File.Name : MobileTexts.RecentFileNameUnknown;

    /// <summary>
    /// Where the file is stored, as far as it is known. There is no root to show when the folder path starts at the
    /// top, and nothing at all when nothing is known of where the file is stored.
    /// </summary>
    public string DisplayLocation => string.Join(MobileTexts.BreadcrumbSeparator, [.. Root.ToPathSegments(), .. FolderPath]);

    /// <summary>
    /// True if anything is known of where the file is stored, false otherwise.
    /// </summary>
    public bool HasLocation => DisplayLocation.Length > 0;

    /// <summary>
    /// Where the file is stored and its name, as far as they are known.
    /// </summary>
    internal string DisplayPath => HasLocation
        ? DisplayLocation + MobileTexts.BreadcrumbSeparator + DisplayName
        : DisplayName;

    /// <summary>
    /// True if the file was encrypted or decrypted in place, false otherwise.
    /// </summary>
    public bool IsInPlace => Operation == RecentFileOperation.InPlace;

    /// <summary>
    /// True if the file was viewed or edited, false otherwise.
    /// </summary>
    public bool IsViewed => Operation is RecentFileOperation.View or RecentFileOperation.Edit;

    /// <summary>
    /// True if the file was neither encrypted or decrypted in place, nor viewed or edited, false otherwise.
    /// </summary>
    public bool IsOther => !IsInPlace && !IsViewed;
}
