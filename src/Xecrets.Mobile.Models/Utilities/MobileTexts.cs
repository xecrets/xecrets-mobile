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

#pragma warning disable CS1591

using Xecrets.Mobile.Models.Models;
using Xecrets.Texts;

using AppTexts = Xecrets.Texts.Texts;

namespace Xecrets.Mobile.Models.Utilities;

/// <summary>
/// The texts of the app, either untranslated or from Xecrets.Texts.
/// </summary>
public static class MobileTexts
{
    #region Untranslated texts

    public static string AboutLabelPlatform => "Platform";

    public static string AboutLabelPublisher => "Publisher";

    // "Source code"
    public static string AboutLabelSourceCode => MenuGitHub;

    public static string AboutLabelVersion => "Version";

    public static string AboutPageThirdPartyNotice => "For details, see ‘Third-party licenses’ in the menu.";

    // The breadcrumb segments, here and in the translated region below, are joined by
    // BreadcrumbSeparator to form the trail shown at the top of the pages that are shared between
    // the encrypt and decrypt flows.

    // "Change local profile"
    public static string BreadcrumbChangeProfile => MenuChangeProfile;

    // "Work with a copy"
    public static string BreadcrumbCopy => HomeButtonEncryptDecryptCopy;

    // "Decryption passwords"
    public static string BreadcrumbDecryptionPasswords => DecryptionPasswordsContentTitle;

    // "Delete"
    public static string BreadcrumbDelete => HomeButtonDelete;

    // "Delete local profile"
    public static string BreadcrumbDeleteProfile => MenuDeleteProfile;

    // "Edit"
    public static string BreadcrumbEdit => EditContentTitle;

    public static string BreadcrumbEncryptedCopy => "Encrypted copy";

    public static string BreadcrumbEncryptToShare => ButtonEncryptToShare.StripEllipsis();

    // "Home"
    public static string BreadcrumbHome => HomeContentTitle;

    // "Work with the original"
    public static string BreadcrumbOriginal => HomeButtonEncryptDecryptOriginal;

    // "Preview"
    public static string BreadcrumbPreview => PreviewContentTitle;

    public static string BreadcrumbReceivedFile => "Received file";

    // "Recent files"
    public static string BreadcrumbRecentFiles => RecentFilesContentTitle;

    public static string BreadcrumbSeparator => " › ";

    // "View"
    public static string BreadcrumbView => ViewContentTitle;

    public static string ButtonBrowseElsewhere => "Browse anywhere";

    public static string ButtonDelete => "Delete";

    public static string ButtonDiscard => "Discard";

    public static string ButtonEncryptToShare => "Encrypt to share";

    public static string ButtonOpenIn => "Open in…";

    public static string ButtonReport => "Report";

    public static string ButtonSave => "Save";

    public static string ButtonSendTo => "Send to…";

    public static string ButtonView => "View";

    public static string ChangeProfileDescription => "Change the email or password of your local profile. You can still open files you encrypted earlier.";

    public static string CrashPageAppleAdditionalInformation => "Apple may have more details. To share them with us automatically, turn on ‘Share With App Developers’ in ‘Analytics & Improvements’.";

    public static string CrashPageExplanation => "The details below have been copied to the clipboard. Choose ‘Report’ and paste them into the message to help us fix the problem.";

    public static string CrashPageTitle => "Sorry, something went wrong";

    public static string DecryptionPasswordsContentTitle => "Decryption passwords";

    public static string DecryptionPasswordsDescription => "Extra passwords for decrypting files from others or encrypted with an earlier password. They’re tried automatically and are never used for encrypting. Use the add and delete buttons to manage them.";

    public static string DialogTextAccessDeniedOrNotSupported => "The app isn’t allowed to change the file.";

    public static string DialogTextAllowAccessToFolder => "Allow the app to access the folder?";

    public static string DialogTextAlreadyEncryptedFormat => "The file “{0}” is already encrypted.";

    public static string DialogTextBackupNameInUse => "Not saved, the folder already has a file named “{0}”. Remove it, and try again.";

    public static string DialogTextCanceled => "Canceled.";

    public static string DialogTextCloudVersionsMayRemain => "Earlier versions or the trash may remain in the cloud.";

    public static string DialogTextConfirmCancelOperation => "Cancel the operation?";

    public static string DialogTextConfirmDecryptFiles => "Decrypt the files?";

    public static string DialogTextConfirmDeleteFiles => "Delete the files?";

