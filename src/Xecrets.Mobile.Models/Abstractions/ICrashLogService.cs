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

using System.Runtime.CompilerServices;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// Keeps crash logs, and logs of exceptions that are handled without being passed on.
/// </summary>
public interface ICrashLogService
{
    /// <summary>
    /// True if there is a crash log that has not been read with <see cref="ReadCurrent"/>, false otherwise.
    /// </summary>
    bool HasPendingCrashLog { get; }

    /// <summary>
    /// Registers handlers that write a crash log for unhandled exceptions and unobserved task exceptions.
    /// </summary>
    void RegisterHandlers();

    /// <summary>
    /// Reads the pending crash log and keeps it as an earlier log, so that it is no longer pending.
    /// </summary>
    /// <returns>The pending crash log.</returns>
    /// <remarks>Requires <see cref="HasPendingCrashLog"/> to be true.</remarks>
    string ReadCurrent();

    /// <summary>
    /// Writes a crash log that becomes the pending crash log, replacing any earlier pending crash log.
    /// </summary>
    /// <param name="source">What caused the crash.</param>
    /// <param name="crash">The exception of the crash, or null if there is no managed exception.</param>
    void WriteCrashLog(string source, object? crash);

    /// <summary>
    /// Deletes all crash logs and logs of swallowed exceptions. A failure to delete is ignored.
    /// </summary>
    void DeleteAll();

    /// <summary>
    /// Logs an exception that is handled without being passed on. Nothing is shown to the user, and a failure to log
    /// is ignored.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="filePath">The path of the source file where the exception is handled.</param>
    /// <param name="memberName">The name of the member where the exception is handled.</param>
    /// <param name="lineNumber">The line number in the source file where the exception is handled.</param>
    void WriteSwallowedException(
        Exception exception,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0);
}
