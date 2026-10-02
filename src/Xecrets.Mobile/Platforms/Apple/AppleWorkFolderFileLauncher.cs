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

using System.Runtime.Versioning;
using System.Threading.Tasks;
using CoreGraphics;
using Foundation;
using UIKit;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// Hands the file itself to QuickLook or the share sheet, rather than a copy, keeping the access to it until they are
/// dismissed.
/// </summary>
[SupportedOSPlatform("ios")]
[SupportedOSPlatform("maccatalyst")]
public class AppleWorkFolderFileLauncher(AppleFileGrants grants) : IWorkFolderFileLauncher
{
    /// <summary>
    /// As the Files app does, previews the file with QuickLook, whose share button offers the apps it can be opened
    /// in. The share sheet offers them directly for files QuickLook cannot preview. Whether the app chosen can save
    /// its changes is decided by the platform.
    /// </summary>
    public async Task<bool> OpenAsync(FileReference file, bool allowWrite)
    {
        await grants.WithAccessAsync(file.Id, async fileUrl =>
        {
            await (QuickLookFileViewer.CanView(fileUrl)
                ? QuickLookFileViewer.ViewAsync(fileUrl)
                : PresentShareSheetAsync(fileUrl));
            return true;
        });
        return true;
    }

    public Task ShareAsync(FileReference file) =>
        grants.WithAccessAsync(file.Id, async fileUrl =>
        {
            await PresentShareSheetAsync(fileUrl);
            return true;
        });

    /// <summary>
    /// Presents the share sheet, completing when it is dismissed and any chosen activity has finished with the file.
    /// </summary>
    private static Task PresentShareSheetAsync(NSUrl fileUrl)
    {
        UIViewController presentingController = Platform.GetCurrentUIViewController()!;
        UIActivityViewController activityController = new([fileUrl], null);
        TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        activityController.CompletionWithItemsHandler = (_, _, _, _) => completed.TrySetResult();

        // Required on iPad and Mac, where the sheet is a popover.
        if (activityController.PopoverPresentationController is { } popover)
        {
            UIView sourceView = presentingController.View!;
            popover.SourceView = sourceView;
            popover.SourceRect = new CGRect(sourceView.Bounds.GetMidX(), sourceView.Bounds.GetMidY(), 0, 0);
            popover.PermittedArrowDirections = 0;
        }

        presentingController.PresentViewController(activityController, true, null);
        return completed.Task;
    }
}
