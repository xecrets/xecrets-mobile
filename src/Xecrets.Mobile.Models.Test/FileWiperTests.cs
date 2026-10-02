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

using System.Text;

using NUnit.Framework;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class FileWiperTests
{
    private sealed class FakeWritableFile : IWritableFile
    {
        public Func<Task<bool>> CanWrite { get; init; } = () => Task.FromResult(true);

        public Func<Task<bool>> CanDelete { get; init; } = () => Task.FromResult(true);

        public Func<Task<long>> GetLength { get; init; } = () => throw new AssertionException("The file should not be inspected.");

        public Func<Task<Stream>> OpenWrite { get; init; } = () => throw new AssertionException("The file should not be opened.");

        public Func<string, Task<bool>> RenameIfPossible { get; init; } = _ => throw new AssertionException("The file should not be renamed.");

        public Func<Task> Delete { get; init; } = () => throw new AssertionException("The file should not be deleted.");

        public string Id => "file";

        public Task<T> WithAccessAsync<T>(Func<Task<T>> action) => action();

        public Task<bool> CanWriteAsync() => CanWrite();

        public Task<bool> CanDeleteAsync() => CanDelete();

        public Task<long> GetLengthAsync() => GetLength();

        public Task<Stream> OpenWriteAsync() => OpenWrite();

        public Task<bool> RenameIfPossibleAsync(string newFileName) => RenameIfPossible(newFileName);

        public Task DeleteAsync() => Delete();
    }

    [Test]
    public async Task WipeAsyncReturnsInsufficientRightsWithoutChangingFile()
    {
        bool wasOpened = false;
        bool wasDeleted = false;
        IWritableFile file = new FakeWritableFile
        {
            CanWrite = () => Task.FromResult(false),
            OpenWrite = () =>
            {
                wasOpened = true;
                return Task.FromResult<Stream>(Stream.Null);
            },
            Delete = () =>
            {
                wasDeleted = true;
                return Task.CompletedTask;
            },
        };

        FileWipeStatus status = await new FileWiper().WipeAsync(file);

        Assert.That(status, Is.EqualTo(FileWipeStatus.InsufficientRights));
        Assert.That(wasOpened, Is.False);
        Assert.That(wasDeleted, Is.False);
    }

    [Test]
    public async Task WipeAsyncDeletesFileWhenRenameIsNotSupported()
    {
        byte[] contents = new byte[1024];
        bool wasDeleted = false;
        IWritableFile file = new FakeWritableFile
        {
            GetLength = () => Task.FromResult((long)contents.Length),
            OpenWrite = () => Task.FromResult<Stream>(new MemoryStream(contents, writable: true)),
            RenameIfPossible = _ => Task.FromResult(false),
            Delete = () =>
            {
                wasDeleted = true;
                return Task.CompletedTask;
            },
        };

        FileWipeStatus status = await new FileWiper().WipeAsync(file);

        Assert.That(status, Is.EqualTo(FileWipeStatus.Succeeded));
        Assert.That(wasDeleted, Is.True);
        Assert.That(contents, Is.Not.All.EqualTo((byte)0));
    }

    /// <summary>
    /// Storage that keeps versions, or moves a deleted file to a trash, then keeps only the notice, named as wiped,
    /// rather than the original.
    /// </summary>
    [Test]
    public async Task WipeAsyncRenamesOverwritesAndLeavesOnlyNoticeBeforeDeleting()
    {
        List<string> steps = [];
        MemoryStream written = new();
        string? newName = null;
        IWritableFile file = new FakeWritableFile
        {
            GetLength = () => Task.FromResult(4096L),
            OpenWrite = () =>
            {
                steps.Add("write");
                return Task.FromResult<Stream>(new UnclosedStream(written));
            },
            RenameIfPossible = name =>
            {
                steps.Add("rename");
                newName = name;
                return Task.FromResult(true);
            },
            Delete = () =>
            {
                steps.Add("delete");
                return Task.CompletedTask;
            },
        };

        FileWipeStatus status = await new FileWiper().WipeAsync(file);

        byte[] notice = Encoding.UTF8.GetBytes(FileWiper.DeletedNotice);
        Assert.That(status, Is.EqualTo(FileWipeStatus.Succeeded));
        Assert.That(steps, Is.EqualTo(["rename", "write", "delete"]));
        Assert.That(newName, Does.Match("^\\.xecrets-wiped-[0-9a-f]{32}\\.txt$"));
        Assert.That(written.ToArray(), Is.EqualTo(notice));
    }

    /// <summary>
    /// Some storage only gives streams that cannot seek, which are not overwritten, but only given the notice.
    /// </summary>
    [Test]
    public async Task WipeAsyncOnlyWritesNoticeWhenStreamCannotSeek()
    {
        List<MemoryStream> opened = [];
        IWritableFile file = new FakeWritableFile
        {
            GetLength = () => Task.FromResult(4096L),
            OpenWrite = () =>
            {
                MemoryStream stream = new();
                opened.Add(stream);
                return Task.FromResult<Stream>(new UnseekableStream(stream));
            },
            RenameIfPossible = _ => Task.FromResult(true),
            Delete = () => Task.CompletedTask,
        };

        await new FileWiper().WipeAsync(file);

        Assert.That(opened, Has.Count.EqualTo(1));
        Assert.That(Encoding.UTF8.GetString(opened[0].ToArray()), Is.EqualTo(FileWiper.DeletedNotice));
    }

    [Test]
    public void WipeAsyncDeletesFileAndReportsFailureWhenOverwriteFails()
    {
        bool wasDeleted = false;
        IWritableFile file = new FakeWritableFile
        {
            GetLength = () => Task.FromResult(1024L),
            OpenWrite = () => throw new IOException("Overwrite failed."),
            RenameIfPossible = _ => Task.FromResult(true),
            Delete = () =>
            {
                wasDeleted = true;
                return Task.CompletedTask;
            },
        };

        Assert.That(async () => await new FileWiper().WipeAsync(file),
            Throws.TypeOf<IOException>().With.Message.EqualTo("Overwrite failed."));
        Assert.That(wasDeleted, Is.True);
    }

    [Test]
    public void WipeAsyncReportsOriginalFailureWhenDeleteAlsoFails()
    {
        IWritableFile file = new FakeWritableFile
        {
            GetLength = () => Task.FromResult(1024L),
            OpenWrite = () => throw new IOException("Overwrite failed."),
            RenameIfPossible = _ => Task.FromResult(true),
            Delete = () => throw new IOException("Delete failed."),
        };

        Assert.That(async () => await new FileWiper().WipeAsync(file),
            Throws.TypeOf<IOException>().With.Message.EqualTo("Overwrite failed."));
    }

    // Keeps the contents readable after the wiper has disposed the stream.
    private sealed class UnclosedStream(MemoryStream stream) : Stream
    {
        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => stream.CanSeek;
        public override bool CanWrite => stream.CanWrite;
        public override long Length => stream.Length;
        public override long Position { get => stream.Position; set => stream.Position = value; }
        public override void Flush() => stream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);
        public override void SetLength(long value) => stream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);
    }

    private sealed class UnseekableStream(MemoryStream stream) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => stream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);
    }
}
