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

using System;
using System.Runtime.Versioning;

using Android.Runtime;
using Android.Text;
using Android.Graphics;
using AndroidContentCaptureImportance = Android.Views.ViewImportantForContentCapture;

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Services;
using Xecrets.Mobile.Utilities;

namespace Xecrets.Mobile.Platforms.Android;

[SupportedOSPlatform("android28.0")]
public class AndroidServices : PlatformServicesBase
{
    public override void RegisterCrashHandlers()
    {
        AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
            CrashLogService.WriteCrashLog("Unhandled Android runtime exception", args.Exception);
    }

    public override void CrashNative()
    {
        global::Android.OS.Process.SendSignal(
            global::Android.OS.Process.MyPid(),
            (global::Android.OS.Signal)6);
    }

    // Registered as a DI singleton, so this runs exactly once per app lifetime. AppendToMapping
    // guarantees each action runs after MAUI's own mapping for that key has applied, so there's no
    // need to speculate about event-firing order; entries opt in via PasswordEntryProperties.
    public AndroidServices()
    {
        LabelHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (handler, view) =>
            ApplyFontWeight(view, handler.PlatformView));
        ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (handler, view) =>
            ApplyFontWeight(view, handler.PlatformView));

        // One-time setup, applied whenever a password entry's handler (re)connects.
        EntryHandler.Mapper.AppendToMapping(PasswordEntryProperties.ConfigureMapperKey, (handler, view) =>
        {
            Entry entry = (Entry)view;
            if (PasswordEntryProperties.GetPurpose(entry) is not { } purpose)
            {
                return;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                handler.PlatformView.SetAutofillHints(purpose == PasswordEntryPurpose.NewPassword ? "newPassword" : "password");
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                handler.PlatformView.ImportantForContentCapture = (int)AndroidContentCaptureImportance.NoExcludeDescendants;
            }
        });

        // MAUI's own IsPassword mapping only applies the password input-type variation when
        // IsPassword is true; when the user reveals the password, force the visible-password
        // variation instead of the platform's plain-text default. TextFlagNoSuggestions is already
        // handled by MAUI's own IsSpellCheckEnabled mapping (set via PlatformServicesBase), so it
        // doesn't need to be repeated here.
        EntryHandler.Mapper.AppendToMapping(nameof(IEntry.IsPassword), (handler, view) =>
        {
            Entry entry = (Entry)view;
            if (entry.IsPassword || PasswordEntryProperties.GetPurpose(entry) is null)
            {
                return;
            }

            handler.PlatformView.InputType = (handler.PlatformView.InputType & ~InputTypes.MaskVariation)
                | InputTypes.TextVariationVisiblePassword;
        });
    }

    private static void ApplyFontWeight(IView view, global::Android.Widget.TextView platformView)
    {
        if (Typography.IsSemibold(view))
        {
            platformView.Typeface = Typeface.Create(platformView.Typeface, 600, false);
        }
    }
}