    public static string DialogTextConfirmEncryptFiles => "Encrypt the files?";

    public static string DialogTextConfirmOverwrite => "“{0}” already exists. Replace it?";

    public static string DialogTextCopiedToClipboard => "Copied to the clipboard.";

    public static string DialogTextDone => "Done.";

    public static string DialogTextExceptionFormat => "An unexpected error “{0}” occurred.";

    public static string DialogTextFileCountAndSizeFormat => "Number of files: {0}. Total size: {1}.";

    public static string DialogTextFileDecrypted => "The file is decrypted.";

    public static string DialogTextFileDeleted => "The file is deleted, but not overwritten.";

    // "The file is deleted, but not overwritten. Earlier versions or the trash may remain in the cloud."
    public static string DialogTextFileDeletedInCloud => $"{DialogTextFileDeleted} {DialogTextCloudVersionsMayRemain}";

    public static string DialogTextFileEncrypted => "The file is encrypted.";

    public static string DialogTextFileNotAccessibleNow => "The file can’t be found, or isn’t available right now.";

    public static string DialogTextFileOverwritten => "The file is overwritten and deleted.";

    public static string DialogTextFileSaved => "The file is saved.";

    public static string DialogTextFilesDecryptedFormat => "Files decrypted: {0}.";

    public static string DialogTextFilesDeletedFormat => "Files deleted: {0}.";

    public static string DialogTextFilesEncryptedFormat => "Files encrypted: {0}.";

    public static string DialogTextFolderName => "Enter a label for the folder";

    public static string DialogTextFolderNoAccess => "The app can’t access the folder. Choose another one.";

    public static string DialogTextIncomingFileAccessDenied => "The app isn’t allowed to open the file.";

    public static string DialogTextInterruptedSaveFormat =>
        "An operation on “{0}” was interrupted. Check the file, and do it again if needed.";

    public static string DialogTextNoAppToOpenFile => "There is no app to open the file.";

    public static string DialogTextNotEncryptedFormat => "The file “{0}” isn’t encrypted.";

    public static string DialogTextOriginalNotRemoved => "Done, but the original file can’t be removed.";

    public static string DialogTextProfileChanged => "Your local profile is changed.";

    public static string DialogTextResult => "The file is only kept temporarily in the app. Choose what to do with it.";

    public static string DialogTextSaveChanges => "Save changes before leaving?";

    public static string DialogTextSavedAs => "The file is saved as “{0}”.";

    public static string DialogTextSelectFolderFirst => "You chose a file. Choose the folder that contains it instead.";

    public static string DialogTextSelfHandoffRejected => "The app can’t open files from itself.";

    // "Edit"
    public static string EditContentTitle => ButtonEdit;

    public static string EditDescription => "Edit the text. Your changes are encrypted when you save.";

    // "Password"
    public static string EncryptToShareContentTitle => WatermarkPassword;

    public static string EncryptToShareDescription => "Enter a new password for the encrypted copy. Send the password by a different means than the file.";

    public static string FileSizeFormat => "{0} bytes";

    public static string FileSizeGigabytesFormat => "{0} GB";

    public static string FileSizeKilobytesFormat => "{0} KB";

    public static string FileSizeMegabytesFormat => "{0} MB";

    public static string FileStateAll => "All";

    public static string FileStateDecrypted => "Decrypted";

    public static string FileStateEncrypted => "Encrypted";

    // An ellipsis rather than a word, to save space in the segmented control.
    public static string FileStateOther => "…";

    public static string GitHubUrl => "https://github.com/xecrets/xecrets-mobile";

    // "Delete"
    public static string HomeButtonDelete => ButtonDelete;

    public static string HomeButtonEncryptDecryptCopy => "Work with a copy";

    public static string HomeButtonEncryptDecryptOriginal => "Work with the original";

    // Also used as the breadcrumb, see BreadcrumbHome.
    public static string HomeContentTitle => "Home";

    public static string HomeDescription => "Choose an action. Work on the original file where it’s stored, or on a copy in the app. To share a file, encrypt a copy with a new password.";

    public static string HomeDetailCopy => "Encrypt or decrypt in the app";

    public static string HomeDetailDelete => "Overwrite first when possible";

    public static string HomeDetailRecentFiles => "Quickly work with recent files";

    public static string HomeDetailReplace => "Encrypt or decrypt in place";

    public static string HomeDetailShare => "Encrypt a copy with a new password";

    public static string LabelContinue => "Continue";

