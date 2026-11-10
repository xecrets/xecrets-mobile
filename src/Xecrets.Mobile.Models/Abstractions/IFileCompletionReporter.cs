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

using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// Is told when an operation on a file in one of My folders is done, to tell the user and to list the result in the
/// recent files, or to keep account of the files done when the file is one of many. It also decides how the operation
/// on the files goes on.
/// </summary>
public interface IFileCompletionReporter
{
    /// <summary>
    /// Whether the operation goes on with the next file, once a file is done. The user is told when it does not.
    /// </summary>
    Task<bool> ContinuesAfterFileAsync();

    /// <summary>
    /// The file is left, since its contents show that it is not what the operation is for, as the message tells. The
    /// operation goes on with the next file.
    /// </summary>
    Task ReportSkippedAsync(string message);

    /// <summary>
    /// The file is left, since the user chose not to replace the file of the name the result would have. The operation
    /// goes on with the next file.
    /// </summary>
    Task ReportReplaceDeclinedAsync();

    /// <summary>
    /// All the files of the operation are done.
    /// </summary>
    Task ReportRunCompletedAsync();

    /// <summary>
    /// The operation is canceled, by the user or as the user chose, and the files after the last one done are left.
    /// </summary>
    Task ReportRunCanceledAsync();

    /// <summary>
    /// The file has been encrypted or decrypted, and replaced by the result, unless the original could not be removed.
    /// </summary>
    Task ReportReplacedAsync(FileReference result, string completedText, bool isOriginalRemoved);

    /// <summary>
    /// The file has been deleted, and removed from the recent files.
    /// </summary>
    Task ReportDeletedAsync(string completedText);
}
