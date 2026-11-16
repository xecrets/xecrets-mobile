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

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class CacheBrowserPageTests
{
    private string _directory = null!;

    private string CacheRoot => Path.Combine(_directory, "cache");

    [SetUp]
    public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), $"xecrets-mobile-test-{Guid.NewGuid():N}");

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Test]
    public async Task FoldersAreListedBeforeFilesByName()
    {
        CreateFile("b.txt");
        CreateFile("A.txt");
        Directory.CreateDirectory(Path.Combine(CacheRoot, "z"));
        Directory.CreateDirectory(Path.Combine(CacheRoot, "XecretsEz"));
        CacheBrowserPageModel page = CreatePage();

        await page.LoadCommand.ExecuteAsync(null);

        Assert.That(page.Entries.Select(entry => entry.DisplayName), Is.EqualTo(["XecretsEz", "z", "A.txt", "b.txt"]));
        Assert.That(page.Entries.Select(entry => entry.IsDirectory), Is.EqualTo([true, true, false, false]));
        Assert.That(page.CurrentPath, Is.EqualTo("Cache"));
    }

    [Test]
    public async Task AFolderIsOpenedAndLeftWithUp()
    {
        CreateFile(Path.Combine("XecretsEz", "inner.txt"));
        CacheBrowserPageModel page = CreatePage();
        await page.LoadCommand.ExecuteAsync(null);

        await page.OpenCommand.ExecuteAsync(page.Entries.Single());

        Assert.That(page.Entries.Select(entry => entry.DisplayName), Is.EqualTo(["inner.txt"]));
        Assert.That(page.CurrentPath, Is.EqualTo(Path.Combine("Cache", "XecretsEz")));

        await page.UpCommand.ExecuteAsync(null);

        Assert.That(page.Entries.Select(entry => entry.DisplayName), Is.EqualTo(["XecretsEz"]));
    }

    [Test]
    public async Task TheSystemFolderIsListedButCannotBeOpened()
    {
        CreateFile(Path.Combine("oat_primary", "base.art"));
        Directory.CreateDirectory(Path.Combine(CacheRoot, "XecretsEz"));
        CacheBrowserPageModel page = CreatePage();

        await page.LoadCommand.ExecuteAsync(null);

        CacheBrowserEntry system = page.Entries.Single(entry => entry.DisplayName == "oat_primary");
        CacheBrowserEntry other = page.Entries.Single(entry => entry.DisplayName == "XecretsEz");
        Assert.That(system.IsEnabled, Is.False);
        Assert.That(page.OpenCommand.CanExecute(system), Is.False);
        Assert.That(other.IsEnabled, Is.True);
        Assert.That(page.OpenCommand.CanExecute(other), Is.True);
    }

    [Test]
    public async Task ClearDeletesAllButTheSystemFolder()
    {
        CreateFile(Path.Combine("oat_primary", "base.art"));
        CreateFile(Path.Combine("image_manager_disk_cache", "image.0"));
        CreateFile(Path.Combine("XecretsEz", "file.txt"));
        CreateFile("loose.txt");
        CacheBrowserPageModel page = CreatePage();
        await page.LoadCommand.ExecuteAsync(null);

        await page.ClearCommand.ExecuteAsync(null);

        Assert.That(page.StatusText, Is.Empty);
        Assert.That(page.Entries.Select(entry => entry.DisplayName), Is.EqualTo(["oat_primary"]));
        Assert.That(File.Exists(Path.Combine(CacheRoot, "oat_primary", "base.art")), Is.True);
    }

    private void CreateFile(string relativePath)
    {
        string path = Path.Combine(CacheRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "content");
    }

    private CacheBrowserPageModel CreatePage()
    {
        Directory.CreateDirectory(CacheRoot);
        return new CacheBrowserPageModel(
            new TestFileService(Path.Combine(CacheRoot, "XecretsEz")),
            new FakeCrashLogService(),
            new FakeEzLogging(),
            new ScriptedUserInterface());
    }

    private sealed class TestFileService(string cacheDirectory) : IFileService
    {
        public string PlatformId => "test";
        public string AppDataDirectory => throw new NotSupportedException();
        public string CacheDirectory => cacheDirectory;
        public Task<bool> OpenInAsync(string filePath, string displayName) => throw new NotSupportedException();
        public Task<bool> SendToAsync(string filePath, string displayName, string contentType) => throw new NotSupportedException();
        public Task<bool> CanViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public Task<bool> ViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public bool IsSelfHandoffReference(string reference) => false;
    }
}
