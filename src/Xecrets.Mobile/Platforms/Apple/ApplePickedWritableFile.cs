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
using System.IO;
using System.Threading.Tasks;

using Foundation;

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Platforms.Apple;

internal sealed class ApplePickedWritableFile(NSUrl url) : IPickedWritableFile
{
    // A rename gives the file a new NSUrl (a new path in the same directory), so the target of
    // subsequent access/write/delete calls has to track it - hence a mutable field.
    private NSUrl _fileUrl = url;

    public async Task<T> WithAccessAsync<T>(Func<Task<T>> action)
    {
        bool isAccessing = _fileUrl.StartAccessingSecurityScopedResource();
        if (!isAccessing)
        {
            throw new UnauthorizedAccessException("The selected file could not be accessed.");
        }

        try
        {
            return await action();
        }
        finally
        {
            _fileUrl.StopAccessingSecurityScopedResource();
        }
    }

    public Task<bool> CanWriteAsync() => Task.FromResult(IsWritable(_fileUrl));

    public Task<bool> CanDeleteAsync() => Task.FromResult(IsWritable(_fileUrl));

    public Task<long> GetLengthAsync() => Task.FromResult(new FileInfo(_fileUrl.Path!).Length);

    public Task<Stream> OpenWriteAsync() =>
        Task.FromResult<Stream>(new FileStream(_fileUrl.Path!, FileMode.Open, FileAccess.Write, FileShare.None));

    public Task RenameIfPossibleAsync(string newFileName)
    {
        try
        {
            string path = Path.Combine(_fileUrl.RemoveLastPathComponent().Path!, newFileName);
            File.Move(_fileUrl.Path!, path);
            _fileUrl = NSUrl.FromFilename(path);
            return Task.CompletedTask;
        }
        catch (IOException)
        {
            return Task.CompletedTask;
        }
        catch (UnauthorizedAccessException)
        {
            return Task.CompletedTask;
        }
    }

    public Task DeleteAsync()
    {
        File.Delete(_fileUrl.Path!);
        return Task.CompletedTask;
    }

    private static bool IsWritable(NSUrl url) =>
        url.TryGetResource(NSUrl.IsWritableKey, out NSObject value, out NSError _) && ((NSNumber)value).BoolValue;
}
