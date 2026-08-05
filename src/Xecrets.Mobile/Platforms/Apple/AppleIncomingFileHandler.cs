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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Foundation;

using Microsoft.Extensions.DependencyInjection;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Platforms.Apple;

internal static class AppleIncomingFileHandler
{
    public static async Task HandleIncomingUrlAsync(NSUrl url)
    {
        try
        {
            ITransientFileService transientFileStore = MauiProgram.Services!.GetRequiredService<ITransientFileService>();
            IIncomingFileService incomingFileService = MauiProgram.Services!.GetRequiredService<IIncomingFileService>();
            IFileService fileService = MauiProgram.Services!.GetRequiredService<IFileService>();
            if (!url.IsFileUrl)
            {
                return;
            }

            bool securityScoped = url.StartAccessingSecurityScopedResource();
            try
            {
                string sourcePath = url.Path ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    return;
                }

                if (fileService.IsSelfHandoffReference(sourcePath))
                {
                    IUserInterfaceService userInterfaceService = MauiProgram.Services!.GetRequiredService<IUserInterfaceService>();
                    await userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextSelfHandoffRejected);
                    return;
                }

                string displayName = Path.GetFileName(sourcePath);
                string contentType = ContentTypeDetector.DetectContentType(displayName);
                string incomingPath = transientFileStore.CreateIncomingPath(displayName);

                await using (FileStream inputStream = File.OpenRead(sourcePath))
                await using (FileStream outputStream =
                             File.Open(incomingPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    await inputStream.CopyToAsync(outputStream);
                }

                await incomingFileService.ReceiveAsync(new IncomingFileInfo(incomingPath, displayName, contentType));
            }
            finally
            {
                if (securityScoped)
                {
                    url.StopAccessingSecurityScopedResource();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not open incoming Apple file URL '{url}'. {ex}");
            throw;
        }
    }
}