    public static string MenuChangeProfile => "Change local profile";

    public static string MenuDecryptFolderAndSubfolderFiles => "Decrypt folder and subfolders";

    public static string MenuDecryptFolderFiles => "Decrypt folder";

    // "Decryption passwords"
    public static string MenuDecryptionPasswords => DecryptionPasswordsContentTitle;

    public static string MenuDeleteFolderAndSubfolderFiles => "Delete folder and subfolders";

    public static string MenuDeleteFolderFiles => "Delete folder";

    public static string MenuDeleteProfile => "Delete local profile";

    public static string MenuEditLabel => "Edit label";

    public static string MenuEncryptFolderAndSubfolderFiles => "Encrypt folder and subfolders";

    public static string MenuEncryptFolderFiles => "Encrypt folder";

    // Also used as the label on the About page, see AboutLabelSourceCode.
    public static string MenuGitHub => "Source code";

    public static string MenuInfo => "Xecrets website";

    // "Recent files"
    public static string MenuRecentFiles => RecentFilesContentTitle;

    public static string MenuRemoveFromList => "Remove from list";

    public static string MenuSignOut => "Sign out";

    public static string MenuSupport => "Support";

    // "Third-party licenses"
    public static string MenuThirdPartyLicenses => ThirdPartyLicensesPageTitle;

    public static string MenuWelcome => "Welcome";

    public static string MenuXecretsDesktop => $"{DisplayNameProgram} for desktop";

    public static string MobileHelpUrl => "https://www.axantum.com/help/mobile";

    public static string PreviewContentTitle => "Preview";

    public static string RecentFileNameUnknown => "(unknown)";

    public static string RecentFilesContentTitle => "Recent files";

    public static string RecentFilesDescription => "Recently used files. Choose the icon for actions, or the name to preview. Press and hold to see the full name and location. Use the add button to add a file from ‘My folders’.";

    public static string SuggestPasswordDescription => "Strong passwords that are easy to type and remember. Use » for a new suggestion, and the copy button to copy it.";

    public static string ThirdPartyLicensesPageExplanation => "The app includes the following third-party software, used under these licenses.";

    // Also used as the overflow menu item text, see MenuThirdPartyLicenses.
    public static string ThirdPartyLicensesPageTitle => "Third-party licenses";

    public static string VerifyPasswordDescription => "Enter your current password to confirm that it’s you.";

    public static string VerifyPasswordHeading => "Verify your password";

    // "View"
    public static string ViewContentTitle => ButtonView;

    public static string ViewDescription => "Shows the text or image in the file. Go back to do something else with it.";

    // The first line is the heading. The placeholders such as "(c)" must never be translated, and must stay at the
    // start of their line. Each is replaced at runtime by an icon, making the line a bullet. The same placeholder may be
    // used more than once.
    private static string WelcomeText =>
        $"""
        Welcome to {DisplayNameProgram}

        Encrypt and decrypt files across all major desktop and mobile platforms with {DisplayNameProgram}.

        (c) Create an entirely local profile.
        (e) Encrypt and decrypt files in place.
        (f) Allow access to favorite folders.
        (v) Decrypt to view, edit or share files.
        (s) Encrypt to send or share files.
        (p) Get strong passwords that are easy to use.
        (w) Delete files and overwrite when possible.
        (o) Always offline.

        …and more!

        Note: Deleted cloud files may remain in the trash or in earlier versions.
        """;

    public static string WorkFolderDescription => "Add a folder to give the app access to it. Then choose a folder to pick a file there, or use ‘Browse anywhere’ for a file somewhere else.";

    public static string WorkFolderIntentCopy => "Choose a file to work with as a copy.";

    public static string WorkFolderIntentDelete => "Choose a file to overwrite and delete.";

    public static string WorkFolderIntentShare => "Choose a file to encrypt with a new password for sharing.";

    public static string WorkFoldersContentTitle => "My folders";

    #endregion Untranslated texts

