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
    #region Untranslated texts

    public static string AboutPageThirdPartyNotice => "Built with .NET MAUI and CommunityToolkit.Maui. Other components are provided under their respective licenses, reproduced in full under Third-Party Licenses in the menu. All third-party product names and trademarks are the property of their respective owners. Their use does not imply affiliation, sponsorship or endorsement.";

    // Not AppTexts.ButtonClose, that is "Close all" and this closes the current file only.
    public static string LabelClose => "Close";

    public static string LabelContinue => "Continue";

    public static string ButtonDecryptAs => "Decrypt As…";

    public static string ButtonEncryptAs => "Encrypt As…";

    public static string ButtonEncryptToShare => "Encrypt To…";

    public static string ButtonOpenIn => "Open In…";

    public static string ButtonReport => "Report";

    public static string ButtonSave => "Save";

    public static string ButtonSendTo => "Send To…";

    public static string ButtonView => "View";

    public static string CrashPageAppleAdditionalInformation => "Apple may have additional crash information. You can report it manually or enable Share with App Developers under Analytics & Improvements.";

    public static string CrashPageExplanation => "There was a problem. We have copied the following report to the clipboard. Please report this and paste the report into the message. Use the Report button to go to our support site.";

    public static string CrashPageTitle => "Sorry, we crashed!";

    public static string DecryptWorkFolderDescription => "Decrypt a file and delete the original. Add or select a folder, and then the file.";

    public static string DialogTextAddUnknownWorkFolder => "This is not a known work folder, would you like to add it?";

    public static string DialogTextAlreadyEncrypted => "This file is already encrypted. Use Decrypt to open it.";

    public static string DialogTextCannotSaveInPlace => "This file cannot be saved back on this device.";

    public static string DialogTextConfirmOverwrite => "The file '{0}' already exists. Overwrite it?";

    public static string DialogTextDecrypted => "The file was decrypted.";

    public static string DialogTextEncrypted => "The file was encrypted.";

    public static string DialogTextEncryptFailed => "Could not encrypt this file.";

    public static string DialogTextFolderName => "Name for this folder";

    public static string DialogTextNoAppAvailable => "No app is available to open this file.";

    public static string DialogTextOpenInFailed => "Could not open this file in another app.";

    public static string DialogTextPreviewFailed => "Could not preview this file.";

    public static string DialogTextSaveAsFailed => "Could not save the file.";

    public static string DialogTextSaved => "Saved.";

    public static string DialogTextSelfHandoffRejected => "Xecrets Ez can't handle files from itself.";

    public static string DialogTextSendToFailed => "Could not send this file.";

    public static string DialogTextSourceAndDestinationMatch => "The source and destination file names are the same.";

    public static string DialogTextTemporaryFileUnavailable => "The temporary decrypted file is not available.";

    public static string DialogTextWorkFolderFailed => "The folder operation could not be completed.";

    public static string DialogTextWorkFolderListReset => "The list of folders could not be read, and has been cleared. Please add the folders you use again.";

    public static string EncryptToShareDescription => "Enter a separate password for the encrypted copy. Share the password through a different channel.";

    public static string EncryptWorkFolderDescription => "Encrypt a file and delete the original. Add or select a folder, and then the file.";

    public static string HomeContentTitle => "Encrypt or decrypt files";

    public static string HomeDescription => "Choose an action below. Encrypt protects a file. Decrypt restores the original file.";

    public static string MenuInfo => "Xecrets Home";

    // "Third-Party Licenses"
    public static string MenuThirdPartyLicenses => ThirdPartyLicensesPageTitle;

    public static string MenuXecretsDesktop => "Xecrets Desktop";

    public static string ThirdPartyLicensesPageExplanation => "The application incorporates the following third-party material, under the terms reproduced here.";

    // Also used as the overflow menu item text, see MenuThirdPartyLicenses.
    public static string ThirdPartyLicensesPageTitle => "Third-Party Licenses";

    public static string WorkFoldersContentTitle => "My Folders";

    public static string WorkFoldersPageTitle => "Work folders";

    #endregion Untranslated texts

    #region Translated texts

    // "Create"
    public static string ButtonCreate => AppTexts.LabelCreate;

    // "Decrypt…", the ellipsis is kept since a file selection follows.
    public static string ButtonDecrypt => AppTexts.ButtonDecryptMore;

    // "Edit"
    public static string ButtonEdit => AppTexts.ButtonEdit;

    // "Encrypt…", the ellipsis is kept since a file selection follows.
    public static string ButtonEncrypt => AppTexts.ButtonEncryptMore;

    // "Help", also used as the overflow menu item text, see MenuHelp.
    public static string ButtonHelp => AppTexts.ButtonHelp;

    // "Ok"
    public static string ButtonOk => AppTexts.LabelOk;

    // "Save As…"
    public static string ButtonSaveAs => AppTexts.ButtonSaveAs;

    // "Add a decryption password"
    public static string DialogTextAddPassword => AppTexts.DialogTextAddPassword;

    // "Decrypt and open failed"
    public static string DialogTextOpenEncryptedFileFailed => AppTexts.DialogTextOpenFailed;

    // "Canceled"
    public static string DialogTextOperationNotCompleted => AppTexts.FilesCanceledMessage;

    // "Encrypt copy to share"
    public static string DialogTitleEncryptCopyFor => AppTexts.DialogTitleEncryptCopyFor;

    // "Setup a local profile"
    public static string HeadingCreateUserFirstTimeSetup => AppTexts.HeadingCreateUserFirstTimeSetup;

    // "About"
    public static string MenuAbout => AppTexts.ButtonAbout;

    // "Help"
    public static string MenuHelp => ButtonHelp;

    // "Sign in to Xecrets Ez"
    public static string SignInHeading => AppTexts.SignInHeading;

    // "Adding a password is only for decryption, when you receive files from someone else or you have used another
    // password previously. It does not affect encryption."
    public static string ToolTipAddPassword => AppTexts.ToolTipAddPassword;

    // "Not used for sending emails. It's an identifier for your local profile in the software and for sharing with others."
    public static string ToolTipEmail => AppTexts.ToolTipEmail;

    // "Your master password. Make it strong, write it down and keep it safe."
    public static string ToolTipMasterPassword => AppTexts.ToolTipMasterPassword;

    // "Signing in ensures you are using the correct password for encryption, and that you don't need to retype the
    // password each time. This is the master password for all encryption. The sign in is only local to the app."
    public static string ToolTipSignIn => AppTexts.ToolTipSignIn;

    // "Confirm password"
    public static string WatermarkConfirmPassword => AppTexts.WatermarkConfirmPassword;

    // "Email"
    public static string WatermarkEmail => AppTexts.WatermarkEmail;

    // "Password"
    public static string WatermarkPassword => AppTexts.WatermarkPassword;

    // "Password to share"
    public static string WatermarkPasswordShare => AppTexts.WatermarkPasswordShare;

    #endregion Translated texts
}
