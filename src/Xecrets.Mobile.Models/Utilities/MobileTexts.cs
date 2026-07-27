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

using AppTexts = Xecrets.Texts.Texts;

namespace Xecrets.Mobile.Models.Utilities;

public static class MobileTexts
{
    public static string AboutPageThirdPartyNotice => "Built with .NET MAUI and CommunityToolkit.Maui. Other components are provided under their respective licenses, reproduced in full under Third-Party Licenses in the menu.";

    // Also used as the overflow menu item text, see MenuThirdPartyLicenses.
    public static string ThirdPartyLicensesPageTitle => "Third-Party Licenses";

    public static string ThirdPartyLicensesPageExplanation => "The application incorporates the following third-party material, under the terms reproduced here.";

    public static string CrashPageTitle => "Sorry, we crashed!";

    public static string CrashPageExplanation => "There was a problem. We have copied the following report to the clipboard. Please report this and paste the report into the message. Use the Report button to go to our support site.";

    public static string CrashPageAppleAdditionalInformation => "Apple may have additional crash information. You can report it manually or enable Share with App Developers under Analytics & Improvements.";

    public static string ButtonReport => "Report";

    // "Help", also used as the overflow menu item text, see MenuHelp.
    public static string ButtonHelp => AppTexts.ButtonHelp;

    public static string ButtonContinue => "Continue";

    public static string ButtonView => "View";

    // "Edit"
    public static string ButtonEdit => AppTexts.ButtonEdit;

    // "Encrypt"
    public static string ButtonEncrypt => AppTexts.ButtonEncrypt;

    // "Encrypt copy to share" + "...", the desktop wording of the same operation.
    public static string ButtonEncryptToShare => AppTexts.DialogTextEncryptCopyFor + "...";

    // "Decrypt"
    public static string ButtonDecrypt => AppTexts.ButtonDecrypt;

    // "Open" + "...", the app to open in is picked in the sheet that follows.
    public static string ButtonOpenIn => AppTexts.ButtonOpen + "...";

    public static string ButtonSendTo => "Send to...";

    public static string ButtonSave => "Save";

    // "Save As..."
    public static string ButtonSaveAs => AppTexts.ButtonSaveAs;

    // Not AppTexts.ButtonClose, that is "Close all" and this closes the current file only.
    public static string ButtonClose => "Close";

    // "Create"
    public static string ButtonCreate => AppTexts.ButtonCreateMobile;

    // "Ok"
    public static string ButtonOk => AppTexts.ButtonOkMobile;

    // "Help"
    public static string MenuHelp => ButtonHelp;

    public static string MenuInfo => "Xecrets Home";

    public static string MenuXecretsDesktop => "Xecrets Desktop";

    // "About"
    public static string MenuAbout => AppTexts.ButtonAbout;

    // "Third-Party Licenses"
    public static string MenuThirdPartyLicenses => ThirdPartyLicensesPageTitle;

    // "Not used for sending emails. It's an identifier for your local profile in the software and for sharing with others."
    public static string ToolTipEmail => AppTexts.ToolTipEmail;

    // "Your master password. Make it strong, write it down and keep it safe."
    public static string ToolTipMasterPassword => AppTexts.ToolTipMasterPassword;

    // "Signing in ensures you are using the correct password for encryption, and that you don't need to retype the
    // password each time. This is the master password for all encryption. The sign in is only local to the app."
    public static string ToolTipSignIn => AppTexts.ToolTipSignIn;

    // "Email"
    public static string WatermarkEmail => AppTexts.WatermarkEmail;

    // "Password"
    public static string WatermarkPassword => AppTexts.WatermarkPassword;

    // "Confirm password"
    public static string WatermarkConfirmPassword => AppTexts.WatermarkConfirmPassword;

    // "Password to share"
    public static string WatermarkPasswordShare => AppTexts.WatermarkPasswordShare;

    public static string DialogTextCannotSaveInPlace => "This file cannot be saved back on this device.";

    public static string DialogTextSaveAsFailed => "Could not save the file.";

    public static string DialogTextEncryptFailed => "Could not encrypt this file.";

    public static string DialogTextAlreadyEncrypted => "This file is already encrypted. Use Decrypt to open it.";

    public static string DialogTextSaved => "Saved.";

    public static string DialogTextNoAppAvailable => "No app is available to open this file.";

    // "Decrypt and open failed"
    public static string DialogTextOpenEncryptedFileFailed => AppTexts.DialogTextOpenFailed;

    public static string DialogTextTemporaryFileUnavailable => "The temporary decrypted file is not available.";

    public static string DialogTextOpenInFailed => "Could not open this file in another app.";

    public static string DialogTextSendToFailed => "Could not send this file.";

    public static string DialogTextPreviewFailed => "Could not preview this file.";
}
