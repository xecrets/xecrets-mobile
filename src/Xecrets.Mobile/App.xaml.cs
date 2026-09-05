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

using Microsoft.Maui;
using Microsoft.Maui.Controls;

using Microsoft.Extensions.DependencyInjection;

using System;
using Xecrets.Common.Abstractions;
using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Services;
using Xecrets.Mobile.Services;

namespace Xecrets.Mobile;

public partial class App
{
    private readonly ITransientFileService _transientFileService;
    private readonly IUserInterfaceService _userInterfaceService;
    private readonly SessionExitService _sessionExitService;
    private readonly IBuildInformation _buildInformation;
    private readonly IXecretsDataStore _dataStore;
    private readonly IServiceProvider _services;
    private readonly MobileCultureCoordinator _cultureCoordinator;

    public App(
        ITransientFileService transientFileService,
        IUserInterfaceService userInterfaceService,
        SessionExitService sessionExitService,
        IBuildInformation buildInformation,
        IXecretsDataStore dataStore,
        IServiceProvider services,
        MobileCultureCoordinator cultureCoordinator,
        IPlatformServices platformServices,
        ICrashLogService crashLogService)
    {
        platformServices.RegisterCrashHandlers(crashLogService);
        _transientFileService = transientFileService;
        _userInterfaceService = userInterfaceService;
        _sessionExitService = sessionExitService;
        _buildInformation = buildInformation;
        _dataStore = dataStore;
        _services = services;
        _cultureCoordinator = cultureCoordinator;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = new(new ContentPage());
        window.Created += OnWindowCreated;
        window.Created += InitializeWindowAsync;
        return window;
    }

    private static void OnWindowCreated(object? sender, EventArgs e)
    {
        Window window = (Window)sender!;
        PlatformWindow.Configure(window);
    }

    // ReSharper disable once AsyncVoidEventHandlerMethod - the exception is caught and logged as unhandled.
    private async void InitializeWindowAsync(object? sender, EventArgs e)
    {
        await _transientFileService.MaybeWipeTrackedFilesAsync();
        await _cultureCoordinator.ApplySavedAsync();

        Window window = (Window)sender!;
        window.Page = CreateAppShell();
    }

    private AppShell CreateAppShell() => new(
        _userInterfaceService,
        _sessionExitService,
        _buildInformation,
        _dataStore,
        _services.GetRequiredService<StartupPageModel>());
}
