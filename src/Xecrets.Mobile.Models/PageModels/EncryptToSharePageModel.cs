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
using Xecrets.Texts;

namespace Xecrets.Mobile.Models.PageModels;

public partial class EncryptToSharePageModel(
    IProfileService profileService,
    IFileAccess fileAccess,
    IEncryptionPreparationService encryptionPreparationService,
    IFlowContext flowContext,
    ICrashLogService crashLogService,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IStatusTextPageModel, IBreadcrumbPageModel
{
    // The file a copy of is encrypted with the password, chosen on the page before.
    private FileReference _file = new("", "");

    // The file has been chosen on the page before, which is named for sharing.
    public string Breadcrumb => BreadcrumbFormatter.Format(
        flowContext.Origin,
        MobileTexts.BreadcrumbEncryptToShare,
        MobileTexts.BreadcrumbPassword);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial bool IsBusy { get; set; }

    public void Initialize(FileReference file)
    {
        _file = file;
        StatusText = string.Empty;
        OnPropertyChanged(nameof(Breadcrumb));
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task Submit()
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;

            string password = Password;
            ReferencedReadableFile source = new(fileAccess, _file);
            EncryptionPreparationResult? result;

            if (_file.Name.IsEncrypted())
            {
                result = await encryptionPreparationService.ReEncryptWithPasswordAsync(source, password);
                if (result is null)
                {
                    StatusText = MobileTexts.DialogTextWrongPasswordOpen;
                    return;
                }
            }
            else
            {
                result = await encryptionPreparationService.EncryptWithPasswordAsync(source, password);
            }

            await profileService.RecordExtraPasswordUseAsync(password);

            Password = string.Empty;
            await UserInterfaceService.GoBackAsync(null);
            await UserInterfaceService.NavigateToAsync(AppDestination.EncryptResult, result);
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            crashLogService.WriteSwallowedException(ex);
            StatusText = ex.FormatException();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSubmit()
        => !IsBusy && Password.Length > 0;
}
