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
public sealed class BreadcrumbTests
{
    [TestCase(FlowOrigin.Home, false, nameof(MobileTexts.BreadcrumbEncryptedCopy))]
    [TestCase(FlowOrigin.Home, true, nameof(MobileTexts.BreadcrumbEncryptToShare))]
    [TestCase(FlowOrigin.ReceivedFile, false, nameof(MobileTexts.BreadcrumbEncryptedCopy))]
    public void EncryptResultIsNamedForTheKindOfCopy(FlowOrigin origin, bool isForPassword, string pageTextName)
    {
        TestApp app = new();
        app.Flow.Begin(origin);
        EncryptResultPageModel page = new(null!, app.Operations, app.Recent, app.Transient, app.Flow, app.CrashLog, app.UserInterface);

        page.Initialize(EncryptionPreparationResult.Empty with { IsForPassword = isForPassword });

        Assert.That(page.Breadcrumb, Is.EqualTo(ExpectedBreadcrumb(origin, pageTextName)));
    }

    [Test]
    public async Task PasswordAskedForInMyFoldersIsShownUnderMyFolders()
    {
        TestApp app = new();
        WorkFolder docs = app.AddFolder("docs");
        app.AddFile("docs/x.axx", "ENC:a.txt\ntext");
        app.Access.PickedFiles.Enqueue("docs/x.axx");
        app.Core.Password = "unknown";

        await app.Operations.PickAndRunAsync(WorkFolderIntent.Auto, docs);

        EnterPasswordPageModel page = new(app.Preview, app.OperationService, app.Flow, app.UserInterface);
        Assert.That(app.UserInterface.Destinations, Is.EqualTo([AppDestination.EnterPassword]));
        Assert.That(page.Breadcrumb, Is.EqualTo(string.Join(
            MobileTexts.BreadcrumbSeparator,
            MobileTexts.BreadcrumbHome,
            MobileTexts.BreadcrumbMyFolders,
            MobileTexts.BreadcrumbPassword)));
    }

    [TestCase(WorkFolderIntent.Auto, nameof(MobileTexts.BreadcrumbMyFolders))]
    [TestCase(WorkFolderIntent.Delete, nameof(MobileTexts.BreadcrumbWipe))]
    [TestCase(WorkFolderIntent.Copy, nameof(MobileTexts.BreadcrumbCopy))]
    [TestCase(WorkFolderIntent.Share, nameof(MobileTexts.BreadcrumbEncryptToShare))]
    public void MyFoldersIsNamedForTheOperationItWasOpenedFor(WorkFolderIntent intent, string pageTextName)
    {
        TestApp app = new();
        WorkFoldersPageModel page = app.CreateWorkFoldersPage();

        page.Initialize(intent);

        string pageText = pageTextName switch
        {
            nameof(MobileTexts.BreadcrumbMyFolders) => MobileTexts.BreadcrumbMyFolders,
            nameof(MobileTexts.BreadcrumbWipe) => MobileTexts.BreadcrumbWipe,
            nameof(MobileTexts.BreadcrumbCopy) => MobileTexts.BreadcrumbCopy,
            nameof(MobileTexts.BreadcrumbEncryptToShare) => MobileTexts.BreadcrumbEncryptToShare,
            _ => throw new ArgumentOutOfRangeException(nameof(pageTextName), pageTextName, @"Unexpected page text."),
        };
        Assert.That(page.Breadcrumb, Is.EqualTo(string.Join(
            MobileTexts.BreadcrumbSeparator,
            MobileTexts.BreadcrumbHome,
            pageText)));
    }

    private static string ExpectedBreadcrumb(FlowOrigin origin, string pageTextName)
    {
        string originText = origin switch
        {
            FlowOrigin.Home => MobileTexts.BreadcrumbHome,
            FlowOrigin.ReceivedFile => MobileTexts.BreadcrumbReceivedFile,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, @"Unexpected flow origin."),
        };
        string pageText = pageTextName switch
        {
            nameof(MobileTexts.BreadcrumbEncryptedCopy) => MobileTexts.BreadcrumbEncryptedCopy,
            nameof(MobileTexts.BreadcrumbEncryptToShare) => MobileTexts.BreadcrumbEncryptToShare,
            _ => throw new ArgumentOutOfRangeException(nameof(pageTextName), pageTextName, @"Unexpected page text."),
        };

        return string.Join(MobileTexts.BreadcrumbSeparator, originText, pageText);
    }
}
