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
using System.Runtime.Versioning;
using System.Threading.Tasks;

using Foundation;

using QuickLook;

using UIKit;

using Xecrets.Mobile.Models.Models;

using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace Xecrets.Mobile.Platforms.Apple;

[SupportedOSPlatform("ios")]
[SupportedOSPlatform("maccatalyst")]
internal static class QuickLookFileViewer
{
    public static Task<bool> CanViewAsync(DecryptedFileInfo file) =>
        Task.FromResult(QLPreviewController.CanPreviewItem(NSUrl.FromFilename(file.FilePath)));

    public static async Task ViewAsync(DecryptedFileInfo file)
    {
        NSUrl item = NSUrl.FromFilename(file.FilePath);
        UIViewController presentingController = Platform.GetCurrentUIViewController()!;
        SingleItemQuickLookController previewController = new(item);
        TaskCompletionSource dismissed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler? didDismissHandler = null;
        didDismissHandler = (_, _) =>
        {
            previewController.DidDismiss -= didDismissHandler;
            dismissed.TrySetResult();
        };

        previewController.DidDismiss += didDismissHandler;
        presentingController.PresentViewController(previewController, true, null);

        await dismissed.Task;
    }

    private sealed class SingleItemQuickLookController : QLPreviewController
    {
        public SingleItemQuickLookController(IQLPreviewItem item)
        {
            DataSource = new SingleItemQuickLookDataSource(item);
        }
    }

    private sealed class SingleItemQuickLookDataSource(IQLPreviewItem item) : QLPreviewControllerDataSource
    {
        public override nint PreviewItemCount(QLPreviewController controller) => 1;

        public override IQLPreviewItem GetPreviewItem(QLPreviewController controller, nint index)
        {
            return item;
        }
    }
}
