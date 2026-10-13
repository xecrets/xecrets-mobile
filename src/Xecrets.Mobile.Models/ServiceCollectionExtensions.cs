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

using Microsoft.Extensions.DependencyInjection;

using Xecrets.Common.Abstractions;
using Xecrets.Core.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Data;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services and page models implemented here. The platform services they depend on are registered
    /// by the app.
    /// </summary>
    public static IServiceCollection AddMobileModels(this IServiceCollection services)
    {
        services.AddSingleton<MobileDataStore>();
        services.AddSingleton<IXecretsDataStore>(serviceProvider => serviceProvider.GetRequiredService<MobileDataStore>());
        services.AddSingleton<IDeviceSettings, MobileDeviceSettings>();
        services.AddSingleton<IProfileStore, ProfileStore>();
        services.AddSingleton<ProfileSession>();
        services.AddSingleton<IFileWiper, FileWiper>();
        services.AddSingleton<ITransientFileService, TransientFileService>();
        services.AddSingleton<PreviewState>();
        services.AddSingleton<DecryptionPasswordRequestState>();
        services.AddSingleton<IFlowContext, FlowContext>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<IDecryptedFileViewer, DecryptedFileViewer>();
        services.AddSingleton<EncryptRequestFactory>();
        services.AddSingleton<IEncryptionPreparationService, EncryptionPreparationService>();
        services.AddSingleton<IPreviewService, PreviewService>();
        services.AddSingleton<IIncomingFileService, IncomingFileService>();
        services.AddSingleton<SessionExitService>();
        services.AddSingleton<IWorkFolderOperationService, WorkFolderOperationService>();
        services.AddSingleton<IEditSaveService, EditSaveService>();
        services.AddSingleton<FolderAccessWorkflow>();
        services.AddSingleton<FileOperationWorkflow>();
        services.AddSingleton<GrantCleanupService>();
        services.AddSingleton<InstallationService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<WorkFolderStorage>();

        services.AddTransient<StartupPageModel>();
        services.AddTransient<LoginPageModel>();
        services.AddTransient<HomePageModel>();

        return services;
    }
}
