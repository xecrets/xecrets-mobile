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

using NUnit.Framework;

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class FileWiperTests
{
    [Test]
    public async Task WipeAsyncReturnsInsufficientRightsWithoutChangingFile()
    {
        bool wasOpened = false;
        bool wasDeleted = false;
        PickedWritableFile file = new(
            "document.txt",
            action => action(),
            () => Task.FromResult(false),
            () => Task.FromResult(true),
            () => throw new AssertionException("The file should not be inspected."),
            () =>
            {
                wasOpened = true;
                return Task.FromResult<Stream>(Stream.Null);
            },
            _ => throw new AssertionException("The file should not be renamed."),
            () =>
            {
                wasDeleted = true;
                return Task.CompletedTask;
            });

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
        PickedWritableFile file = new(
            "document.txt",
            action => action(),
            () => Task.FromResult(true),
            () => Task.FromResult(true),
            () => Task.FromResult((long)contents.Length),
            () => Task.FromResult<Stream>(new MemoryStream(contents, writable: true)),
            _ => Task.FromResult(false),
            () =>
            {
                wasDeleted = true;
                return Task.CompletedTask;
            });

        FileWipeStatus status = await new FileWiper().WipeAsync(file);

        Assert.That(status, Is.EqualTo(FileWipeStatus.Succeeded));
        Assert.That(wasDeleted, Is.True);
        Assert.That(contents, Is.Not.All.EqualTo((byte)0));
    }
}
