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

using CommunityToolkit.Maui;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

using System;
using System.Globalization;

using Xecrets.Core.Public;
using Xecrets.Common.Abstractions;
using Xecrets.Common.Implementation;
using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Data;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Pages;
using Xecrets.Mobile.Services;
using Xecrets.Mobile.Utilities;
using Xecrets.Texts;

namespace Xecrets.Mobile;

public static class MauiProgram
{
    internal static ICrashLogService CrashLog { get; } = new CrashLogService();

    internal static IServiceProvider? Services { get; private set; }

    public static MauiApp CreateMauiApp()
    {
        string deviceFormattingCultureName = CultureInfo.CurrentCulture.Name;
        BuildInformation buildInformation = new();
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts => fonts.AddFont("FluentSystemIcons-Regular.ttf", "FluentUI"));

        if (buildInformation.IsDebug)
        {
            builder.Logging.AddDebug();
            builder.Services.AddLogging(configure => configure.AddDebug());
        }

        builder.ConfigurePlatform();

        builder.Services.AddXecretsCore();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IProtectedPayload, ProtectedPayload>();
        builder.Services.AddSingleton<MobileDataStore>();
        builder.Services.AddSingleton<IXecretsDataStore>(services => services.GetRequiredService<MobileDataStore>());
        builder.Services.AddSingleton<IProfileStore, ProfileStore>();
        builder.Services.AddSingleton<ProfileSession>();
        builder.Services.AddSingleton<ITransientFileService, TransientFileService>();
        builder.Services.AddSingleton<PreviewState>();
        builder.Services.AddSingleton<DecryptionPasswordRequestState>();
        builder.Services.AddSingleton<IFlowContext, FlowContext>();
        builder.Services.AddSingleton<IProfileService, ProfileService>();
        builder.Services.AddSingleton<IDecryptedFileViewer, DecryptedFileViewer>();
        builder.Services.AddSingleton<IEncryptionPreparationService, EncryptionPreparationService>();
        builder.Services.AddSingleton<IPreviewService, PreviewService>();
        builder.Services.AddSingleton<IIncomingFileService, IncomingFileService>();
        builder.Services.AddSingleton<SessionExitService>();
        builder.Services.AddSingleton<ICrashTestService, CrashTestService>();
        builder.Services.AddSingleton<IWorkFolderOperationService, WorkFolderOperationService>();
        builder.Services.AddSingleton<WorkFolderStorage>();
        builder.Services.AddSingleton(CrashLog);
        builder.Services.AddSingleton<IBuildInformation>(buildInformation);
        builder.Services.AddSingleton<IPageHeaderService, PageHeaderService>();
        builder.Services.AddSingleton<IThirdPartyNoticesService, ThirdPartyNoticesService>();
        builder.Services.AddSingleton(new Cultures(() => deviceFormattingCultureName));
        builder.Services.AddSingleton<MobileCultureCoordinator>();

        builder.Services.AddTransient<StartupPageModel>();
        builder.Services.AddTransient<LoginPageModel>();
        builder.Services.AddTransient<CreateProfilePageModel>();
        builder.Services.AddTransient<HomePageModel>();

        builder.Services.AddTransientWithShellRoute<PreviewPage, PreviewPageModel>("preview");
        builder.Services.AddTransientWithShellRoute<ViewPage, ViewPageModel>("view");
        builder.Services.AddTransientWithShellRoute<EditPage, EditPageModel>("edit");
        builder.Services.AddTransientWithShellRoute<EncryptResultPage, EncryptResultPageModel>("encrypt-result");
        builder.Services.AddTransientWithShellRoute<EncryptToSharePage, EncryptToSharePageModel>("encrypt-to-share");
        builder.Services.AddTransientWithShellRoute<WorkFoldersPage, WorkFoldersPageModel>("work-folders");
        builder.Services.AddTransientWithShellRoute<EnterPasswordPage, EnterPasswordPageModel>("enter-password");
        builder.Services.AddTransientWithShellRoute<AboutPage, AboutPageModel>("about");
        builder.Services.AddTransientWithShellRoute<ThirdPartyLicensesPage, ThirdPartyLicensesPageModel>("third-party-licenses");
        builder.Services.AddTransientWithShellRoute<DebugPage, DebugPageModel>("debug");

        MauiApp app = builder.Build();
        Services = app.Services;
        return app;
    }
}
