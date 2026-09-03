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

using Xecrets.Texts;

using AppTexts = Xecrets.Texts.Texts;

namespace Xecrets.Mobile.Models.Utilities;

public static class MobileTexts
{
    #region Untranslated texts

    public static string AboutPageThirdPartyNotice => "Built with .NET MAUI and CommunityToolkit.Maui. Other components are provided under their respective licenses, reproduced in full under Third-Party Licenses in the menu. All third-party product names and trademarks are the property of their respective owners. Their use does not imply affiliation, sponsorship or endorsement.";

    // Not AppTexts.ButtonClose, that is "Close all" and this closes the current file only.
    public static string LabelClose => "Close";

    public static string LabelContinue => "Continue";

    // The breadcrumb segments, here and in the translated region below, are joined by
    // BreadcrumbSeparator to form the trail shown at the top of the pages that are shared between
    // the encrypt and decrypt flows.
    public static string BreadcrumbCopyToShare => "Copy to share";

    public static string BreadcrumbHome => "Home";

    // "My folders"
    public static string BreadcrumbMyFolders => WorkFoldersContentTitle;

    public static string BreadcrumbPreview => "Preview";

    public static string BreadcrumbReceivedFile => "Received file";

    public static string BreadcrumbResult => "Result";

    public static string BreadcrumbSeparator => " › ";

    public static string ButtonDecryptAs => "Decrypt and…";

    public static string ButtonEncryptAs => "Encrypt and…";

    public static string ButtonEncryptToShare => "Encrypt to… and…";

    public static string ButtonOpenIn => "Open in…";

    public static string ButtonReport => "Report";

    public static string ButtonSave => "Save";

    public static string ButtonSendTo => "Send to…";

    public static string ButtonView => "View";

    public static string CrashPageAppleAdditionalInformation => "Apple may have additional crash information. You can report it manually or enable Share with App Developers under Analytics & Improvements.";

    public static string CrashPageExplanation => "There was a problem. We have copied the following log to the clipboard. Use the button to report it. Paste the log in the message.";

    public static string CrashPageTitle => "Sorry, we crashed!";

    public static string WorkFolderDescription => "First add or select a folder, then a file to work with.";

    public static string DialogTextAddUnknownWorkFolder => "Do you want to add this folder?";

    public static string DialogTextFolderNoAccess => "The selected folder could not be accessed. Please select another folder.";

    public static string DialogTextSelectFolderFirst => "You selected a file in the folder. Select and add the folder first.";

    public static string DialogTextAlreadyEncrypted => "This file is already encrypted.";

    public static string DialogTextConfirmOverwrite => "The file \"{0}\" already exists. Overwrite it?";

    public static string DialogTextResultSaved => "The action has completed.";

    public static string DialogTextExceptionFormat => "An unexpected error \"{0}\" occurred.";

    public static string DialogTextFolderName => "Enter a label for this folder";

    public static string DialogTextResult => "The file is only saved locally in the app. Choose an action for what to do with it next.";

    public static string DialogTextSelfHandoffRejected => "Xecrets Ez can't handle files from itself.";

    public static string EncryptToShareDescription => "Enter a separate password for the encrypted copy. Share the password through a different channel.";

    public static string HomeContentTitle => "Actions";

    public static string HomeDescription => "Choose an action. Work with files where they are stored. Work in the app and select what to do next. Encrypt a copy with a separate password.";

    public static string MenuInfo => "Xecrets home";

    // "Third-Party Licenses"
    public static string MenuThirdPartyLicenses => ThirdPartyLicensesPageTitle;

    public static string MenuXecretsDesktop => "Xecrets desktop";

    public static string ThirdPartyLicensesPageExplanation => "The application includes the following third-party material, under the terms reproduced here.";

    // Also used as the overflow menu item text, see MenuThirdPartyLicenses.
    public static string ThirdPartyLicensesPageTitle => "Third-party Licenses";

    public static string WorkFoldersContentTitle => "My folders";

    public static string MobileHelpUrl => "https://www.axantum.com/help/mobile";

    #endregion Untranslated texts

    #region Translated texts

    // "Decrypt", without the ellipsis of ButtonDecrypt, since a breadcrumb names a place and not an
    // action that leads on to a selection.
    public static string BreadcrumbDecrypt => ButtonDecrypt.StripEllipsis();

    // "Encrypt", see BreadcrumbDecrypt.
    public static string BreadcrumbEncrypt => ButtonEncrypt.StripEllipsis();

    // "Password"
    public static string BreadcrumbPassword => WatermarkPassword;

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
    public static string ButtonSaveAs => AppTexts.ButtonSaveAs.ToSentenceCase();

    // "Add a decryption password"
    public static string DialogTextAddPassword => AppTexts.DialogTextAddPassword;

    // "Canceled"
    public static string DialogTextOperationNotCompleted => AppTexts.FilesCanceledMessage;

    // "Encrypt copy to share"
    public static string DialogTitleEncryptCopyFor => AppTexts.DialogTitleEncryptCopyFor;

    // "Set up a local profile"
    public static string HeadingCreateUserFirstTimeSetup => AppTexts.HeadingCreateUserFirstTimeSetup;

    // "About"
    public static string MenuAbout => AppTexts.ButtonAbout;

    // "Help"
    public static string MenuHelp => ButtonHelp;

    // "Sign in to Xecrets Ez"
    public static string SignInHeading => AppTexts.SignInHeading;

    // "Adding a password is only for decryption, when you receive files from someone else, or you have used another
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
