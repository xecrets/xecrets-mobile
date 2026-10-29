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
        TestApp app = new();
        TestFileService fileService = new(hasExternalViewer);
        PreviewPageModel page = await CreatePageAsync(app, fileService, PreviewInfoFactory.MaxInlineTextSizeBytes);

        await page.ViewCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.View]));
        Assert.That(fileService.Viewed, Is.Empty);
    }

    [Test]
    public async Task TextOverTheSizeCapIsViewedExternally()
    {
        TestApp app = new();
        TestFileService fileService = new(hasExternalViewer: true);
        PreviewPageModel page = await CreatePageAsync(app, fileService, PreviewInfoFactory.MaxInlineTextSizeBytes + 1);

        await page.ViewCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.Destinations, Is.Empty);
        Assert.That(fileService.Viewed, Is.EqualTo(["a.txt"]));
    }

    [Test]
    public async Task TextOverTheSizeCapCannotBeViewedWithoutAnExternalViewer()
    {
        TestApp app = new();
        PreviewPageModel page = await CreatePageAsync(app, new TestFileService(hasExternalViewer: false), PreviewInfoFactory.MaxInlineTextSizeBytes + 1);

        Assert.That(page.ViewCommand.CanExecute(null), Is.False);
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    public async Task TextCanBeEditedOnlyAtOrUnderTheSizeCap(long overTheCap, bool canEdit)
    {
        TestApp app = new();
        PreviewPageModel page = await CreatePageAsync(app, new TestFileService(hasExternalViewer: true), PreviewInfoFactory.MaxInlineTextSizeBytes + overTheCap);

        Assert.That(page.EditCommand.CanExecute(null), Is.EqualTo(canEdit));
    }

    private async Task<PreviewPageModel> CreatePageAsync(TestApp app, TestFileService fileService, long size)
    {
        string path = Path.Combine(_directory, "a.txt");
        File.WriteAllText(path, new string('a', (int)size), Encoding.ASCII);
        DecryptedFileInfo file = PreviewInfoFactory.CreateInfo(path, "a.txt");
        if (file.Kind == PreviewKind.Text)
        {
            app.Preview.Current.SetText(file, File.ReadAllText(path));
        }
        else
        {
            app.Preview.Current.SetExternal(file);
        }

        PreviewPageModel page = new(
            app.Preview,
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

    private sealed class TestFileService(bool hasExternalViewer) : IFileService
    {
        public List<string> Viewed { get; } = [];
        public string PlatformId => "test";
        public string AppDataDirectory => throw new NotSupportedException();
        public string CacheDirectory => throw new NotSupportedException();
        public Task<bool> OpenInAsync(string filePath, string displayName) => throw new NotSupportedException();
        public Task SendToAsync(string filePath, string displayName, string contentType) => throw new NotSupportedException();
        public Task<bool> CanViewFileAsync(DecryptedFileInfo file) => Task.FromResult(hasExternalViewer);

        public Task ViewFileAsync(DecryptedFileInfo file)
        {
            Viewed.Add(file.DisplayName);
            return Task.CompletedTask;
        }

        public bool IsSelfHandoffReference(string reference) => false;
    }
}
