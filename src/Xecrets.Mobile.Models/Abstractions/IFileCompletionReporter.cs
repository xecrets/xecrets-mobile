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
/// Reports the outcome of each file of an operation on files in My folders, and of the operation as a whole, and
/// decides whether the operation goes on after each file.
/// </summary>
public interface IFileCompletionReporter
{
    /// <summary>
    /// Decides whether the operation goes on with the next file, once a file is done.
    /// </summary>
    /// <returns>True if the operation goes on, false if it stops, in which case the user has been told why.</returns>
    Task<bool> ContinuesAfterFileAsync();

    /// <summary>
    /// Reports that a file is left as it is, since its contents show that it is not what the operation is for. The
    /// operation goes on with the next file.
    /// </summary>
    /// <param name="message">Tells why the file is left.</param>
    Task ReportSkippedAsync(string message);

    /// <summary>
    /// Reports that a file is left as it is, since the user declined to replace the existing file with the name of the
    /// result. The operation goes on with the next file.
    /// </summary>
    Task ReportReplaceDeclinedAsync();

    /// <summary>
    /// Reports that all the files of the operation are done.
    /// </summary>
    Task ReportRunCompletedAsync();

    /// <summary>
    /// Reports that the operation is canceled, and that the files after the last one done are left as they are.
    /// </summary>
    Task ReportRunCanceledAsync();

    /// <summary>
    /// Reports that a file is encrypted or decrypted, and replaced by the result.
    /// </summary>
    /// <param name="result">The resulting file.</param>
    /// <param name="completedText">Tells that the file is done.</param>
    /// <param name="isOriginalRemoved">True if the original file is removed, false if it is left.</param>
    Task ReportReplacedAsync(FileReference result, string completedText, bool isOriginalRemoved);

    /// <summary>
    /// Reports that a file is deleted, and removed from the recent files.
    /// </summary>
    /// <param name="completedText">Tells that the file is deleted.</param>
    Task ReportDeletedAsync(string completedText);
}
