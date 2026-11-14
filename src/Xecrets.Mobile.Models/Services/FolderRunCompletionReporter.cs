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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Reports the outcome of an operation on the files of a folder. The user is not told of each file, and the files are
/// not added to the recent files, which a whole folder would crowd out. The files done are counted for a summary at the
/// end, which tells whether all are done or the operation is canceled, followed by the count.
/// </summary>
/// <param name="userInterfaceService">Tells the user.</param>
/// <param name="countFormat">The format of the count in the summary, with the count text as {0}.</param>
internal sealed class FolderRunCompletionReporter(IUserInterfaceService userInterfaceService, string countFormat)
    : IFileCompletionReporter
{
    private int _completedCount;

    // Whether the original of the last file could not be removed, which stops the others.
    private bool _isOriginalLeft;

    /// <inheritdoc/>
    /// <remarks>
    /// The operation stops when the original of the last file is left.
    /// </remarks>
    public async Task<bool> ContinuesAfterFileAsync()
    {
        if (_isOriginalLeft)
        {
            await userInterfaceService.DisplayMessageAsync(MobileTexts.DialogTextOriginalNotRemoved);
        }

        return !_isOriginalLeft;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Files of a folder that are not what the operation is for are passed over silently, and not counted.
    /// </remarks>
    public Task ReportSkippedAsync(string message) => Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>
    /// The user has just answered for the file, which is passed over silently and not counted.
    /// </remarks>
    public Task ReportReplaceDeclinedAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ReportRunCompletedAsync() => ReportSummaryAsync(MobileTexts.DialogTextDone);

    /// <inheritdoc/>
    public Task ReportRunCanceledAsync() => ReportSummaryAsync(MobileTexts.DialogTextCanceled);

    /// <inheritdoc/>
    /// <remarks>
    /// The file is counted.
    /// </remarks>
    public Task ReportReplacedAsync(FileReference result, string completedText, bool isOriginalRemoved)
    {
        _completedCount++;
        _isOriginalLeft = !isOriginalRemoved;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The file is counted.
    /// </remarks>
    public Task ReportDeletedAsync(string completedText)
    {
        _completedCount++;
        return Task.CompletedTask;
    }

    private Task ReportSummaryAsync(string outcome) =>
        userInterfaceService.DisplayTransientMessageAsync(
            $"{outcome} {string.Format(countFormat, _completedCount.ToCountText())}");
}
