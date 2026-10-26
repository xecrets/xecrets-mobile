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

using Xecrets.Core.Public;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.PageModels;

public partial class CacheBrowserPageModel : PageModelBase
{
    private readonly IFileService _fileService;
    private readonly ICrashLogService _crashLogService;
    private readonly string _cacheDirectory;
    private string _currentDirectory;

    public CacheBrowserPageModel(
        IFileService fileService,
        ICrashLogService crashLogService,
        IUserInterfaceService userInterfaceService)
        : base(userInterfaceService)
    {
        _fileService = fileService;
        _crashLogService = crashLogService;
        // The whole cache of the app, of which the cache directory of the files is a part, so that also the crash logs
        // are shown.
        _cacheDirectory = Path.GetDirectoryName(fileService.CacheDirectory)!;
        _currentDirectory = _cacheDirectory;
    }

    public ObservableCollection<CacheBrowserEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial string CurrentPath { get; set; } = "Cache";

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [RelayCommand]
    private Task LoadAsync()
    {
        try
        {
            if (!Directory.Exists(_currentDirectory))
            {
                _currentDirectory = _cacheDirectory;
            }

            Entries.Clear();
            foreach (DirectoryInfo directory in new DirectoryInfo(_currentDirectory).EnumerateDirectories()
                         .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase))
            {
                Entries.Add(new CacheBrowserEntry(directory.FullName, directory.Name, true, 0, OpenCommand));
            }

            foreach (FileInfo file in new DirectoryInfo(_currentDirectory).EnumerateFiles()
                         .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
            {
                Entries.Add(new CacheBrowserEntry(file.FullName, file.Name, false, file.Length, OpenCommand));
            }

            CurrentPath = GetCurrentPath();
            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            _crashLogService.WriteSwallowedException(ex);
            StatusText = ex.FormatException();
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task UpAsync()
    {
        if (!string.Equals(_currentDirectory, _cacheDirectory, StringComparison.Ordinal))
        {
            _currentDirectory = Directory.GetParent(_currentDirectory)!.FullName;
        }

        return LoadAsync();
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        string status = string.Empty;
        try
        {
            foreach (CacheBrowserEntry entry in Entries)
            {
                if (entry.IsDirectory)
                {
                    foreach (string file in Directory.EnumerateFiles(entry.FilePath, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }

                    Directory.Delete(entry.FilePath, recursive: true);
                }
                else
                {
                    File.SetAttributes(entry.FilePath, FileAttributes.Normal);
                    File.Delete(entry.FilePath);
                }
            }

            if (!string.Equals(_currentDirectory, _cacheDirectory, StringComparison.Ordinal))
            {
                string parentDirectory = Directory.GetParent(_currentDirectory)!.FullName;
                Directory.Delete(_currentDirectory);
                _currentDirectory = parentDirectory;
            }
        }
        catch (Exception ex)
        {
            status = ex.FormatException();
        }
        finally
        {
            await LoadAsync();
        }

        // After the reload, which clears the status.
        if (status.Length > 0)
        {
            StatusText = status;
        }
    }

    [RelayCommand]
    private async Task OpenAsync(CacheBrowserEntry entry)
    {
        try
        {
            if (entry.IsDirectory)
            {
                _currentDirectory = entry.FilePath;
                await LoadAsync();
                return;
            }

            await _fileService.ViewFileAsync(new DecryptedFileInfo(
                entry.FilePath,
                entry.DisplayName,
                FileTypeClassifier.DetectContentType(entry.DisplayName),
                entry.Size,
                PreviewKind.External));
        }
        catch (Exception ex)
        {
            _crashLogService.WriteSwallowedException(ex);
            StatusText = ex.FormatException();
        }
    }

    private string GetCurrentPath()
    {
        string relativePath = Path.GetRelativePath(_cacheDirectory, _currentDirectory);
        return relativePath == "." ? "Cache" : Path.Combine("Cache", relativePath);
    }
}
