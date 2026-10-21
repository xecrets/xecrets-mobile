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

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

/// <summary>
/// What happens once a copy has been saved with Save as.
/// </summary>
[TestFixture]
public sealed class HandOverTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory().FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public async Task EncryptedCopySavedReturnsHomeAndRemovesTheCopy()
    {
        TestApp app = new();
        app.Flow.Begin(FlowOrigin.Home);
        string path = CreateFile(Path.Combine(_directory, "result"), "a.txt.axx");
        EncryptResultPageModel page = new(null!, app.Operations, app.Recent, app.Transient, app.Flow, app.CrashLog, app.UserInterface);
        page.Initialize(new EncryptionPreparationResult(path, "a.txt.axx", "application/octet-stream", 4, false));
        app.Access.PickedFiles.Enqueue("a.txt.axx");

        await page.SaveAsCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.Home]));
        Assert.That(File.Exists(path), Is.False);
    }

    [Test]
    public async Task EncryptedCopyNotSavedStays()
    {
        TestApp app = new();
        app.Flow.Begin(FlowOrigin.Home);
        string path = CreateFile(Path.Combine(_directory, "result"), "a.txt.axx");
        EncryptResultPageModel page = new(null!, app.Operations, app.Recent, app.Transient, app.Flow, app.CrashLog, app.UserInterface);
        page.Initialize(new EncryptionPreparationResult(path, "a.txt.axx", "application/octet-stream", 4, false));
        app.Access.PickedFiles.Enqueue(null);

        await page.SaveAsCommand.ExecuteAsync(null);

        Assert.That(app.UserInterface.Destinations, Is.Empty);
        Assert.That(File.Exists(path), Is.True);
    }

    [TestCase(FlowOrigin.Home, true)]
    [TestCase(FlowOrigin.ReceivedFile, true)]
    [TestCase(FlowOrigin.MyFolders, false)]
    [TestCase(FlowOrigin.RecentFiles, false)]
    public async Task DecryptedCopySavedReturnsHomeOnlyWhenStartedThere(FlowOrigin origin, bool returnsHome)
    {
        TestApp app = new();
        app.Flow.Begin(origin);
        string path = CreateFile(_directory, "a.txt");
        app.Preview.Current.SetText(new DecryptedFileInfo(path, "a.txt", "text/plain", 4, PreviewKind.Text), "text");
        PreviewPageModel page = new(
            app.Preview,
            null!,
            null!,
            app.FolderAccess,
            app.Operations,
            app.Recent,
            null!,
            app.Flow,
            app.CrashLog,
            app.UserInterface);
        app.Access.PickedFiles.Enqueue("a.txt");

        await page.SaveAsCommand.ExecuteAsync(null);

        List<AppDestination> expected = returnsHome ? [AppDestination.Home] : [];
        Assert.That(app.UserInterface.Destinations, Is.EqualTo(expected));
        Assert.That(app.Preview.Current.IsReady, Is.Not.EqualTo(returnsHome));
    }

    private static string CreateFile(string directory, string name)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, "text");
        return path;
    }
}