    /// <summary>
    /// Splits the welcome text into its lines. A line that starts with a placeholder of the icon map is a bullet line
    /// with the icon of the placeholder, and any other line is a paragraph.
    /// </summary>
    /// <param name="iconMap">The icon glyphs by placeholder, such as "(c)".</param>
    /// <returns>The lines of the welcome text, without empty lines.</returns>
    internal static IReadOnlyList<WelcomeLine> WelcomeLines(IReadOnlyDictionary<string, string> iconMap) =>
    [
        .. WelcomeText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => ToWelcomeLine(line, iconMap)),
    ];

    private static WelcomeLine ToWelcomeLine(string line, IReadOnlyDictionary<string, string> iconMap)
    {
        foreach ((string placeholder, string icon) in iconMap)
        {
            if (line.StartsWith(placeholder, StringComparison.Ordinal))
            {
                return new WelcomeLine(icon, line[placeholder.Length..].Trim());
            }
        }

        return new WelcomeLine("", line);
    }

    #region Translated texts

    public static string AboutDecryptionPasswords(int count) => AppTexts.DecryptionPasswordsAbout(count);

    // "License"
    public static string AboutLabelLicense => AppTexts.ButtonLicense;

    public static string AboutPrivateKeys(int decryptable, int total) => AppTexts.PrivateKeysAbout(decryptable, total);

    // "About"
    public static string BreadcrumbAbout => MenuAbout;

    // "Password"
    public static string BreadcrumbPassword => WatermarkPassword;

    // "Suggest password"
    public static string BreadcrumbSuggestPassword => MenuSuggestPassword;

    // "Edit"
    public static string ButtonEdit => AppTexts.ButtonEdit;

    // "Help", also used as the overflow menu item text, see MenuHelp.
    public static string ButtonHelp => AppTexts.ButtonHelp;

    // "Ok"
    public static string ButtonOk => AppTexts.LabelOk;

    // "Reset!"
    public static string ButtonReset => AppTexts.ButtonReset;

    // "Save As…"
    public static string ButtonSaveAs => AppTexts.ButtonSaveAs.ToSentenceCase();

    public static string DialogTextInsufficientRights => AppTexts.DialogTextInsufficientRights;

    // "Canceled"
    public static string DialogTextOperationNotCompleted => AppTexts.FilesCanceledMessage;

    public static string DialogTextWrongPasswordOpen => AppTexts.DialogTextWrongPasswordOpen;

    public static string DialogValidationAlreadySignedIn => AppTexts.DialogValidationAlreadySignedIn;

    public static string DialogValidationConfirmPassword => AppTexts.DialogValidationConfirmPassword;

    public static string DialogValidationInvalidEmail => AppTexts.DialogValidationInvalidEmail;

    public static string DialogValidationWrongPassword => AppTexts.DialogValidationWrongPassword;

    public static string DisplayNameProgram => AppTexts.DisplayNameProgram;

    // "Don't show this again"
    public static string DontShowAgain => AppTexts.DontShowAgain;

    public static string FileEncryptionUrl => AppTexts.FileEncryptionUrl;

    // "Set up a local profile"
    public static string HeadingCreateUserFirstTimeSetup => AppTexts.HeadingCreateUserFirstTimeSetup;

    // "Reset password!"
    public static string HeadingEmergencyReset => AppTexts.HeadingEmergencyReset;

    public static string LabelCancel => AppTexts.LabelCancel;

    public static string LabelNo => AppTexts.LabelNo;

    public static string LabelYes => AppTexts.LabelYes;

    // "About"
    public static string MenuAbout => AppTexts.ButtonAbout;

    public static string MenuDebug => AppTexts.MenuDebug;

    // "Help"
    public static string MenuHelp => ButtonHelp;

    // "Suggest password", also used as the page title.
    public static string MenuSuggestPassword => AppTexts.DialogTitleSuggestPassword;

    // "This operation cannot be undone.", confirming any operation that cannot be undone, not only wiping.
    public static string MessageTextConfirmWipe => AppTexts.MessageTextConfirmWipe;

    // "Forgot your password?", offering the password reset after repeated wrong passwords when signing in.
    public static string MessageTextForgotPassword => AppTexts.MessageTextForgotPassword;

    // "Sign in to Xecrets Ez"
    public static string SignInHeading => AppTexts.SignInHeading;

    public static string SiteUrl => AppTexts.SiteUrl;

    // "Not used for sending emails. It's an identifier for your local profile in the software and for sharing with others."
    public static string ToolTipEmail => AppTexts.ToolTipEmail;

    // "Set a new master password. Warning! The old password is still needed to decrypt previously encrypted files!"
    public static string ToolTipEmergencyReset => AppTexts.ToolTipEmergencyReset;

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

    public static string XecretsHelpUrl => AppTexts.XecretsHelpUrl();

    public static string XecretsSupportUrl => AppTexts.XecretsSupportUrl;

    #endregion Translated texts
}
