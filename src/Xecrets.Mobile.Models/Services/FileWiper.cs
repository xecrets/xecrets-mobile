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

public sealed class FileWiper : IFileWiper
{
    public async Task<FileWipeStatus> WipeAsync(PickedWritableFile file)
    {
        FileWipeStatus status = FileWipeStatus.Succeeded;
        await file.WithAccessAsync(async () =>
        {
            if (!await file.CanWriteAsync() || !await file.CanDeleteAsync())
            {
                status = FileWipeStatus.InsufficientRights;
                return;
            }

            long length = await file.GetLengthAsync();
            await using Stream stream = await file.OpenWriteAsync();
            await OverwriteAsync(stream, length);

            // A plain FlushAsync only clears managed/OS buffers - for a genuine wipe of a file we don't
            // control, force the random overwriting to physical storage before it's renamed and deleted.
            if (stream is FileStream fileStream)
            {
                fileStream.Flush(true);
            }

            _ = await file.RenameIfPossibleAsync(Path.GetRandomFileName());
            await file.DeleteAsync();
        });

        return status;
    }

    public async Task OverwriteAsync(Stream stream, long length)
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
