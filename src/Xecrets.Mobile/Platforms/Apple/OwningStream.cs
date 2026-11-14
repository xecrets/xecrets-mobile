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

using System.IO;

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// A stream that reads a temporary file, and disposes the file when the stream is disposed.
/// </summary>
/// <param name="file">The temporary file, which is opened for reading, and which the stream takes over.</param>
internal sealed class OwningStream(ITransientFile file) : Stream
{
    private readonly FileStream _stream = File.OpenRead(file.FilePath);

    /// <inheritdoc/>
    public override bool CanRead => _stream.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => _stream.CanSeek;

    /// <inheritdoc/>
    public override bool CanWrite => _stream.CanWrite;

    /// <inheritdoc/>
    public override long Length => _stream.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    /// <inheritdoc/>
    public override void Flush() => _stream.Flush();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

    /// <inheritdoc/>
    public override void SetLength(long value) => _stream.SetLength(value);

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                _stream.Dispose();
                file.Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
