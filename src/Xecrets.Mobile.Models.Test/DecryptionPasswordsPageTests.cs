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
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class DecryptionPasswordsPageTests
{
    [Test]
    public void KeptPasswordsAreListed()
    {
        using TestApp app = new();
        app.Profile.ExtraPasswords.Add(new PasswordUsage { Password = "first", });
        app.Profile.ExtraPasswords.Add(new PasswordUsage { Password = "second", });
        EnterPasswordPageModel page = app.CreateEnterPasswordPage();

        page.Initialize(DecryptionPasswordsIntent.Manage);
        page.Load();

        Assert.That(page.Passwords, Is.EqualTo(["first", "second"]));
        Assert.That(page.NoPasswordsText, Is.Empty);
        Assert.That(page.IsAdding, Is.False);
    }

    [Test]
    public void WithoutPasswordsThePageInformationIsShown()
    {
        using TestApp app = new();
        EnterPasswordPageModel page = app.CreateEnterPasswordPage();

        page.Initialize(DecryptionPasswordsIntent.Manage);
        page.Load();

        Assert.That(page.Passwords, Is.Empty);
        Assert.That(page.NoPasswordsText, Is.EqualTo(MobileTexts.DecryptionPasswordsDescription));
    }

    [Test]
    public void OpenedToAddAPasswordShowsTheEntry()
    {
        using TestApp app = new();
        EnterPasswordPageModel page = app.CreateEnterPasswordPage();

        page.Initialize(DecryptionPasswordsIntent.Add);

        Assert.That(page.IsAdding, Is.True);
    }

    [Test]
    public async Task AddedPasswordIsKeptAndListed()
    {
        using TestApp app = new();
        EnterPasswordPageModel page = app.CreateEnterPasswordPage();
        page.Initialize(DecryptionPasswordsIntent.Manage);
        page.Load();

        page.AddCommand.Execute(null);
        Assert.That(page.IsAdding, Is.True);
        page.Password = "added";
        await page.SubmitCommand.ExecuteAsync(null);

        Assert.That(app.Profile.ExtraPasswords.Select(extraPassword => extraPassword.Password), Is.EqualTo(["added"]));
        Assert.That(page.Passwords, Is.EqualTo(["added"]));
        Assert.That(page.NoPasswordsText, Is.Empty);
        Assert.That(page.Password, Is.Empty);
        Assert.That(page.IsAdding, Is.False);
        Assert.That(app.UserInterface.BackCount, Is.Zero);
    }

    [Test]
    public async Task PasswordAddedFromTheMenuDoesNotDecryptAFileWaitingForAPassword()
    {
        using TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Access.PickedFiles.Enqueue("docs/x.axx");
        app.Core.Password = "unknown";
        await app.Operations.PickAndRunAsync(WorkFolderIntent.Auto, docs);
        EnterPasswordPageModel page = app.CreateEnterPasswordPage();
        page.Initialize(DecryptionPasswordsIntent.Manage);
        page.Load();
        Assert.That(page.ErrorText, Is.Empty);

        page.Password = "unknown";
        await page.SubmitCommand.ExecuteAsync(null);

        Assert.That(app.OperationService.HasPendingPasswordRequest, Is.True);
        Assert.That(
            app.Profile.ExtraPasswords.Select(extraPassword => extraPassword.Password),
            Is.EqualTo(["unknown"]));
        Assert.That(app.UserInterface.BackCount, Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task PasswordIsDeletedOnlyWhenConfirmed(bool confirmed)
    {
        using TestApp app = new();
        app.Profile.ExtraPasswords.Add(new PasswordUsage { Password = "first", });
        app.Profile.ExtraPasswords.Add(new PasswordUsage { Password = "second", });
        app.UserInterface.Answers.Enqueue(confirmed);
        EnterPasswordPageModel page = app.CreateEnterPasswordPage();
        page.Initialize(DecryptionPasswordsIntent.Manage);
        page.Load();

        await page.DeleteCommand.ExecuteAsync("first");

        string[] expected = confirmed ? ["second"] : ["first", "second"];
        Assert.That(app.UserInterface.Confirmations, Is.EqualTo([MobileTexts.MessageTextConfirmWipe]));
        Assert.That(app.UserInterface.ConfirmationButtons, Is.EqualTo([(MobileTexts.ButtonOk, MobileTexts.LabelCancel)]));
        Assert.That(app.Profile.ExtraPasswords.Select(extraPassword => extraPassword.Password), Is.EqualTo(expected));
        Assert.That(page.Passwords, Is.EqualTo(expected));
    }
}
