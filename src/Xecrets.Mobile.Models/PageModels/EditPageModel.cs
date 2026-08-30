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

using Xecrets.Core.Abstractions;
using Xecrets.Core.Models;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Texts;

using AppTexts = Xecrets.Texts.Texts;

namespace Xecrets.Mobile.Models.PageModels;

public partial class EditPageModel(
    IPreviewService previewService,
    IProfileService profileService,
    ICoreServices coreServices,
    IFileService fileService,
    IUserInterfaceService userInterfaceService)
    : PageModelBase(userInterfaceService), IStatusTextPageModel
{
    [ObservableProperty]
    public partial string FileNameText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSaveVisible { get; set; }

    [ObservableProperty]
    public partial bool IsSaveToLocationVisible { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToLocationCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task Initialize()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady || state.Kind != PreviewKind.Text)
        {
            await UserInterfaceService.GoBackAsync();
            return;
        }

        state.EnableTextEditing();
        FileNameText = string.IsNullOrWhiteSpace(state.OriginalFileName)
            ? AppTexts.DisplayNameProgram
            : state.OriginalFileName;
        Text = state.Text;
        IsSaveVisible = CanOverwriteSourcePath(state.SourcePath);
        IsSaveToLocationVisible = !IsSaveVisible;
        StatusText = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task Save()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady || state.Kind != PreviewKind.Text || !IsSaveVisible)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = string.Empty;

            await PrepareTemporaryTextFileAsync(state);
            EncryptRequest request = CreateEncryptRequest(state.OriginalFileName);

            await using FileStream cleartext = File.OpenRead(state.DecryptedPath);
            await using FileStream encrypted = File.Open(state.SourcePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await coreServices.EncryptAsync(cleartext, encrypted, request);

            StatusText = MobileTexts.DialogTextSaved;
        }
        catch (Exception ex)
        {
            StatusText = FormatStatusText(MobileTexts.DialogTextCannotSaveInPlace, ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseCommand))]
    private async Task SaveToLocation()
    {
        IPreviewState state = previewService.Current;
        if (!state.IsReady || state.Kind != PreviewKind.Text || !IsSaveToLocationVisible)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = string.Empty;

            await PrepareTemporaryTextFileAsync(state);
            EncryptRequest request = CreateEncryptRequest(state.OriginalFileName);
            await using MemoryStream encrypted = new();
            await using (FileStream cleartext = File.OpenRead(state.DecryptedPath))
            {
                await coreServices.EncryptAsync(cleartext, encrypted, request);
            }

            encrypted.Position = 0;

            string fileName = state.OriginalFileName.ToEncryptedName(string.Empty);
            SaveFileResult result = await fileService.SaveAsAsync(
                encrypted,
                fileName,
                state.SourcePath);
            if (result.IsCancelled)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.FilePath))
            {
                state.UpdateSourcePath(result.FilePath);
                IsSaveVisible = CanOverwriteSourcePath(state.SourcePath);
                IsSaveToLocationVisible = !IsSaveVisible;
            }

            StatusText = MobileTexts.DialogTextSaved;
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
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
    private async Task Close()
    {
        await UserInterfaceService.GoBackAsync();
    }

    private bool CanUseCommand()
        => !IsBusy;

    private EncryptRequest CreateEncryptRequest(string originalFileName)
    {
        DateTime utcNow = DateTime.UtcNow;
        Identity identity = profileService.GetIdentity();
        return new EncryptRequest(
            identity.Passphrase,
            [profileService.GetPublicKey()],
            [],
            originalFileName,
            utcNow,
            utcNow,
            utcNow,
            true,
            new Progress<Progress>(_ => { }));
    }

    private static bool CanOverwriteSourcePath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return false;
        }

        try
        {
            using FileStream _ = File.Open(sourcePath, FileMode.Open, FileAccess.Write, FileShare.Read);
            return true;
        }
        catch
        {
            return false;
        }
    }
    private async Task PrepareTemporaryTextFileAsync(IPreviewState state)
    {
        if (state.Kind != PreviewKind.Text || state.File is null)
        {
            return;
        }

        TryClearReadOnly(state.File.FilePath);
        await File.WriteAllTextAsync(state.File.FilePath, Text);
        TryMakeReadOnly(state.File.FilePath);

        state.UpdateText(Text);
        state.UpdateFileSize(new FileInfo(state.File.FilePath).Length);
    }

    private static void TryClearReadOnly(string filePath)
    {
        try
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.ReadOnly);
        }
        catch
        {
            // Best effort. The file write will report the real failure if this matters.
        }
    }

    private static void TryMakeReadOnly(string filePath)
    {
        try
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
        }
        catch
        {
            // Best effort. Some mobile filesystems do not support this attribute.
        }
    }
}
