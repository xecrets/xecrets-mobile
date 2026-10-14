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

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Writes the destination file in place. A new file is created only if there is none with the name, so that an
/// existing file is never replaced unless that is allowed.
/// </summary>
public sealed class LocalFileWriteSession(string folderPath, string name, bool overwrite) : IFileWriteSession
{
    private readonly string _destinationPath = Path.Combine(folderPath, name);
    private bool _isCommitted;

    public Stream OutputStream { get; } = new FileStream(
        Path.Combine(folderPath, name),
        overwrite ? FileMode.Create : FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None);

    public async Task<string> CommitAsync()
    {
        await OutputStream.DisposeAsync();
        _isCommitted = true;
        return _destinationPath;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await OutputStream.DisposeAsync();
        }
        finally
        {
            if (!_isCommitted)
            {
                File.Delete(_destinationPath);
            }
        }
    }
}
