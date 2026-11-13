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
using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class HomePageTests
{
    /// <summary>
    /// A file handed off to another app is kept for an hour after it is disposed, for the app to read it. In a debug
    /// build, it is kept for a minute.
    /// </summary>
    [TestCase(false, 60)]
    [TestCase(true, 1)]
    public void BackAtTheStartTheTemporaryFilesNoLongerNeededAreWiped(bool isDebug, int maxAgeMinutes)
    {
        using TestApp app = new();
        HomePageModel page = CreatePage(app, isDebug);

        page.SweepTemporaryFilesCommand.Execute(null);

        Assert.That(app.Transient.Sweeps, Is.EqualTo([TimeSpan.FromMinutes(maxAgeMinutes)]));
    }

    /// <summary>
    /// A preview left without closing it, such as with the back button, is done with once the user is back at the
    /// start. A file still in use by an app it was handed off to is kept.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void BackAtTheStartThePreviewIsEnded(bool isHandedOff)
    {
        using TestApp app = new();
        ITransientFile decryptedFile = app.Transient.CreateHandoffFile("a.jpg");
        File.WriteAllText(decryptedFile.FilePath, "image");
        app.Preview.Current.SetFile(
            new DecryptedFileInfo(decryptedFile.FilePath, "a.jpg", "image/jpeg", 5, PreviewKind.Image, FileTimes.UtcNow),
            EncryptedWithParameters.Empty,
            decryptedFile);
        if (isHandedOff)
        {
            app.Preview.Current.MarkHandedOff();
        }

        CreatePage(app, isDebug: false).SweepTemporaryFilesCommand.Execute(null);

        Assert.That(app.Preview.Current.IsReady, Is.False);
        Assert.That(File.Exists(decryptedFile.FilePath), Is.EqualTo(isHandedOff));
    }

    /// <summary>
    /// An encrypted copy left without closing its page, such as with the back button, is done with once the user is
    /// back at the start. A copy still in use by an app it was sent to is kept.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void BackAtTheStartTheEncryptedCopyIsEnded(bool isHandedOff)
    {
        using TestApp app = new();
        ITransientFile encryptedFile = app.Transient.CreateEncryptedOutputFile("a.txt.axx");
        File.WriteAllText(encryptedFile.FilePath, "encrypted");
        EncryptResultPageModel resultPage = new(null!, app.Operations, app.Recent, app.Encrypted, app.Flow, app.CrashLog, app.UserInterface);
        resultPage.Initialize(new EncryptionPreparationResult(encryptedFile, "a.txt.axx", "application/octet-stream", 9, false));
        if (isHandedOff)
        {
            encryptedFile.MarkHandedOff();
        }

        CreatePage(app, isDebug: false).SweepTemporaryFilesCommand.Execute(null);

        Assert.That(app.Encrypted.Result, Is.EqualTo(EncryptionPreparationResult.Empty));
        Assert.That(File.Exists(encryptedFile.FilePath), Is.EqualTo(isHandedOff));
    }

    private static HomePageModel CreatePage(TestApp app, bool isDebug) =>
        new(
            new FakeProfileService(),
            null!,
            app.Transient,
            app.Preview,
            app.Encrypted,
            new ScriptedUserInterface(),
            new FakeBuildInformation { IsDebug = isDebug });
}
