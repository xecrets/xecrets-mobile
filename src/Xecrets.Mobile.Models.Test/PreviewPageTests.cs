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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

/// <summary>
/// How a decrypted text file is viewed and whether it can be edited, depending on its size.
/// </summary>
[TestFixture]
public sealed class PreviewPageTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory().FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [TestCase(true)]
    [TestCase(false)]
    public async Task TextAtOrUnderTheSizeCapIsViewedInTheApp(bool hasExternalViewer)
    {
        using TestApp app = new();
        TestFileService fileService = new(hasExternalViewer);
        PreviewPageModel page = await CreatePageAsync(app, fileService, PreviewInfoFactory.MaxInlineTextSizeBytes);

        await page.ViewCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.View]));
        Assert.That(fileService.Viewed, Is.Empty);
    }

    [Test]
    public async Task TextOverTheSizeCapIsViewedExternally()
    {
        using TestApp app = new();
        TestFileService fileService = new(hasExternalViewer: true);
        PreviewPageModel page = await CreatePageAsync(app, fileService, PreviewInfoFactory.MaxInlineTextSizeBytes + 1);

        await page.ViewCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.Destinations, Is.Empty);
        Assert.That(fileService.Viewed, Is.EqualTo(["a.txt"]));
    }

    [Test]
    public async Task TextOverTheSizeCapCannotBeViewedWithoutAnExternalViewer()
    {
        using TestApp app = new();
        PreviewPageModel page = await CreatePageAsync(app, new TestFileService(hasExternalViewer: false), PreviewInfoFactory.MaxInlineTextSizeBytes + 1);

        Assert.That(page.ViewCommand.CanExecute(null), Is.False);
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    public async Task TextCanBeEditedOnlyAtOrUnderTheSizeCap(long overTheCap, bool canEdit)
    {
        using TestApp app = new();
        PreviewPageModel page = await CreatePageAsync(app, new TestFileService(hasExternalViewer: true), PreviewInfoFactory.MaxInlineTextSizeBytes + overTheCap);

        Assert.That(page.EditCommand.CanExecute(null), Is.EqualTo(canEdit));
    }

    [Test]
    public async Task SavedCopyGetsTheTimesOfTheOriginal()
    {
        using TestApp app = new();
        PreviewPageModel page = await CreatePageAsync(app, new TestFileService(hasExternalViewer: true), 4);
        FileTimes original = new(
            new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            new DateTime(2021, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            new DateTime(2022, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        app.Preview.Current.SetText(
            app.Preview.Current.File with { OriginalTimes = original },
            Encoding.UTF8.GetBytes(app.Preview.Current.Text),
            EncryptedWithParameters.Empty);
        app.Access.PickedFiles.Enqueue("docs/a.txt");

        await page.SaveAsCommand.ExecuteAsync(null);

        Assert.That(app.Access.Times["docs/a.txt"], Is.EqualTo(original));
    }

    /// <summary>
    /// An edited file is saved over through one of My folders, so a file in none of them is not edited.
    /// </summary>
    [TestCase("docs/x.axx", true)]
    [TestCase("downloads/x.axx", false)]
    public async Task TextCanBeEditedOnlyInMyFolders(string path, bool canEdit)
    {
        using TestApp app = new();
        app.AddFolder("docs");
        app.AddFile(path);
        app.Flow.Begin(FlowOrigin.Home, new FileReference(path, "x.axx"));
        PreviewPageModel page = await CreatePageAsync(app, new TestFileService(hasExternalViewer: true), 4);

        Assert.That(page.EditCommand.CanExecute(null), Is.EqualTo(canEdit));
        Assert.That(app.UserInterface.Destinations, Is.Empty);
    }

    [Test]
    public async Task ReceivedTextCanBeEdited()
    {
        using TestApp app = new();
        app.Flow.Begin(FlowOrigin.ReceivedFile);
        PreviewPageModel page = await CreatePageAsync(app, new TestFileService(hasExternalViewer: true), 4);

        await page.EditCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.Edit]));
    }

    /// <summary>
    /// Text is kept in memory until it is handed over, and is then saved as it was decrypted.
    /// </summary>
    [Test]
    public async Task SavedCopyOfTextIsAsItWasDecrypted()
    {
        using TestApp app = new();
        PreviewPageModel page = await CreatePageAsync(app, new TestFileService(hasExternalViewer: true), 4);
        byte[] decrypted = [.. Encoding.Unicode.Preamble, .. Encoding.Unicode.GetBytes("Räksmörgås")];
        app.Preview.Current.SetText(app.Preview.Current.File, [.. decrypted], EncryptedWithParameters.Empty);
        app.Access.PickedFiles.Enqueue("docs/a.txt");

        Assert.That(app.Transient.RemainingFiles(), Is.Empty);

        await page.SaveAsCommand.ExecuteAsync(null);

        Assert.That(app.Access.Files["docs/a.txt"], Is.EqualTo(decrypted));
    }

    /// <summary>
    /// A file that is not handed off to another app is not needed once the preview is closed.
    /// </summary>
    [Test]
    public async Task FileShownIsRemovedWhenClosed()
    {
        using TestApp app = new();
        PreviewPageModel page = await CreatePageAsync(app, new TestFileService(hasExternalViewer: true), PreviewInfoFactory.MaxInlineTextSizeBytes + 1);

        await page.CloseCommand.ExecuteAsync(null);

        Assert.That(app.Transient.RemainingFiles(), Is.Empty);
    }

    [Test]
    public async Task FileShownIsRemovedWhenAnotherIsShown()
    {
        using TestApp app = new();
        await CreatePageAsync(app, new TestFileService(hasExternalViewer: true), PreviewInfoFactory.MaxInlineTextSizeBytes + 1);

        app.Preview.Current.SetText(
            new DecryptedFileInfo("", "b.txt", "text/plain", 4, PreviewKind.Text, FileTimes.UtcNow),
            "text"u8.ToArray(),
            EncryptedWithParameters.Empty);

        Assert.That(app.Transient.RemainingFiles(), Is.Empty);
    }

    /// <summary>
    /// The app the file was handed off to may still read it once the preview is closed, unless it was done with it
    /// when the handing off was.
    /// </summary>
    [TestCase(nameof(PreviewPageModel.SendToCommand), true)]
    [TestCase(nameof(PreviewPageModel.SendToCommand), false)]
    [TestCase(nameof(PreviewPageModel.ViewCommand), true)]
    [TestCase(nameof(PreviewPageModel.ViewCommand), false)]
    [TestCase(nameof(PreviewPageModel.OpenInCommand), true)]
    public async Task FileHandedOffIsKeptWhenClosedOnlyWhileTheAppMayReadIt(string command, bool mayStillRead)
    {
        using TestApp app = new();
        PreviewPageModel page = await CreatePageAsync(app, new TestFileService(hasExternalViewer: true, mayStillRead), PreviewInfoFactory.MaxInlineTextSizeBytes + 1);
        string path = app.Preview.Current.File.FilePath;

        await (command switch
        {
            nameof(PreviewPageModel.SendToCommand) => page.SendToCommand.ExecuteAsync(null),
            nameof(PreviewPageModel.ViewCommand) => page.ViewCommand.ExecuteAsync(null),
            nameof(PreviewPageModel.OpenInCommand) => page.OpenInCommand.ExecuteAsync(null),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        });
        await page.CloseCommand.ExecuteAsync(null);

        Assert.That(File.Exists(path), Is.EqualTo(mayStillRead));
    }

    private async Task<PreviewPageModel> CreatePageAsync(TestApp app, TestFileService fileService, long size)
    {
        string path = Path.Combine(_directory, "a.txt");
        File.WriteAllText(path, new string('a', (int)size), Encoding.ASCII);
        bool isText = size <= PreviewInfoFactory.MaxInlineTextSizeBytes;
        ITransientFile? decryptedFile = isText ? null : app.Transient.CreateHandoffFile("a.txt");
        if (decryptedFile is not null)
        {
            File.Copy(path, decryptedFile.FilePath);
        }

        DecryptedFileInfo file = PreviewInfoFactory.CreateInfo(
            decryptedFile?.FilePath ?? "",
            "a.txt",
            FileCategory.Text,
            size,
            FileTimes.UtcNow);
        if (file.Kind == PreviewKind.Text)
        {
            app.Preview.Current.SetText(file, File.ReadAllBytes(path), EncryptedWithParameters.Empty);
        }
        else
        {
            app.Preview.Current.SetFile(file, EncryptedWithParameters.Empty, decryptedFile!);
        }

        // The preview service itself, showing what the test set up, so that text is handed over as it really is.
        PreviewService preview = new(
            app.Core,
            app.Profile,
            app.Transient,
            app.Preview.Current,
            new DecryptionPasswordRequestState(),
            app.Running,
            app.CrashLog);
        PreviewPageModel page = new(
            preview,
            new DecryptedFileViewer(fileService),
            fileService,
            app.FolderAccess,
            app.Operations,
            app.Recent,
            new FakeCrashTestService(),
            app.Flow,
            app.CrashLog,
            app.UserInterface);
        await page.InitializeCommand.ExecuteAsync(null);
        return page;
    }

    // Whether the app a file is handed off to may still read it afterwards.
    private sealed class TestFileService(bool hasExternalViewer, bool mayStillRead = true) : IFileService
    {
        public List<string> Viewed { get; } = [];
        public string PlatformId => "test";
        public string AppDataDirectory => throw new NotSupportedException();
        public string CacheDirectory => throw new NotSupportedException();
        public Task<bool> OpenInAsync(string filePath, string displayName) => Task.FromResult(mayStillRead);
        public Task<bool> SendToAsync(string filePath, string displayName, string contentType) => Task.FromResult(mayStillRead);
        public Task<bool> CanViewFileAsync(DecryptedFileInfo file) => Task.FromResult(hasExternalViewer);

        public Task<bool> ViewFileAsync(DecryptedFileInfo file)
        {
            Viewed.Add(file.OriginalFileName);
            return Task.FromResult(mayStillRead);
        }

        public bool IsSelfHandoffReference(string reference) => false;
    }
}
