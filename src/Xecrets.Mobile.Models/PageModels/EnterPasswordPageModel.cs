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

using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.PageModels;

/// <summary>
/// The decryption passwords, tried in turn when the password of the profile does not decrypt a file. Opened from the
/// menu they can be added and deleted. Opened when no known password decrypts a file, the password entered is tried on
/// that file, and kept when it decrypts it.
/// </summary>
public partial class EnterPasswordPageModel(
    IPreviewService previewService,
    IWorkFolderOperationService workFolderOperationService,
    IProfileService profileService,
    IFlowContext flowContext,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IBreadcrumbPageModel
{
    private DecryptionPasswordsIntent _intent = DecryptionPasswordsIntent.Manage;

    public string Breadcrumb => _intent switch
    {
        DecryptionPasswordsIntent.Manage => string.Join(
            MobileTexts.BreadcrumbSeparator,
            MobileTexts.BreadcrumbHome,
            MobileTexts.BreadcrumbDecryptionPasswords),
        DecryptionPasswordsIntent.Add => BreadcrumbFormatter.Format(
            flowContext.Origin,
            MobileTexts.BreadcrumbDecryptionPasswords),
        _ => throw new ArgumentOutOfRangeException(nameof(_intent), _intent, @"Unknown decryption passwords intent."),
    };

    public string Description => MobileTexts.DecryptionPasswordsDescription;

    public ObservableCollection<string> Passwords { get; } = [];

    /// <summary>
    /// The description, shown on the page itself while there are no passwords, to make it easier to find.
    /// </summary>
    [ObservableProperty] public partial string NoPasswordsText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool IsAdding { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty] public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial bool IsBusy { get; set; }

    public void Initialize(DecryptionPasswordsIntent intent)
    {
        _intent = intent;
        IsAdding = intent == DecryptionPasswordsIntent.Add;
        OnPropertyChanged(nameof(Breadcrumb));
    }

    public void Load()
    {
        ErrorText = _intent == DecryptionPasswordsIntent.Add
            && (previewService.HasPendingPasswordRequest || workFolderOperationService.HasPendingPasswordRequest)
                ? MobileTexts.DialogTextWrongPasswordOpen
                : string.Empty;
        ShowPasswords();
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private void Add()
    {
        ErrorText = string.Empty;
        IsAdding = true;
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task DeleteAsync(string password)
    {
        if (!await UserInterfaceService.DisplayConfirmationAsync(
                MobileTexts.MessageTextConfirmWipe, MobileTexts.ButtonOk, MobileTexts.LabelCancel))
        {
            return;
        }

        await profileService.RemoveExtraPasswordAsync(password);
        ShowPasswords();
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task Submit()
    {
        if (_intent == DecryptionPasswordsIntent.Manage)
        {
            await profileService.AddExtraPasswordAsync(Password);
            Password = string.Empty;
            IsAdding = false;
            ShowPasswords();
            return;
        }

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

            // Done with, the page is not left to go back to.
            Password = string.Empty;
            if (isWorkFolderRequest)
            {
                await UserInterfaceService.GoBackAsync(null);
            }
            else
            {
                await UserInterfaceService.NavigateReplacingAsync(AppDestination.Preview);
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

    private bool CanUseCommand() => !IsBusy;

    private void ShowPasswords()
    {
        Passwords.Clear();
        foreach (string password in profileService.GetExtraPasswords().Select(extraPassword => extraPassword.Password))
        {
            Passwords.Add(password);
        }

        NoPasswordsText = Passwords.Count == 0 ? Description : string.Empty;
    }

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
