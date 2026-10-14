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
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Foundation;

using Microsoft.Maui.Devices;

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// Tells where a file is stored from its url. On iOS, the File Provider that stores it is told by its bookmark, and the
/// folders leading to it by where the provider keeps its files. On a Mac, a path has no provider and is told in full,
/// except iCloud Drive. Other locations are told as <see cref="RootProvider.Other"/> with their full path, until their
/// paths are verified and added here.
/// </summary>
internal static partial class AppleFileLocation
{
    // The folder that iCloud Drive is stored in, which the user sees as "iCloud Drive".
    private const string _iCloudDriveFolderName = "com~apple~CloudDocs";

    // Where a File Provider of the original kind keeps its files, such as On My iPhone and Google Drive do, in
    // /var/mobile/Containers/Shared/AppGroup/<uuid>/File Provider Storage/.
    private const string _fileProviderStorageFolderName = "File Provider Storage";

    // Where a replicated File Provider keeps each of its domains, such as Dropbox does, in
    // /var/mobile/Library/CloudStorage/<uuid>/ on iOS, and in ~/Library/CloudStorage/<name>/ on a Mac.
    private const string _cloudStorageFolderName = "CloudStorage";

    // The provider of On My iPhone, which keeps the folders as the user sees them.
    private const string _localStorageProvider = "com.apple.FileProvider.LocalStorage";

    private static readonly Dictionary<string, RootProvider> _providers = new()
    {
        ["com.apple.CloudDocs.iCloudDriveFileProvider"] = RootProvider.ICloudDrive,
        ["com.google.Drive.FileProviderExtension"] = RootProvider.GoogleDrive,
        ["com.getdropbox.Dropbox.FileProvider"] = RootProvider.Dropbox,
        ["com.microsoft.skydrive.onedrivefileprovider"] = RootProvider.OneDrive,
    };

    /// <summary>
    /// The file is accessed while being located, since the url a picker gave only gives access to the file while
    /// accessing it. The provider is only asked when allowed, since asking it about a file just saved may make it lose
    /// track of the file: OneDrive then shows a copy that stays waiting to be uploaded.
    /// </summary>
    public static FileReference Locate(NSUrl fileUrl, bool isProviderAsked = true) =>
        LocateItem(fileUrl, isProviderAsked).File;

    /// <summary>
    /// Locates a folder, and tells if it is the top of where it is stored, such as iCloud Drive itself or a Dropbox
    /// account. Such a folder may have no name of its own to show: a Dropbox account is a folder named by an id.
    /// </summary>
    public static (FileReference Folder, bool IsTop) LocateFolder(NSUrl folderUrl) => LocateItem(folderUrl, true);

    private static (FileReference File, bool IsTop) LocateItem(NSUrl fileUrl, bool isProviderAsked)
    {
        bool isAccessing = fileUrl.StartAccessingSecurityScopedResource();
        try
        {
            (RootProvider root, IReadOnlyList<string> folderPath, bool isTop) =
                GetRootAndFolderPath(fileUrl, isProviderAsked);
            FileReference file = new(fileUrl.AbsoluteString!, fileUrl.LastPathComponent!)
            {
                Root = root,
                FolderPath = folderPath,
            };
            return (file, isTop);
        }
        finally
        {
            if (isAccessing)
            {
                fileUrl.StopAccessingSecurityScopedResource();
            }
        }
    }

    /// <summary>
    /// The folders are those below where the storage starts, which the file or folder may itself be.
    /// </summary>
    private static (RootProvider Root, IReadOnlyList<string> FolderPath, bool IsTop) GetRootAndFolderPath(
        NSUrl fileUrl,
        bool isProviderAsked)
    {
        string[] segments = AppleFileGrants.NormalizePath(fileUrl, fileUrl.Path!).Split('/', StringSplitOptions.RemoveEmptyEntries);
        int last = segments.Length - 1;

        int index = Array.IndexOf(segments, _iCloudDriveFolderName);
        if (index >= 0)
        {
            return (RootProvider.ICloudDrive, FoldersBelow(segments, index), index == last);
        }

        if (!OperatingSystem.IsMacCatalyst())
        {
            // The folders of other providers than On My iPhone are named by the ids of the provider, and not told.
            index = Array.IndexOf(segments, _fileProviderStorageFolderName);
            if (index >= 0)
            {
                string? provider = isProviderAsked ? GetProvider(fileUrl) : null;
                return provider == _localStorageProvider
                    ? (GetDeviceRoot(), FoldersBelow(segments, index), index == last)
                    : (GetProviderRoot(provider), [], index == last);
            }
        }

        // Each domain, such as a Dropbox account, is a folder of its own, named by an id on iOS, and by the provider
        // and account on a Mac, in ~/Library/CloudStorage.
        index = Array.IndexOf(segments, _cloudStorageFolderName);
        if (index >= 1 && segments[index - 1] == "Library" && index + 1 <= last)
        {
            return (
                GetProviderRoot(isProviderAsked ? GetProvider(fileUrl) : null),
                FoldersBelow(segments, index + 1),
                index + 1 == last);
        }

        string[] folders = segments[..Math.Max(last, 0)];

        // Replicated File Providers, such as Dropbox, also tell their files as ubiquitous, but only iCloud tells the
        // name of the container.
        if (IsICloud(fileUrl))
        {
            return (RootProvider.ICloudDrive, folders, false);
        }

        return (OperatingSystem.IsMacCatalyst() ? RootProvider.None : RootProvider.Other, folders, false);
    }

    // The folders between where the storage starts and the file or folder itself.
    private static string[] FoldersBelow(string[] segments, int start) =>
        start + 1 >= segments.Length ? [] : segments[(start + 1)..^1];

    private static bool IsICloud(NSUrl fileUrl) =>
        fileUrl.TryGetResource(NSUrl.IsUbiquitousItemKey, out NSObject value, out NSError _) &&
        value is NSNumber { BoolValue: true } &&
        fileUrl.TryGetResource(NSUrl.UbiquitousItemContainerDisplayNameKey, out NSObject name, out NSError _) &&
        name is not null;

    // A provider such as OneDrive does not tell itself in the bookmark of a file until the file is on the device, which
    // it may not be when it is picked. Such a file is in cloud storage, since On My iPhone always keeps its files.
    private static RootProvider GetProviderRoot(string? provider) =>
        provider is null ? RootProvider.CloudStorage : _providers.GetValueOrDefault(provider, RootProvider.CloudStorage);

    /// <summary>
    /// The id of the File Provider that stores the file. No documented API tells it, but its bookmark does, as
    /// fp:/&lt;hash&gt;/&lt;provider id&gt;/&lt;item id&gt;. Returns null when the bookmark does not tell it.
    /// </summary>
    private static string? GetProvider(NSUrl fileUrl)
    {
        NSData bookmark = fileUrl.CreateBookmarkData(default, [], null, out NSError? error);
        if (error is not null)
        {
            return null;
        }

        Match match = ProviderPattern().Match(Encoding.Latin1.GetString([.. bookmark]));
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"fp:/[A-Za-z0-9+=_-]+/([A-Za-z0-9.-]+)/")]
    private static partial Regex ProviderPattern();

    private static RootProvider GetDeviceRoot() =>
        DeviceInfo.Current.Idiom == DeviceIdiom.Tablet ? RootProvider.IPad : RootProvider.IPhone;
}
