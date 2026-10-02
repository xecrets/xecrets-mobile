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
/// A file the app itself owns in its local storage, such as in the cache, accessed by its path.
/// </summary>
public sealed class LocalWritableFile(string path) : IWritableFile
{
    // A rename moves the file to a new path, so the target of subsequent calls has to track it.
    private string _path = path;

    public string Id => _path;

    public Task<T> WithAccessAsync<T>(Func<Task<T>> action) => action();

    public Task<bool> CanWriteAsync() => Task.FromResult(IsWritable());

    public Task<bool> CanDeleteAsync() => Task.FromResult(IsWritable());

    public Task<long> GetLengthAsync() => Task.FromResult(new FileInfo(_path).Length);

    public Task<Stream> OpenWriteAsync() =>
        Task.FromResult<Stream>(new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.None));

    public Task<bool> RenameIfPossibleAsync(string newFileName)
    {
        try
        {
            string renamedPath = Path.Combine(Path.GetDirectoryName(_path)!, newFileName);
            File.Move(_path, renamedPath);
            _path = renamedPath;
            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    public Task DeleteAsync()
    {
        File.Delete(_path);
        return Task.CompletedTask;
    }

    private bool IsWritable() => (File.GetAttributes(_path) & FileAttributes.ReadOnly) == 0;
}
