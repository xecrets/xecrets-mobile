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
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class InterruptedSaveServiceTests
{
    [Test]
    public async Task PartlySavedFileIsRemovedWhileTheSourceIsThere()
    {
        using TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddFile("docs/a.txt.axx", "ENC:a.t");
        app.SavesInProgress.Current = Save("a.txt.axx", "docs/a.txt");

        await app.InterruptedSaves.RecoverAsync();

        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/a.txt"]));
        Assert.That(
            app.UserInterface.Messages,
            Is.EqualTo([string.Format(MobileTexts.DialogTextInterruptedSaveFormat, "a.txt.axx")]));
        Assert.That(app.SavesInProgress.Current, Is.Null);
    }

    [Test]
    public async Task FileIsLeftWhenTheSourceIsGone()
    {
        using TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt.axx", "ENC:a.t");
        app.SavesInProgress.Current = Save("a.txt.axx", "docs/a.txt");

        await app.InterruptedSaves.RecoverAsync();

        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/a.txt.axx"]));
        Assert.That(
            app.UserInterface.Messages,
            Is.EqualTo([string.Format(MobileTexts.DialogTextInterruptedSaveFormat, "a.txt.axx")]));
        Assert.That(app.SavesInProgress.Current, Is.Null);
    }

    [Test]
    public async Task SourceOfEditsGetsItsNameBackInPlaceOfThePartlySavedFile()
    {
        using TestApp app = new();
        app.AddFolder("docs");
        string backupId = $"docs/{"a.txt.axx".ToBackupName()}";
        app.AddFile(backupId, "ENC:a.txt\noriginal");
        app.AddFile("docs/a.txt.axx", "ENC:a.t");
        app.SavesInProgress.Current = Save("a.txt.axx", backupId) with { RestoresSource = true };

        await app.InterruptedSaves.RecoverAsync();

        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/a.txt.axx"]));
        Assert.That(app.Access.Files["docs/a.txt.axx"], Is.EqualTo("ENC:a.txt\noriginal"u8.ToArray()));
        Assert.That(
            app.UserInterface.Messages,
            Is.EqualTo([string.Format(MobileTexts.DialogTextInterruptedSaveFormat, "a.txt.axx")]));
    }

    [Test]
    public async Task SaveThatHadNotBegunWritingIsOnlyForgotten()
    {
        using TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.SavesInProgress.Current = Save("a.txt.axx", "docs/a.txt");

        await app.InterruptedSaves.RecoverAsync();

        Assert.That(app.Access.Files.Keys, Is.EqualTo(["docs/a.txt"]));
        Assert.That(app.UserInterface.Messages, Is.Empty);
        Assert.That(app.SavesInProgress.Current, Is.Null);
    }

    [Test]
    public async Task SaveOfAnotherProfileIsLeftForIt()
    {
        using TestApp app = new();
        app.AddFolder("docs");
        app.AddFile("docs/a.txt");
        app.AddFile("docs/a.txt.axx", "ENC:a.t");
        SaveInProgress save = Save("a.txt.axx", "docs/a.txt") with { UserId = "other" };
        app.SavesInProgress.Current = save;

        await app.InterruptedSaves.RecoverAsync();

        Assert.That(app.Access.Files.Keys, Is.EquivalentTo(["docs/a.txt", "docs/a.txt.axx"]));
        Assert.That(app.UserInterface.Messages, Is.Empty);
        Assert.That(app.SavesInProgress.Current, Is.EqualTo(save));
    }

    private static SaveInProgress Save(string name, string sourceId) =>
        new() { UserId = "me", FolderId = "docs", Name = name, SourceId = sourceId };
}
