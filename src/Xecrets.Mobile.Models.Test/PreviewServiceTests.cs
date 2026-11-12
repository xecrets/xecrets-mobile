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

using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class PreviewServiceTests
{
    private string _directory = "";

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory().FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    /// <summary>
    /// The decrypted file keeps the times of the original, so that they can be restored when it is saved.
    /// The fake decryption tells the Unix epoch for all of them.
    /// </summary>
    [Test]
    public async Task DecryptedFileKeepsTheTimesOfTheOriginal()
    {
        string encryptedPath = Path.Combine(_directory, "x.axx");
        await File.WriteAllBytesAsync(encryptedPath, Encoding.UTF8.GetBytes("ENC:a.txt\ntext"));
        using FakeTransientFileService transient = new();
        PreviewState state = new();
        PreviewService preview = new(
            new FakeCoreServices(),
            new FakeProfileService(),
            transient,
            state,
            new DecryptionPasswordRequestState(),
            new RunningOperation(new ScriptedUserInterface()),
            new FakeCrashLogService());

        bool prepared = await preview.PrepareAsync(new LocalReadableFile(encryptedPath, "x.axx"));

        Assert.That(prepared, Is.True);
        Assert.That(state.File.OriginalTimes,
            Is.EqualTo(new FileTimes(DateTime.UnixEpoch, DateTime.UnixEpoch, DateTime.UnixEpoch)));
    }

    /// <summary>
    /// Text that the app shows itself is kept in memory, so that all there is in the cache is the encrypted file.
    /// </summary>
    [Test]
    public async Task TextIsKeptInMemoryAndIsNotWrittenToAFile()
    {
        using FakeTransientFileService transient = new();
        PreviewState state = new();
        PreviewService preview = CreatePreview(transient, state);

        bool prepared = await preview.PrepareAsync(await CreateEncryptedAsync("a.txt", "text"));

        Assert.That(prepared, Is.True);
        Assert.That(state.File.Kind, Is.EqualTo(PreviewKind.Text));
        Assert.That(state.Text, Is.EqualTo("text"));
        Assert.That(state.File.FileSize, Is.EqualTo(4));
        Assert.That(state.File.FilePath, Is.Empty);
        Assert.That(DecryptedFiles(transient), Is.Empty);
    }

    [Test]
    public async Task ReceivedTextIsKeptInMemoryAndIsNotWrittenToAFile()
    {
        using FakeTransientFileService transient = new();
        PreviewState state = new();
        PreviewService preview = CreatePreview(transient, state);
        string encryptedPath = Path.Combine(_directory, "x.axx");
        await File.WriteAllBytesAsync(encryptedPath, Encoding.UTF8.GetBytes("ENC:a.txt\ntext"));

        PreviewPreparationStatus status = await preview.PrepareImportedAsync(encryptedPath);

        Assert.That(status, Is.EqualTo(PreviewPreparationStatus.Prepared));
        Assert.That(state.File.Kind, Is.EqualTo(PreviewKind.Text));
        Assert.That(state.Text, Is.EqualTo("text"));
        Assert.That(DecryptedFiles(transient), Is.Empty);
    }

    [Test]
    public async Task TextOverTheSizeCapIsWrittenToAFileInFull()
    {
        using FakeTransientFileService transient = new();
        PreviewState state = new();
        PreviewService preview = CreatePreview(transient, state);
        string text = new('a', (int)PreviewInfoFactory.MaxInlineTextSizeBytes + 1);

        await preview.PrepareAsync(await CreateEncryptedAsync("a.txt", text));

        Assert.That(state.File.Kind, Is.EqualTo(PreviewKind.External));
        Assert.That(state.Text, Is.Empty);
        Assert.That(DecryptedFiles(transient), Is.EqualTo([state.File.FilePath]));
        Assert.That(new FileInfo(state.File.FilePath).Length, Is.EqualTo(text.Length));
        Assert.That(state.File.FileSize, Is.EqualTo(text.Length));
    }

    [Test]
    public async Task WhatIsNotTextIsWrittenToAFile()
    {
        using FakeTransientFileService transient = new();
        PreviewState state = new();
        PreviewService preview = CreatePreview(transient, state);

        await preview.PrepareAsync(await CreateEncryptedAsync("a.pdf", "%PDF-1.4"));

        Assert.That(state.File.Kind, Is.EqualTo(PreviewKind.External));
        Assert.That(await File.ReadAllTextAsync(state.File.FilePath), Is.EqualTo("%PDF-1.4"));
    }

    /// <summary>
    /// The text is handed over as it was decrypted, in whatever encoding it has, and is written only once.
    /// </summary>
    [Test]
    public async Task TextIsWrittenToAFileAsItWasDecryptedWhenHandedOver()
    {
        using FakeTransientFileService transient = new();
        PreviewState state = new();
        PreviewService preview = CreatePreview(transient, state);
        byte[] decrypted = [.. Encoding.Unicode.Preamble, .. Encoding.Unicode.GetBytes("Räksmörgås")];
        state.SetText(
            new DecryptedFileInfo("", "a.txt", "text/plain", decrypted.Length, PreviewKind.Text, FileTimes.UtcNow),
            [.. decrypted],
            EncryptedWithParameters.Empty);

        DecryptedFileInfo file = await preview.MaterializeAsync();
        DecryptedFileInfo again = await preview.MaterializeAsync();

        Assert.That(state.Text, Is.EqualTo("Räksmörgås"));
        Assert.That(await File.ReadAllBytesAsync(file.FilePath), Is.EqualTo(decrypted));
        Assert.That(file.OriginalFileName, Is.EqualTo("a.txt"));
        Assert.That(again.FilePath, Is.EqualTo(file.FilePath));
        Assert.That(state.File.FilePath, Is.EqualTo(file.FilePath));
        Assert.That(DecryptedFiles(transient), Is.EqualTo([file.FilePath]));
    }

    [Test]
    public async Task SavedTextIsWrittenToAFileAsUtf8WhenHandedOver()
    {
        using FakeTransientFileService transient = new();
        PreviewState state = new();
        PreviewService preview = CreatePreview(transient, state);
        state.SetText(
            new DecryptedFileInfo("", "a.txt", "text/plain", 0, PreviewKind.Text, FileTimes.UtcNow),
            [.. Encoding.Unicode.Preamble, .. Encoding.Unicode.GetBytes("text")],
            EncryptedWithParameters.Empty);

        byte[] utf8 = new UTF8Encoding(false).GetBytes("Räksmörgås");
        state.UpdateText([.. utf8]);
        DecryptedFileInfo file = await preview.MaterializeAsync();

        Assert.That(await File.ReadAllBytesAsync(file.FilePath), Is.EqualTo(utf8));
        Assert.That(file.FileSize, Is.EqualTo(utf8.Length));
    }

    [Test]
    public void TextIsClearedWhenNoLongerShown()
    {
        PreviewState state = new();
        byte[] plaintext = "text"u8.ToArray();
        state.SetText(
            new DecryptedFileInfo("", "a.txt", "text/plain", 4, PreviewKind.Text, FileTimes.UtcNow),
            plaintext,
            EncryptedWithParameters.Empty);

        state.Clear();

        Assert.That(state.Text, Is.Empty);
        Assert.That(state.IsReady, Is.False);
    }

    private static PreviewService CreatePreview(FakeTransientFileService transient, PreviewState state) =>
        new(
            new FakeCoreServices(),
            new FakeProfileService(),
            transient,
            state,
            new DecryptionPasswordRequestState(),
            new RunningOperation(new ScriptedUserInterface()),
            new FakeCrashLogService());

    private async Task<LocalReadableFile> CreateEncryptedAsync(string originalName, string contents)
    {
        string encryptedPath = Path.Combine(_directory, "x.axx");
        await File.WriteAllBytesAsync(encryptedPath, Encoding.UTF8.GetBytes($"ENC:{originalName}\n{contents}"));
        return new LocalReadableFile(encryptedPath, "x.axx");
    }

    // The files left in the cache that are not the encrypted file itself.
    private static List<string> DecryptedFiles(FakeTransientFileService transient) =>
        [.. transient.RemainingFiles().Where(path => !path.EndsWith(".axx", StringComparison.Ordinal))];
}
