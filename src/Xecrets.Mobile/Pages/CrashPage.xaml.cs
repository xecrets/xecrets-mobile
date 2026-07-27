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

using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Services;

using AppTexts = Xecrets.Texts.Texts;

namespace Xecrets.Mobile.Pages;

public partial class CrashPage
{
    private const string _supportUrl = "https://www.axantum.com/support";
    private readonly StartupPageModel _startupPageModel;
    private readonly IUserInterfaceService _userInterfaceService;
    private bool _initialized;

    public CrashPage(
        StartupPageModel startupPageModel,
        IPlatformServices platformServices,
        IUserInterfaceService userInterfaceService)
    {
        _startupPageModel = startupPageModel;
        _userInterfaceService = userInterfaceService;
        PlatformAdditionalInformation = platformServices.CrashPageAdditionalInformation;
        InitializeComponent();
        BindingContext = this;
    }

    public string PlatformAdditionalInformation { get; }

    // ReSharper disable once AsyncVoidMethod
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        string report = CrashLogService.ReadCurrent();
        CrashReportEditor.Text = report;
        await Clipboard.Default.SetTextAsync(report);
        CrashLogService.Rotate();
    }

    [RelayCommand]
    private Task Report() => _userInterfaceService.OpenBrowserAsync(_supportUrl);

    [RelayCommand]
    private Task Help() => _userInterfaceService.OpenBrowserAsync(AppTexts.XecretsHelpUrl());

    [RelayCommand]
    private async Task Continue()
    {
        await _startupPageModel.ContinueAfterCrashCommand.ExecuteAsync(null);
    }
}
