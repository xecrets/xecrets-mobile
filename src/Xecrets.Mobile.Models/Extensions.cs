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

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models;

/// <summary>
/// Miscellaneous extensions of the models.
/// </summary>
internal static class Extensions
{
    extension(string value)
    {
        /// <summary>
        /// The text with its first character in upper case and the rest in lower case, as the current culture does.
        /// </summary>
        /// <returns>The text in sentence case.</returns>
        /// <remarks>Requires the text not to be empty.</remarks>
        public string ToSentenceCase() =>
            char.ToUpper(value[0]) + value[1..].ToLower();

        /// <summary>
        /// Opens the file of the path for reading, if it exists.
        /// </summary>
        /// <returns>A stream that reads the file, or null if the file or its folder does not exist.</returns>
        public FileStream? OpenReadIfExists()
        {
            try
            {
                return File.OpenRead(value);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// The name a file is kept under while it is saved over, with its extension in the name, as for an encrypted
        /// file, so that it is seen what it is: "report-txt.axx" is kept as "report-txt-axx.bak".
        /// </summary>
        /// <returns>The backup name.</returns>
        public string ToBackupName() =>
            Path.GetExtension(value) is { Length: > 1 } extension
                ? $"{Path.GetFileNameWithoutExtension(value)}-{extension[1..]}.bak"
                : $"{Path.GetFileNameWithoutExtension(value)}.bak";
    }

    /// <summary>
    /// Tells if an operation changes the file, which is then listed as reached through one of My folders. Other files
    /// are only read, and are listed with a grant to the file itself.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>True if the operation changes the file, false if it only reads it.</returns>
    public static bool IsWriteClass(this RecentFileOperation operation) =>
        operation is RecentFileOperation.InPlace or RecentFileOperation.Edit;

    /// <summary>
    /// Creates the entry of the recent files for a file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="operation">The operation done on the file.</param>
    /// <returns>The entry.</returns>
    public static RecentFile ToRecentFile(this FileReference file, RecentFileOperation operation) =>
        new() { Id = file.Id, Name = file.Name, Root = file.Root, FolderPath = file.FolderPath, Operation = operation };

    /// <summary>
    /// Creates the reference to the file of an entry of the recent files.
    /// </summary>
    /// <param name="recentFile">The entry.</param>
    /// <returns>The reference to the file.</returns>
    public static FileReference ToFileReference(this RecentFile recentFile) =>
        new(recentFile.Id, recentFile.Name) { Root = recentFile.Root, FolderPath = recentFile.FolderPath };

    private static string Stringify(this DontShowAgain dontShowAgain)
    {
        List<string> values = [];
        foreach (DontShowAgain value in Enum.GetValues<DontShowAgain>())
        {
            if (value == DontShowAgain.None)
            {
                continue;
            }

            if (dontShowAgain.HasFlag(value))
            {
                values.Add(value.ToString());
            }
        }

        return string.Join(",", values);
    }

    extension(ProfileDeviceSettings settings)
    {
        /// <summary>
        /// Tells if a notice is to be shown, which it is unless it is set not to be shown again.
        /// </summary>
        /// <param name="flagToCheck">The notice.</param>
        /// <returns>True if the notice is to be shown, false otherwise.</returns>
        public bool ShouldShow(DontShowAgain flagToCheck) =>
            !ParseDontShowAgain(settings.DontShowAgain).HasFlag(flagToCheck);

        /// <summary>
        /// Sets a notice not to be shown again, in addition to those already set.
        /// </summary>
        /// <param name="flagToSet">The notice.</param>
        public void SetDontShowAgain(DontShowAgain flagToSet) =>
            settings.DontShowAgain = (ParseDontShowAgain(settings.DontShowAgain) | flagToSet).Stringify();
    }

    private static DontShowAgain ParseDontShowAgain(string dontShowAgain)
    {
        DontShowAgain flags = DontShowAgain.None;
        foreach (string value in dontShowAgain.Split(','))
        {
            if (Enum.TryParse(value, out DontShowAgain flag))
            {
                flags |= flag;
            }
        }

        return flags;
    }

    extension(RootProvider root)
    {
        /// <summary>
        /// The root as the first segment of a path to show. There is none when the path starts at the top, or when
        /// nothing is known of where the file or folder is stored.
        /// </summary>
        /// <returns>The segment, or no segment.</returns>
        public IReadOnlyList<string> ToPathSegments() =>
            root.ToDisplay() is { Length: > 0 } display ? [display] : [];

        /// <summary>
        /// The root as a short text to show.
        /// </summary>
        /// <returns>The text, or "" if there is nothing meaningful to show.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The root is not a defined value.</exception>
        public string ToDisplay() => root switch
        {
            RootProvider.Unknown => "",
            RootProvider.None => "",
            RootProvider.Other => "",
            RootProvider.AndroidPhone => "Phone",
            RootProvider.AndroidTablet => "Tablet",
            RootProvider.AndroidDevice => "Device",
            RootProvider.IPhone => "On My iPhone",
            RootProvider.IPad => "On My iPad",
            RootProvider.Mac => "",
            RootProvider.RemovableStorage => "Media",
            RootProvider.ExternalDrive => "Media",
            RootProvider.Downloads => "Downloads",
            RootProvider.MediaImages => "Images",
            RootProvider.MediaVideos => "Videos",
            RootProvider.MediaAudio => "Audio",
            RootProvider.MediaDocuments => "Documents",
            RootProvider.ICloudDrive => "iCloud",
            RootProvider.GoogleDrive => "Drive",
            RootProvider.OneDrive => "OneDrive",
            RootProvider.Dropbox => "Dropbox",
            RootProvider.CloudStorage => "Cloud",
            _ => throw new ArgumentOutOfRangeException(nameof(root), root, @"Unknown root provider."),
        };

        /// <summary>
        /// Tells if the root is storage in the device itself, where overwriting a file replaces its contents.
        /// Removable media and cloud storage are not, nor is storage that is not known.
        /// </summary>
        /// <returns>True if the root is storage in the device, false otherwise.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The root is not a defined value.</exception>
        public bool IsOnDevice() => root switch
        {
            RootProvider.Unknown => false,
            RootProvider.None => true,
            RootProvider.Other => false,
            RootProvider.AndroidPhone => true,
            RootProvider.AndroidTablet => true,
            RootProvider.AndroidDevice => true,
            RootProvider.IPhone => true,
            RootProvider.IPad => true,
            RootProvider.Mac => true,
            RootProvider.RemovableStorage => false,
            RootProvider.ExternalDrive => false,
            RootProvider.Downloads => true,
            RootProvider.MediaImages => true,
            RootProvider.MediaVideos => true,
            RootProvider.MediaAudio => true,
            RootProvider.MediaDocuments => true,
            RootProvider.ICloudDrive => false,
            RootProvider.GoogleDrive => false,
            RootProvider.OneDrive => false,
            RootProvider.Dropbox => false,
            RootProvider.CloudStorage => false,
            _ => throw new ArgumentOutOfRangeException(nameof(root), root, @"Unknown root provider."),
        };

        /// <summary>
        /// Tells if wiping a file in the root is known to leave nothing to recover. On the device the file is
        /// overwritten, and iCloud Drive removes all versions of it. Other storage may keep versions or a trash.
        /// </summary>
        /// <returns>True if nothing is left to recover, false otherwise.</returns>
        public bool IsWipedForGood() => root.IsOnDevice() || root == RootProvider.ICloudDrive;
    }

    /// <summary>
    /// Combines a message to the user with the message of an exception, or its type name if it has none.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The combined message.</returns>
    public static string FormatException(this Exception exception)
    {
        string exceptionMessage = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;

        return string.Format(MobileTexts.DialogTextExceptionFormat, exceptionMessage);
    }

    /// <summary>
    /// The size as a short text to show, in the largest unit of 1024 that it reaches, up to gigabytes, with the number
    /// formatted as the current culture does.
    /// </summary>
    /// <param name="size">The size in bytes.</param>
    /// <returns>The size text.</returns>
    public static string ToSizeText(this long size) => size switch
    {
        < 1L << 10 => string.Format(MobileTexts.FileSizeFormat, ToNumberText(size, "N0")),
        < 1L << 20 => string.Format(MobileTexts.FileSizeKilobytesFormat, ToUnitsText(size, 10)),
        < 1L << 30 => string.Format(MobileTexts.FileSizeMegabytesFormat, ToUnitsText(size, 20)),
        _ => string.Format(MobileTexts.FileSizeGigabytesFormat, ToUnitsText(size, 30)),
    };

    // The size in units of two to the power of the shift, with at most one decimal.
    private static string ToUnitsText(long size, int shift) => ToNumberText(size / (double)(1L << shift), "#,0.#");

    // A culture that separates the digit groups with spaces gets non-breaking ones, keeping the number on one line.
    private static string ToNumberText<T>(T number, string format) where T : IFormattable =>
        number.ToString(format, CultureInfo.CurrentCulture).Replace(" ", "\u00A0", StringComparison.Ordinal);

    /// <summary>
    /// The count as a number to show, formatted as the current culture does.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <returns>The count text.</returns>
    public static string ToCountText(this int count) => ToNumberText(count, "N0");

    extension(IUserInterfaceService userInterfaceService)
    {
        /// <summary>
        /// Navigates to a page without a parameter, as <see cref="IUserInterfaceService.NavigateToAsync"/> does.
        /// </summary>
        /// <param name="destination">The page to navigate to.</param>
        public Task NavigateToAsync(AppDestination destination) =>
            userInterfaceService.NavigateToAsync(destination, null);

        /// <summary>
        /// Navigates to a page in place of the current page without a parameter, as
        /// <see cref="IUserInterfaceService.NavigateReplacingAsync"/> does.
        /// </summary>
        /// <param name="destination">The page to navigate to.</param>
        public Task NavigateReplacingAsync(AppDestination destination) =>
            userInterfaceService.NavigateReplacingAsync(destination, null);
    }
}
