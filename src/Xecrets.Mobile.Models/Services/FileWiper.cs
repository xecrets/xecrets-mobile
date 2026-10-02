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

using System.Buffers;
using System.Security.Cryptography;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Wipes a file by renaming it to an obviously wiped name, overwriting it with random data, truncating it and finally
/// deleting it. Storage that only moves a deleted file to a trash, such as Google Drive, then keeps an empty file
/// named as wiped, rather than the original.
/// </summary>
public sealed class FileWiper : IFileWiper
{
    public Task<bool> CanWipeAsync(IPickedWritableFile file) => file.WithAccessAsync(() => HasRightsAsync(file));

    /// <summary>
    /// Wipes the file, if there are rights to do so. Should the wipe itself fail, which is not expected, the file is
    /// still deleted if possible, and the failure is reported.
    /// </summary>
    public Task<FileWipeStatus> WipeAsync(IPickedWritableFile file)
    {
        return file.WithAccessAsync(async () =>
        {
            if (!await HasRightsAsync(file))
            {
                return FileWipeStatus.InsufficientRights;
            }

            try
            {
                await WipeCoreAsync(file);
            }
            catch
            {
                await TryDeleteAsync(file);
                throw;
            }

            return FileWipeStatus.Succeeded;
        });
    }

    private static async Task<bool> HasRightsAsync(IPickedWritableFile file) =>
        await file.CanWriteAsync() && await file.CanDeleteAsync();

    private static async Task WipeCoreAsync(IPickedWritableFile file)
    {
        await file.RenameIfPossibleAsync($".xecrets-wiped-{Guid.NewGuid():N}");

        long length = await file.GetLengthAsync();
        await using (Stream stream = await file.OpenWriteAsync())
        {
            await OverwriteAsync(stream, length);

            // A plain FlushAsync only clears managed/OS buffers - for a genuine wipe of a file we don't
            // control, force the random overwriting to physical storage before it's truncated and deleted.
            if (stream is FileStream fileStream)
            {
                fileStream.Flush(true);
            }
        }

        await file.TruncateAsync();
        await file.DeleteAsync();
    }

    // The file keeps track of a rename, so this deletes it also when the wipe failed after renaming it.
    private static async Task TryDeleteAsync(IPickedWritableFile file)
    {
        try
        {
            await file.DeleteAsync();
        }
        catch
        {
            // Best effort - the failure of the wipe itself is what is reported.
        }
    }

    private static async Task OverwriteAsync(Stream stream, long length)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long remaining = length;
            while (remaining > 0)
            {
                int toWrite = (int)Math.Min(buffer.Length, remaining);
                RandomNumberGenerator.Fill(buffer.AsSpan(0, toWrite));
                await stream.WriteAsync(buffer.AsMemory(0, toWrite));
                remaining -= toWrite;
            }

            await stream.FlushAsync();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
