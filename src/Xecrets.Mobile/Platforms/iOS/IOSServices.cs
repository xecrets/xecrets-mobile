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

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

using UIKit;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Utilities;
using Xecrets.Mobile.Platforms.Apple;
using Xecrets.Mobile.Services;
using Xecrets.Mobile.Utilities;

namespace Xecrets.Mobile.Platforms.iOS;

[SupportedOSPlatform("ios")]
public partial class IOSServices : PlatformServicesBase
{
    public override string CrashPageAdditionalInformation => MobileTexts.CrashPageAppleAdditionalInformation;

    public override void RegisterCrashHandlers(ICrashLogService crashLogService)
    {
        AppleCrashHandler.Register(crashLogService);
    }

    public override void CrashNative()
    {
        _ = RaiseSignal(6);
    }

    public IOSServices()
    {
        AppleTypography.RegisterSemiboldMappings();
        EntryHandler.Mapper.AppendToMapping(PasswordEntryProperties.ConfigureMapperKey, (handler, view) =>
        {
            Entry entry = (Entry)view;
            if (PasswordEntryProperties.GetPurpose(entry) is not { } purpose)
            {
                return;
            }

            handler.PlatformView.TextContentType = purpose == PasswordEntryPurpose.NewPassword
                ? UITextContentType.NewPassword
                : UITextContentType.Password;
        });
    }

    [LibraryImport("libSystem", EntryPoint = "raise")]
    private static partial int RaiseSignal(int signal);
}
