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
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class EditPageTests
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
    public async Task LeavingWithoutChangesDoesNotAsk()
    {
        TestApp app = CreateEditingSource();
        EditPageModel page = await OpenAsync(app);

        Assert.That(await page.CanLeaveAsync(), Is.True);
        Assert.That(app.UserInterface.Confirmations, Is.Empty);
    }

    [Test]
    public async Task LeavingWithChangesAsksAndStaysOnCancel()
    {
        TestApp app = CreateEditingSource();
        EditPageModel page = await OpenAsync(app);
        page.Text = "edited";
        app.UserInterface.SaveChangesAnswers.Enqueue(SaveChangesChoice.Cancel);

        Assert.That(await page.CanLeaveAsync(), Is.False);
        Assert.That(app.UserInterface.Confirmations, Is.EqualTo([MobileTexts.DialogTextSaveChanges]));
        Assert.That(SourceText(app), Is.EqualTo("ENC:a.txt\ntext"));
        Assert.That(page.HasUnsavedChanges, Is.True);
    }

    [Test]
    public async Task LeavingWithChangesDiscardsThem()
    {
        TestApp app = CreateEditingSource();
        EditPageModel page = await OpenAsync(app);
        page.Text = "edited";
        app.UserInterface.SaveChangesAnswers.Enqueue(SaveChangesChoice.Discard);

        Assert.That(await page.CanLeaveAsync(), Is.True);
        Assert.That(SourceText(app), Is.EqualTo("ENC:a.txt\ntext"));
    }

    [Test]
    public async Task LeavingWithChangesSavesThemEncrypted()
    {
        TestApp app = CreateEditingSource();
        EditPageModel page = await OpenAsync(app);
        page.Text = "edited";
        app.UserInterface.SaveChangesAnswers.Enqueue(SaveChangesChoice.Save);

        Assert.That(await page.CanLeaveAsync(), Is.True);
        Assert.That(SourceText(app), Is.EqualTo("ENC:a.txt\nedited"));
    }

    [Test]
    public async Task SaveEncryptsOverTheSourceAndKeepsEditing()
    {
        TestApp app = CreateEditingSource();
        EditPageModel page = await OpenAsync(app);
        page.Text = "edited";

        await page.SaveCommand.ExecuteAsync(null);

        Assert.That(SourceText(app), Is.EqualTo("ENC:a.txt\nedited"));
        Assert.That(page.HasUnsavedChanges, Is.False);
        Assert.That(await page.CanLeaveAsync(), Is.True);
        Assert.That(app.UserInterface.Confirmations, Is.Empty);
        Assert.That(app.UserInterface.TransientMessages, Is.EqualTo([MobileTexts.DialogTextFileEncrypted]));
    }

    [Test]
    public async Task SaveWithoutChangesDoesNothing()
    {
        TestApp app = CreateEditing();
        EditPageModel page = await OpenAsync(app);

        await page.SaveCommand.ExecuteAsync(null);

        // Saving a copy would fail here, since the picker has no answer queued.
        Assert.That(page.StatusText, Is.Empty);
        Assert.That(app.UserInterface.TransientMessages, Is.Empty);
    }

    [Test]
    public async Task ChoosingNotToSaveACopyStaysWithTheChanges()
    {
        TestApp app = CreateEditing();
        app.Access.PickedFiles.Enqueue(null);
        EditPageModel page = await OpenAsync(app);
        page.Text = "edited";
        app.UserInterface.SaveChangesAnswers.Enqueue(SaveChangesChoice.Save);

        Assert.That(await page.CanLeaveAsync(), Is.False);
        Assert.That(page.HasUnsavedChanges, Is.True);
    }

    [Test]
    public async Task SavingACopyLeavesNoUnsavedChanges()
    {
        TestApp app = CreateEditing();
        app.Access.PickedFiles.Enqueue("downloads/copy.axx");
        EditPageModel page = await OpenAsync(app);
        page.Text = "edited";

        await page.SaveCommand.ExecuteAsync(null);

        Assert.That(Encoding.UTF8.GetString(app.Access.Files["downloads/copy.axx"]), Is.EqualTo("ENC:a.txt\nedited"));
        Assert.That(page.HasUnsavedChanges, Is.False);
    }

    [Test]
    public async Task FileIsShownWithWhereTheUserIsAndItsTypeAndSize()
    {
        TestApp app = CreateEditingSource();

        EditPageModel page = await OpenAsync(app);

        Assert.That(page.Breadcrumb, Is.EqualTo(string.Join(
            MobileTexts.BreadcrumbSeparator,
            MobileTexts.BreadcrumbHome,
            MobileTexts.BreadcrumbPreview,
            MobileTexts.BreadcrumbEdit)));
        Assert.That(page.FileNameText, Is.EqualTo("a.txt"));
        Assert.That(page.MetadataText, Is.EqualTo($"text/plain {string.Format(MobileTexts.FileSizeFormat, 4)}"));
    }

    [Test]
    public async Task SizeIsUpdatedWhenSaved()
    {
        TestApp app = CreateEditingSource();
        EditPageModel page = await OpenAsync(app);
        page.Text = "edited";

        await page.SaveCommand.ExecuteAsync(null);

        Assert.That(page.MetadataText, Is.EqualTo($"text/plain {string.Format(MobileTexts.FileSizeFormat, 6)}"));
    }

    private static async Task<EditPageModel> OpenAsync(TestApp app)
    {
        EditPageModel page = app.CreateEditPage();
        await page.InitializeCommand.ExecuteAsync(null);
        return page;
    }

    private static string SourceText(TestApp app) => Encoding.UTF8.GetString(app.Access.Files["docs/x.axx"]);

    private TestApp CreateEditingSource()
    {
        TestApp app = CreateEditing();
        app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Flow.Begin(FlowOrigin.Home, new FileReference("docs/x.axx", "x.axx"));
        return app;
    }

    private TestApp CreateEditing()
    {
        string path = Path.Combine(_directory, "a.txt");
        File.WriteAllText(path, "text");
        TestApp app = new();
        app.Preview.Current.SetText(new DecryptedFileInfo(path, "a.txt", "text/plain", 4, PreviewKind.Text), "text");
        return app;
    }
}
