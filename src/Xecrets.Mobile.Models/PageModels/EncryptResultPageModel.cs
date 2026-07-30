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

public partial class EncryptResultPageModel(
    IFileService fileService,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IStatusTextPageModel
{
    private const string _nonBreakingSpace = "\u00A0";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendToCommand))]
    // ReSharper disable once MemberCanBeMadeStatic.Local
    private partial EncryptionPreparationResult Result { get; set; } = EncryptionPreparationResult.Empty;

    [ObservableProperty]
    public partial string FileNameText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MetadataText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendToCommand))]
    // ReSharper disable once MemberCanBeMadeStatic.Local
    private partial bool IsBusy { get; set; }

    public void Initialize(EncryptionPreparationResult result)
    {
        Result = result;
        FileNameText = result.DisplayName;
        MetadataText = CreateMetadataText(result);
        StatusText = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task SaveAs()
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            EncryptionPreparationResult encryptionResult = Result;
            SaveFileResult saveResult = await fileService.SaveAsAsync(
                encryptionResult.FilePath,
                encryptionResult.DisplayName,
                encryptionResult.OriginalSourcePath);
            if (!saveResult.IsCancelled)
            {
                StatusText = MobileTexts.DialogTextSaved;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        catch (FileNotFoundException)
        {
            StatusText = MobileTexts.DialogTextTemporaryFileUnavailable;
        }
        catch (Exception ex)
        {
            StatusText = FormatStatusText(MobileTexts.DialogTextSaveAsFailed, ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task SendTo()
    {
        try
        {
            IsBusy = true;
            StatusText = string.Empty;
            EncryptionPreparationResult result = Result;
            await fileService.SendToAsync(
                result.FilePath,
                result.DisplayName,
                result.ContentType);
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        catch (FileNotFoundException)
        {
            StatusText = MobileTexts.DialogTextTemporaryFileUnavailable;
        }
        catch (Exception ex)
        {
            StatusText = FormatStatusText(MobileTexts.DialogTextSendToFailed, ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Close()
    {
        DeleteTemporaryFile(Result.FilePath);
        Result = EncryptionPreparationResult.Empty;
        await UserInterfaceService.GoBackAsync();
    }

    private bool CanUseCommand()
        => !IsBusy && Result != EncryptionPreparationResult.Empty;

    private static void DeleteTemporaryFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            string? directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static string CreateMetadataText(EncryptionPreparationResult result)
    {
        string sizeValue = $"{result.FileSize:N0}".Replace(" ", _nonBreakingSpace, StringComparison.Ordinal);

        return $"{result.ContentType} {sizeValue} bytes";
    }

    private static string FormatStatusText(string message, Exception exception)
    {
        string exceptionMessage = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;

        return $"{message} {exceptionMessage}";
    }
}
