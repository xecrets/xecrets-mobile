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
/// Receives files from other apps, one at a time. A received file waits until the user is signed in, and is then
/// previewed if it is encrypted, and encrypted otherwise. The members may be called from any thread.
/// </summary>
public interface IIncomingFileService
{
    /// <summary>
    /// Receives a file from another app and handles it, now or once it can be handled. The file is ignored if another
    /// file is already waiting, or if files cannot be received now.
    /// </summary>
    /// <param name="source">The source of the received file.</param>
    Task ReceiveAsync(IIncomingFileSource source);

    /// <summary>
    /// Receives a message to show in place of a file, now or once it can be shown. It replaces any file that is
    /// waiting.
    /// </summary>
    /// <param name="message">The message to show.</param>
    Task ReceiveMessageAsync(string message);

    /// <summary>
    /// Handles the file or message that is waiting, if any, as far as it can be handled now.
    /// </summary>
    Task ProcessPendingAsync();
}
