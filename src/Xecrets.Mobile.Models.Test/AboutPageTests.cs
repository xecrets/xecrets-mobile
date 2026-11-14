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
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class AboutPageTests
{
    [Test]
    public async Task NothingIsShownWithoutExtraPasswordsAndPrivateKeys()
    {
        AboutPageModel page = CreateAboutPage(out _);

        await page.InitializeCommand.ExecuteAsync(null);

        Assert.That(page.ExtraPasswordsInfo, Is.Empty);
        Assert.That(page.PrivateKeysInfo, Is.Empty);
    }

    [Test]
    public async Task NothingIsShownWhenSignedOut()
    {
        AboutPageModel page = CreateAboutPage(out FakeProfileService profile);
        profile.IsAuthenticated = false;
        profile.ExtraPasswords.Add(new PasswordUsage { Password = "extra password", });
        profile.PrivateKeyCounts = new PrivateKeyCounts(Decryptable: 1, Total: 2);

        await page.InitializeCommand.ExecuteAsync(null);

        Assert.That(page.ExtraPasswordsInfo, Is.Empty);
        Assert.That(page.PrivateKeysInfo, Is.Empty);
    }

    [Test]
    public async Task OnlyTheExtraPasswordsAreShownWithoutPrivateKeys()
    {
        AboutPageModel page = CreateAboutPage(out FakeProfileService profile);
        profile.ExtraPasswords.Add(new PasswordUsage { Password = "extra password", });
        profile.ExtraPasswords.Add(new PasswordUsage { Password = "other password", });

        await page.InitializeCommand.ExecuteAsync(null);

        Assert.That(page.ExtraPasswordsInfo, Is.EqualTo(MobileTexts.AboutDecryptionPasswords(2)));
        Assert.That(page.PrivateKeysInfo, Is.Empty);
    }

    [Test]
    public async Task OnlyThePrivateKeysAreShownWithoutExtraPasswords()
    {
        AboutPageModel page = CreateAboutPage(out FakeProfileService profile);
        profile.PrivateKeyCounts = new PrivateKeyCounts(Decryptable: 0, Total: 1);

        await page.InitializeCommand.ExecuteAsync(null);

        Assert.That(page.ExtraPasswordsInfo, Is.Empty);
        Assert.That(page.PrivateKeysInfo, Is.EqualTo(MobileTexts.AboutPrivateKeys(0, 1)));
    }

    [Test]
    public async Task BothTheExtraPasswordsAndThePrivateKeysAreShown()
    {
        AboutPageModel page = CreateAboutPage(out FakeProfileService profile);
        profile.ExtraPasswords.Add(new PasswordUsage { Password = "extra password", });
        profile.PrivateKeyCounts = new PrivateKeyCounts(Decryptable: 1, Total: 2);

        await page.InitializeCommand.ExecuteAsync(null);

        Assert.That(page.ExtraPasswordsInfo, Is.EqualTo(MobileTexts.AboutDecryptionPasswords(1)));
        Assert.That(page.PrivateKeysInfo, Is.EqualTo(MobileTexts.AboutPrivateKeys(1, 2)));
    }

    [Test]
    public async Task SevenTapsOnTheVersionAskAndYesEnablesTheDebugMenu()
    {
        AboutPageModel page = CreateAboutPage(new FakeBuildInformation(), out ScriptedUserInterface ui, out DebugMenu debugMenu);
        ui.Answers.Enqueue(true);
        int changedCount = 0;
        debugMenu.IsEnabledChanged += (_, _) => ++changedCount;
        await page.InitializeCommand.ExecuteAsync(null);

        await TapVersionAsync(page, 6);
        Assert.That(ui.Confirmations, Is.Empty);

        await TapVersionAsync(page, 1);
        Assert.That(ui.Confirmations, Is.EqualTo(new[] { "Enable debug menu?" }));
        Assert.That(ui.ConfirmationButtons, Is.EqualTo(new[] { ("Yes", "No") }));
        Assert.That(debugMenu.IsEnabled, Is.True);
        Assert.That(changedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task NoLeavesTheDebugMenuDisabledAndSevenMoreTapsAskAgain()
    {
        AboutPageModel page = CreateAboutPage(new FakeBuildInformation(), out ScriptedUserInterface ui, out DebugMenu debugMenu);
        ui.Answers.Enqueue(false);
        int changedCount = 0;
        debugMenu.IsEnabledChanged += (_, _) => ++changedCount;
        await page.InitializeCommand.ExecuteAsync(null);

        await TapVersionAsync(page, 7);
        Assert.That(debugMenu.IsEnabled, Is.False);
        Assert.That(changedCount, Is.Zero);

        await TapVersionAsync(page, 6);
        Assert.That(ui.Confirmations, Has.Count.EqualTo(1));

        await TapVersionAsync(page, 1);
        Assert.That(ui.Confirmations, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AppearingAgainResetsTheTaps()
    {
        AboutPageModel page = CreateAboutPage(new FakeBuildInformation(), out ScriptedUserInterface ui, out _);
        await page.InitializeCommand.ExecuteAsync(null);
        await TapVersionAsync(page, 6);

        await page.InitializeCommand.ExecuteAsync(null);
        await TapVersionAsync(page, 1);

        Assert.That(ui.Confirmations, Is.Empty);
    }

    [Test]
    public async Task TapsDoNotAskWhenTheDebugMenuIsAlreadyEnabled()
    {
        AboutPageModel page = CreateAboutPage(
            new FakeBuildInformation { IsDebug = true, },
            out ScriptedUserInterface ui,
            out DebugMenu debugMenu);
        await page.InitializeCommand.ExecuteAsync(null);

        await TapVersionAsync(page, 14);

        Assert.That(debugMenu.IsEnabled, Is.True);
        Assert.That(ui.Confirmations, Is.Empty);
    }

    private static async Task TapVersionAsync(AboutPageModel page, int count)
    {
        for (int i = 0; i < count; ++i)
        {
            await page.VersionTappedCommand.ExecuteAsync(null);
        }
    }

    private static AboutPageModel CreateAboutPage(out FakeProfileService profile)
    {
        profile = new FakeProfileService();
        FakeBuildInformation buildInformation = new();
        return new AboutPageModel(new ScriptedUserInterface(), buildInformation, profile, new DebugMenu(buildInformation));
    }

    private static AboutPageModel CreateAboutPage(
        FakeBuildInformation buildInformation,
        out ScriptedUserInterface ui,
        out DebugMenu debugMenu)
    {
        ui = new ScriptedUserInterface();
        debugMenu = new DebugMenu(buildInformation);
        return new AboutPageModel(ui, buildInformation, new FakeProfileService(), debugMenu);
    }
}
