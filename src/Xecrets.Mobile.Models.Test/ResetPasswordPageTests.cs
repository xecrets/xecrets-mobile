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
using Xecrets.Words.Abstractions;
using Xecrets.Words.Model;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class ResetPasswordPageTests
{
    [Test]
    public async Task EmailOfTheProfileIsShown()
    {
        ResetPasswordPageModel page = CreateResetPasswordPage(out _, out _);

        await page.InitializeCommand.ExecuteAsync(null);

        Assert.That(page.Email, Is.EqualTo("me@example.com"));
    }

    [Test]
    public void ResetNeedsBothPasswords()
    {
        ResetPasswordPageModel page = CreateResetPasswordPage(out _, out _);

        page.Password = "new password";

        Assert.That(page.ResetCommand.CanExecute(null), Is.False);
        page.ConfirmPassword = "new password";
        Assert.That(page.ResetCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task DifferentPasswordsAreNotReset()
    {
        ResetPasswordPageModel page = CreateResetPasswordPage(out FakeProfileService profile,
            out ScriptedUserInterface userInterface);
        page.Password = "new password";
        page.ConfirmPassword = "other password";

        await page.ResetCommand.ExecuteAsync(null);

        Assert.That(page.ErrorText, Is.EqualTo(MobileTexts.DialogValidationConfirmPassword));
        Assert.That(profile.ResetPassword, Is.Null);
        Assert.That(userInterface.Destinations, Is.Empty);
    }

    [Test]
    public async Task ResetWithTheNewPasswordLeadsBackToSignIn()
    {
        ResetPasswordPageModel page = CreateResetPasswordPage(out FakeProfileService profile,
            out ScriptedUserInterface userInterface);
        page.Password = "new password";
        page.ConfirmPassword = "new password";

        await page.ResetCommand.ExecuteAsync(null);

        Assert.That(profile.ResetPassword, Is.EqualTo("new password"));
        Assert.That(userInterface.Destinations, Is.EqualTo([AppDestination.Login]));
    }

    private static ResetPasswordPageModel CreateResetPasswordPage(
        out FakeProfileService profile,
        out ScriptedUserInterface userInterface)
    {
        profile = new FakeProfileService();
        userInterface = new ScriptedUserInterface();
        return new ResetPasswordPageModel(profile, new FixedStrengthMeter(), userInterface);
    }

    private sealed class FixedStrengthMeter : IStrengthMeter
    {
        public int StrengthPercent(string password) => 50;

        public StrengthColor ToStrengthColor(int strength) => StrengthColor.Green;
    }
}
