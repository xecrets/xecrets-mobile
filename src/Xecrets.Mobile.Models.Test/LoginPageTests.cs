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
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class LoginPageTests
{
    [Test]
    public async Task PasswordIsKeptWhileLeavingAfterSigningIn()
    {
        LoginPageModel page = CreateLoginPage(out ScriptedUserInterface userInterface);
        await page.InitializeCommand.ExecuteAsync(null);

        page.Password = "password";
        await page.LoginCommand.ExecuteAsync(null);

        Assert.That(userInterface.Destinations, Is.EqualTo([AppDestination.Home]));
        Assert.That(page.Password, Is.EqualTo("password"));
    }

    [Test]
    public async Task PasswordIsGoneWhenThePageAppearsAgain()
    {
        LoginPageModel page = CreateLoginPage(out _);
        await page.InitializeCommand.ExecuteAsync(null);
        page.Password = "password";
        await page.LoginCommand.ExecuteAsync(null);

        await page.InitializeCommand.ExecuteAsync(null);

        Assert.That(page.Password, Is.Empty);
    }

    [Test]
    public async Task WrongPasswordErrorIsGoneWhenThePageAppearsAgain()
    {
        LoginPageModel page = CreateLoginPage(out _);
        await page.InitializeCommand.ExecuteAsync(null);
        page.Password = "wrong";
        await page.LoginCommand.ExecuteAsync(null);
        Assert.That(page.ErrorText, Is.EqualTo(MobileTexts.DialogValidationWrongPassword));

        await page.InitializeCommand.ExecuteAsync(null);

        Assert.That(page.ErrorText, Is.Empty);
        Assert.That(page.Password, Is.Empty);
    }

    [Test]
    public async Task EmailOfTheProfileIsShown()
    {
        LoginPageModel page = CreateLoginPage(out _);

        await page.InitializeCommand.ExecuteAsync(null);

        Assert.That(page.Email, Is.EqualTo("me@example.com"));
    }

    [Test]
    public async Task VerifiedPasswordLeadsOnToTheActionVerifiedFor([Values(ProfileAction.Change, ProfileAction.Delete)] ProfileAction action)
    {
        LoginPageModel page = CreateLoginPage(out ScriptedUserInterface userInterface);
        page.InitializeVerification(action);
        page.Password = "password";

        await page.LoginCommand.ExecuteAsync(null);

        Assert.That(userInterface.Destinations, Is.EqualTo([AppDestination.ChangeProfile]));
        Assert.That(userInterface.Payloads, Is.EqualTo([action]));
    }

    private static LoginPageModel CreateLoginPage(out ScriptedUserInterface userInterface)
    {
        userInterface = new ScriptedUserInterface();
        return new LoginPageModel(new FakeProfileService(), new FakeIncomingFileService(), userInterface);
    }
}
