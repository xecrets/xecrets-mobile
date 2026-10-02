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
using System.Text;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Wipes a file by renaming it to an obviously wiped name, replacing its contents with a notice and finally deleting
/// it. Storage that keeps versions of a file, or only moves a deleted file to a trash, such as Google Drive, then keeps
/// the notice under the wiped name, rather than the original.
/// </summary>
public sealed class FileWiper : IFileWiper
{
    /// <summary>
    /// The notice left in a wiped file, for anyone who finds what storage keeps of it. Not translated, so that it reads
    /// the same wherever it is found.
    /// </summary>
    public const string DeletedNotice = "Deleted by Xecrets Ez. Ensure to delete all versions of this file permanently.";

    public Task<bool> CanWipeAsync(IWritableFile file) => file.WithAccessAsync(() => HasRightsAsync(file));

    /// <summary>
    /// There are too many caveats with wiping a file that is on a mobile phone, or in cloud storage. Versions are created and many operations are not
    /// supported. So this is really a best effort, and we only do any attempt to overwrite if the stream is seekable.
    /// </summary>
    public Task<FileWipeStatus> WipeAsync(IWritableFile file)
    {
        return file.WithAccessAsync(async () =>
        {
            if (!await HasRightsAsync(file))
            {
                return FileWipeStatus.InsufficientRights;
            }

            bool result;
            try
            {
                result = await WipeCoreAsync(file);
            }
            catch
            {
                await TryDeleteAsync(file);
                throw;
            }

            return result ? FileWipeStatus.Succeeded : FileWipeStatus.OnlyDelete;
        });
    }

    private static async Task<bool> HasRightsAsync(IWritableFile file) =>
        await file.CanWriteAsync() && await file.CanDeleteAsync();

    /// <summary>
    /// On a stream that can seek, the notice is written first and the rest of the file is overwritten with random
    /// data, after which the file is cut down to the notice. A stream that cannot seek, as for some storage, is only
    /// given the notice.
    /// </summary>
    /// <returns>
    /// True if any meaningful wipe was done, false if the file was only renamed and deleted. The latter is the case for a stream that cannot seek.
    /// </returns>
    private static async Task<bool> WipeCoreAsync(IWritableFile file)
    {
        await file.RenameIfPossibleAsync($".xecrets-wiped-{Guid.NewGuid():N}.txt");
        long length = await file.GetLengthAsync();
        bool canSeek;
        await using (Stream stream = await file.OpenWriteAsync())
        {
            canSeek = stream.CanSeek;
            byte[] notice = Encoding.UTF8.GetBytes(DeletedNotice);
            await stream.WriteAsync(notice);
            if (canSeek)
            {
                await OverwriteAsync(stream, length - notice.Length);
                stream.SetLength(notice.Length);
            }
        }

        await file.DeleteAsync();
        return canSeek;
    }

    // The file keeps track of a rename, so this deletes it also when the wipe failed after renaming it.
    private static async Task TryDeleteAsync(IWritableFile file)
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
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
