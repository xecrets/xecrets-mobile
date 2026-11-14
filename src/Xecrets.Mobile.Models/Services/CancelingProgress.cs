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

using Xecrets.Core.Models;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Cancels encrypting or decrypting by throwing <see cref="OperationCanceledException"/>, which is how the core is
/// canceled. The core reports the progress synchronously, as the data is read or written, and is canceled then, but
/// not when it reports that a stream is done, since that is when the stream is disposed.
/// </summary>
/// <param name="token">Tells when to cancel.</param>
internal sealed class CancelingProgress(CancellationToken token) : IProgress<Progress>
{
    /// <inheritdoc/>
    /// <remarks>
    /// Throws <see cref="OperationCanceledException"/> when the token is canceled and data is reported read or
    /// written.
    /// </remarks>
    public void Report(Progress value)
    {
        if (value.Kind == ProgressKind.CountAdded)
        {
            token.ThrowIfCancellationRequested();
        }
    }
}
