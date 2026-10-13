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
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class LocalFileWriteSessionTests
{
    private string _directory = "";

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory().FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [TestCase(false)]
    [TestCase(true)]
    public async Task CommitPublishesCompleteContentsAndClosesStream(bool overwrite)
    {
        string path = Path.Combine(_directory, "output.txt");
        if (overwrite)
        {
            await File.WriteAllTextAsync(path, "original");
        }

        await using (IFileWriteSession session = new LocalFileWriteSession(_directory, "output.txt", overwrite))
        {
            await session.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("replacement"));
            if (overwrite)
            {
                Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("original"));
            }
            else
            {
                Assert.That(File.Exists(path), Is.False);
            }

            Assert.That(await session.CommitAsync(), Is.EqualTo(path));
            Assert.That(session.OutputStream.CanWrite, Is.False);
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("replacement"));
        }

        Assert.That(Directory.GetFiles(_directory), Is.EqualTo((string[])[path]));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AbandonedWritePreservesDestinationAndRemovesTemporaryFile(bool fail)
    {
        string path = Path.Combine(_directory, "output.txt");
        await File.WriteAllTextAsync(path, "original");

        async Task WriteAsync()
        {
            await using IFileWriteSession session = new LocalFileWriteSession(_directory, "output.txt", true);
            await session.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("partial"));
            if (fail)
            {
                throw new IOException("Encryption failed.");
            }
        }

        if (fail)
        {
            await Assert.ThatAsync(WriteAsync, Throws.TypeOf<IOException>());
        }
        else
        {
            await WriteAsync();
        }

        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("original"));
        Assert.That(Directory.GetFiles(_directory), Is.EqualTo((string[])[path]));
    }

    [Test]
    public async Task FailedCommitPreservesDestinationAndRemovesTemporaryFile()
    {
        string path = Path.Combine(_directory, "output.txt");
        await File.WriteAllTextAsync(path, "original");

        await using (IFileWriteSession session = new LocalFileWriteSession(_directory, "output.txt", false))
        {
            await session.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("replacement"));
            await Assert.ThatAsync(async () => await session.CommitAsync(), Throws.TypeOf<IOException>());
        }

        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("original"));
        Assert.That(Directory.GetFiles(_directory), Is.EqualTo((string[])[path]));
    }
}
