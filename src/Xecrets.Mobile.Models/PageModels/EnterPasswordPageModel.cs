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
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.PageModels;

public partial class EnterPasswordPageModel(
    IPreviewService previewService,
    IWorkFolderOperationService workFolderOperationService,
    IFlowContext flowContext,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IBreadcrumbPageModel
{
    public string Breadcrumb =>
        BreadcrumbFormatter.Format(flowContext.Origin, MobileTexts.BreadcrumbPassword);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty] public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial bool IsBusy { get; set; }

    public void Initialize()
    {
        ErrorText = previewService.HasPendingPasswordRequest || workFolderOperationService.HasPendingPasswordRequest
            ? MobileTexts.DialogTextWrongPasswordOpen
            : string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task Submit()
    {
        try
        {
            IsBusy = true;
            ErrorText = string.Empty;

            bool isWorkFolderRequest = workFolderOperationService.HasPendingPasswordRequest;
            PreviewPreparationStatus status = isWorkFolderRequest
                ? ToPreparationStatus(await workFolderOperationService.DecryptPendingWithPasswordAsync(Password))
                : await previewService.PrepareWithPasswordAsync(Password);
            if (status == PreviewPreparationStatus.Cancelled)
            {
                Password = string.Empty;
                await UserInterfaceService.GoBackAsync(null);
                return;
            }

            if (status == PreviewPreparationStatus.WrongPassword)
            {
                ErrorText = MobileTexts.DialogTextWrongPasswordOpen;
                return;
            }

            Password = string.Empty;
            if (isWorkFolderRequest)
            {
                await UserInterfaceService.GoBackAsync(null);
            }
            else
            {
                await UserInterfaceService.NavigateToAsync(AppDestination.Preview);
            }
        }
        catch (OperationCanceledException)
        {
            ErrorText = MobileTexts.DialogTextOperationNotCompleted;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSubmit() => !IsBusy && Password.Length > 0;

    // A file that was not decrypted, although the password was right, is left as it was, and the user has been told
    // why, as when the user cancels choosing where to save it.
    private static PreviewPreparationStatus ToPreparationStatus(FileOperationOutcome outcome) => outcome.Status switch
    {
        FileOperationStatus.Completed => PreviewPreparationStatus.Prepared,
        FileOperationStatus.AwaitingPassword => PreviewPreparationStatus.WrongPassword,
        FileOperationStatus.NotDone => PreviewPreparationStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Status, @"Unknown file operation status."),
    };

    public void CancelWorkFolderRequest() => workFolderOperationService.CancelPasswordRequest();
}
