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
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Reports the outcome of an operation on a single file. The user is told of the outcome of the file, and a replaced
/// file is added to the recent files.
/// </summary>
internal sealed class SingleFileCompletionReporter(
    IRecentFilesService recentFilesService,
    IUserInterfaceService userInterfaceService) : IFileCompletionReporter
{
    /// <inheritdoc/>
    /// <remarks>
    /// The file is the only one, so there is no next file to stop before.
    /// </remarks>
    public Task<bool> ContinuesAfterFileAsync() => Task.FromResult(true);

    /// <inheritdoc/>
    public Task ReportSkippedAsync(string message) => userInterfaceService.DisplayMessageAsync(message);

    /// <inheritdoc/>
    public Task ReportReplaceDeclinedAsync() =>
        userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextOperationNotCompleted);

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing is reported, since the user is told when the file is done.
    /// </remarks>
    public Task ReportRunCompletedAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ReportRunCanceledAsync() =>
        userInterfaceService.DisplayTransientMessageAsync(MobileTexts.DialogTextOperationNotCompleted);

    /// <inheritdoc/>
    public async Task ReportReplacedAsync(FileReference result, string completedText, bool isOriginalRemoved)
    {
        await recentFilesService.AddAsync(result, RecentFileOperation.InPlace);
        await userInterfaceService.DisplayTransientMessageAsync(
            isOriginalRemoved ? completedText : MobileTexts.DialogTextOriginalNotRemoved);
    }

    /// <inheritdoc/>
    public Task ReportDeletedAsync(string completedText) =>
        userInterfaceService.DisplayTransientMessageAsync(completedText);
}
