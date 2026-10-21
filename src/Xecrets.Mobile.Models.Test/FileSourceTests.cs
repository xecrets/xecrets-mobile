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
public sealed class FileSourceTests
{
    private string _directory = "";

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory().FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReadableFileOpensIndependentStreamsAndKeepsDisplayName(bool referenced)
    {
        string path = Path.Combine(_directory, "stored.txt");
        const string displayName = "display.txt";
        byte[] content = Encoding.UTF8.GetBytes("content");
        FakeFileAccess fileAccess = new(null!) { Files = { [path] = content } };
        await File.WriteAllBytesAsync(path, content);
        IReadableFile source = referenced
            ? new ReferencedReadableFile(fileAccess, new FileReference(path, displayName))
            : new LocalReadableFile(path, displayName);

        await using Stream first = await source.OpenReadAsync();
        await using Stream second = await source.OpenReadAsync();
        Assert.That(source.FileName, Is.EqualTo(displayName));
        Assert.That(await new StreamReader(first).ReadToEndAsync(), Is.EqualTo("content"));
        Assert.That(await new StreamReader(second).ReadToEndAsync(), Is.EqualTo("content"));
    }

    [TestCase("stream")]
    [TestCase("path")]
    [TestCase("text")]
    public async Task IncomingSourceCopiesOnlyWhenImportedAndPreservesMetadata(string kind)
    {
        string sourcePath = Path.Combine(_directory, "source.txt");
        string outputPath = Path.Combine(_directory, "imported.txt");
        await File.WriteAllTextAsync(sourcePath, "content");
        await using MemoryStream input = new(Encoding.UTF8.GetBytes("content"));
        ITransientFileService transient = new TestTransientFileService(outputPath);
        IIncomingFileSource source = kind switch
        {
            "stream" => new StreamIncomingFileSource(input, "display.txt", "text/plain", transient),
            "path" => new PathIncomingFileSource(sourcePath, "display.txt", "text/plain", transient),
            "text" => new TextIncomingFileSource("content", "display.txt", transient),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        Assert.That(File.Exists(outputPath), Is.False);
        IncomingFileInfo imported = await source.ImportAsync();

        Assert.That(imported, Is.EqualTo(new IncomingFileInfo(outputPath, "display.txt", "text/plain")));
        Assert.That(await File.ReadAllTextAsync(outputPath), Is.EqualTo("content"));
        Assert.That(input.CanRead, Is.True);
        await using FileStream exclusive = File.Open(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private sealed class TestTransientFileService(string incomingPath) : ITransientFileService
    {
        public string CreateIncomingPath(string originalFileName) => incomingPath;
        public string CreateHandoffPath(string originalFileName) => throw new NotSupportedException();
        public string CreateEncryptedInputPath(string originalFileName) => throw new NotSupportedException();
        public string CreateEncryptedOutputPath(string originalFileName) => throw new NotSupportedException();
        public Task WipeTransientFileAsync(string filePath) => throw new NotSupportedException();
        public Task RunExclusiveAsync(Func<Task> operation) => throw new NotSupportedException();
        public Task MaybeWipeTrackedFilesAsync() => throw new NotSupportedException();
    }
}
