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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.PageModels;

public partial class HomePageModel(
    IProfileService profileService,
    SessionExitService sessionExitService,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IStatusTextPageModel, IBreadcrumbPageModel
{
    public string Breadcrumb => MobileTexts.BreadcrumbHome;

    [ObservableProperty]
    public partial string Email { get; set; } = profileService.CurrentEmail;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MyFoldersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecentFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(InAppCommand))]
    [NotifyCanExecuteChangedFor(nameof(EncryptToShareCommand))]
    [NotifyCanExecuteChangedFor(nameof(WipeCommand))]
    [NotifyCanExecuteChangedFor(nameof(SignOutCommand))]
    public partial bool IsBusy { get; set; }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task MyFoldersAsync() => UserInterfaceService.NavigateToAsync(AppDestination.WorkFolders);

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task RecentFilesAsync() => UserInterfaceService.NavigateToAsync(AppDestination.RecentFiles);

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task Wipe() => UserInterfaceService.NavigateToAsync(AppDestination.WorkFolders, WorkFolderIntent.Delete);

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task InApp() => UserInterfaceService.NavigateToAsync(AppDestination.WorkFolders, WorkFolderIntent.Copy);

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task EncryptToShare() =>
        UserInterfaceService.NavigateToAsync(AppDestination.WorkFolders, WorkFolderIntent.Share);

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private Task SignOut() => sessionExitService.ExitAsync();

    private bool CanUseCommand()
        => !IsBusy;
}
