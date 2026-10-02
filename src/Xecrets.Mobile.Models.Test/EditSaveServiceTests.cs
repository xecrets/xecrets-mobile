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

using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class EditSaveServiceTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory().FullName;

    // The decrypted file is left read-only, which keeps it from being deleted on Windows.
    [TearDown]
    public void TearDown()
    {
        foreach (string file in Directory.EnumerateFiles(_directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public async Task SaveEncryptsTheEditedTextOverTheSourceAndListsItInPlaceOfAnyEntryForTheFile()
    {
        TestApp app = CreateEditing("text", usesFolderIds: true);
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.AddRecent("docs/x.axx", RecentFileOperation.InPlace, "x.axx");
        FileReference source = new("grant:docs|docs/x.axx", "x.axx");
        app.Flow.Begin(FlowOrigin.Navigated, WorkFolderOperation.Decrypt, source);

        await app.EditSave.SaveAsync("edited");

        Assert.That(Encoding.UTF8.GetString(app.Access.Files["docs/x.axx"]), Is.EqualTo("ENC:a.txt\nedited"));
        Assert.That(app.Preview.Current.Text, Is.EqualTo("edited"));
        Assert.That(app.Flow.Source, Is.EqualTo(source));
        Assert.That(app.Store.Files.Select(file => (file.Id, file.Operation)),
            Is.EqualTo([("grant:docs|docs/x.axx", RecentFileOperation.Edit)]));
    }

    [Test]
    public async Task CopySavedInMyFoldersBecomesTheSource()
    {
        TestApp app = CreateEditing("text");
        app.AddFolder("docs");
        app.Access.PickedFiles.Enqueue("docs/copy.axx");

        await app.EditSave.SaveCopyAsync("edited");

        Assert.That(Encoding.UTF8.GetString(app.Access.Files["docs/copy.axx"]), Is.EqualTo("ENC:a.txt\nedited"));
        Assert.That(app.Flow.Source, Is.EqualTo(new FileReference("docs/copy.axx", "copy.axx")));
        Assert.That(app.Store.Files.Select(file => (file.Id, file.Operation)),
            Is.EqualTo([("docs/copy.axx", RecentFileOperation.Edit)]));
    }

    [Test]
    public async Task CopySavedOutsideMyFoldersLeavesTheSourceUnchanged()
    {
        TestApp app = CreateEditing("text");
        app.AddFolder("docs");
        app.Access.PickedFiles.Enqueue("downloads/copy.axx");

        await app.EditSave.SaveCopyAsync("edited");

        Assert.That(Encoding.UTF8.GetString(app.Access.Files["downloads/copy.axx"]), Is.EqualTo("ENC:a.txt\nedited"));
        Assert.That(app.Flow.Source, Is.Null);
        Assert.That(app.Store.Files, Is.Empty);
    }

    private TestApp CreateEditing(string text, bool usesFolderIds = false)
    {
        string path = Path.Combine(_directory, "a.txt");
        File.WriteAllText(path, text);
        TestApp app = new(usesFolderIds);
        app.Preview.Current.SetText(new DecryptedFileInfo(path, "a.txt", "text/plain", text.Length, PreviewKind.Text), text);
        return app;
    }
}
