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

using System.Text;

using NUnit.Framework;

using Xecrets.Common.Abstractions;
using Xecrets.Common.Implementation;
using Xecrets.Common.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Data;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class MobileDataStoreTests
{
    private string _directory = null!;
    private MobileDataStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"xecrets-mobile-test-{Guid.NewGuid():N}");
        IProtectedPayload protectedPayload = new ProtectedPayload();
        _store = new MobileDataStore(new TestFileService(_directory), new TestCrashLogService(), TimeProvider.System, protectedPayload);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Test]
    public async Task UnchangedEditableDataDoesNotCreateTheDocument()
    {
        await using (IEditScope<ApplicationSettings> settings =
                     (await _store.OpenApplicationSettingsAsync()).BeginEdit())
        {
            Assert.That(settings.Value.Theme, Is.EqualTo("Default"));
        }

        Assert.That(File.Exists(DataPath), Is.False);
    }

    [Test]
    public async Task ResetStoreRemovesTheDocument()
    {
        await _store.CreateUserAsync(NewUser("a@example.com", "A", 1));

        Assert.That(File.Exists(DataPath), Is.True);

        await _store.ResetStoreAsync();

        Assert.That(File.Exists(DataPath), Is.False);
        Assert.That(await _store.GetUsersAsync(), Is.Empty);
    }

    [Test]
    public async Task MultipleUsersHaveStableDisambiguatedNames()
    {
        await _store.CreateUserAsync(NewUser("same@example.com", "Same", 1));
        await _store.CreateUserAsync(NewUser("same@example.com", "Same", 2));

        IReadOnlyList<UserSummary> users = await _store.GetUsersAsync();

        Assert.That(users.Select(user => user.DisplayName), Is.EqualTo(new[] { "Same", "Same (2)" }));
        Assert.That(users[0].Id, Is.Not.EqualTo(users[1].Id));
    }

    [Test]
    public async Task MobileDocumentUsesRelaxedJsonFormat()
    {
        await _store.CreateUserAsync(NewUser("a@example.com", "Räksmörgås", 1));

        string json = await File.ReadAllTextAsync(DataPath);

        Assert.That(json, Does.Contain("\n  \"version\""));
        Assert.That(json, Does.Contain("Räksmörgås"));
        Assert.That(json, Does.Not.Contain("openState"));
    }

    [Test]
    public async Task ExtraCredentialsAreProtectedAndNotPresentAsPlainJson()
    {
        IUserDataStore user = await _store.CreateUserAsync(NewUser("a@example.com", "A", 1));
        IXecretsProtection protection = new TestProtection();
        await using (IEditScope<ExtraCredentials> credentials =
                     (await user.LoadExtraCredentialsAsync(protection)).BeginEdit())
        {
            credentials.Value.Passwords.Add(new PasswordUsage { Password = "top-secret", UsageCount = 3 });
        }

        string json = await File.ReadAllTextAsync(DataPath);
        ExtraCredentials reloaded = (await user.LoadExtraCredentialsAsync(protection)).Value;

        Assert.That(json, Does.Not.Contain("top-secret"));
        Assert.That(reloaded.Passwords.Single().Password, Is.EqualTo("top-secret"));
        Assert.That(File.Exists($"{DataPath}.tmp"), Is.False);
    }

    [Test]
    public async Task ExtraCredentialsAreNormalizedAndTimestampedWhenSaved()
    {
        IUserDataStore user = await _store.CreateUserAsync(NewUser("a@example.com", "A", 1));
        IXecretsProtection protection = new TestProtection();

        await using (IEditScope<ExtraCredentials> credentials =
                     (await user.LoadExtraCredentialsAsync(protection)).BeginEdit())
        {
            credentials.Value.Passwords.Add(new PasswordUsage { Password = "same" });
            credentials.Value.Passwords.Add(new PasswordUsage { Password = "same", UsageCount = 3 });
            credentials.Value.Passwords.Add(new PasswordUsage { Password = "other" });
        }

        ExtraCredentials reloaded = (await user.LoadExtraCredentialsAsync(protection)).Value;

        Assert.That(reloaded.Passwords.Select(password => password.Password), Is.EqualTo(["same", "other"]));
        Assert.That(reloaded.Passwords[0].UsageCount, Is.Zero);
        Assert.That(reloaded.LastWriteUtc, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task MalformedDocumentResetsToNewDocumentWithoutMutatingTheFile()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(DataPath, "{ malformed");
        string before = File.ReadAllText(DataPath);

        IReadOnlyList<UserSummary> users = await _store.GetUsersAsync();

        Assert.That(users, Is.Empty);
        Assert.That(File.ReadAllText(DataPath), Is.EqualTo(before));
    }

    [Test]
    public async Task EmailChangeReplacesKeyAndImmediatelyUpdatesSummary()
    {
        IUserDataStore user = await _store.CreateUserAsync(NewUser("old@example.com", "Old", 1));
        SignInKey oldKey = (await user.GetSignInKeysAsync()).Single();
        SignInKey replacement = new("new@example.com", DateTimeOffset.UtcNow, [9]);

        await user.ReplaceSignInKeyAsync(oldKey, replacement, "new@example.com", "New");

        UserSummary summary = (await _store.GetUsersAsync()).Single();
        Assert.That(summary.Email, Is.EqualTo("new@example.com"));
        Assert.That(summary.DisplayName, Is.EqualTo("New"));
        Assert.That((await user.GetSignInKeysAsync()).Single().ProtectedBytes, Is.EqualTo(new byte[] { 9 }));
    }

    [Test]
    public async Task PackageRoundTripIncludesAllSettingsAndExcludesOpenState()
    {
        IXecretsProtection protection = new TestProtection();
        IUserDataStore source = await _store.CreateUserAsync(NewUser("a@example.com", "A", 1));
        await using (IEditScope<UserSettings> settings = (await source.LoadSettingsAsync()).BeginEdit())
        {
            settings.Value.LocalUserId = 42;
        }
        await using (IEditScope<PrivateKeyData> privateKeys =
                     (await source.LoadPrivateKeysAsync()).BeginEdit())
        {
            privateKeys.Value.Accounts.Add(new PrivateKeyAccount { Email = "a@example.com" });
        }

        UserDataPackage package = await _store.ExportUserAsync(source.Id, protection);
        IUserDataStore imported = await _store.ImportUserAsync(package, protection);
        UserSettings importedSettings = (await imported.LoadSettingsAsync()).Value;
        PrivateKeyData importedKeys = (await imported.LoadPrivateKeysAsync()).Value;
        ApplicationData stored = JsonFile.Deserialize<ApplicationData>(await File.ReadAllBytesAsync(DataPath), _ => { });
        LocalProfileData storedImported = stored.Users.Single(user => user.Id == imported.Id.Value);

        Assert.That(package.Settings.LocalUserId, Is.EqualTo(42));
        Assert.That(importedSettings.LocalUserId, Is.EqualTo(42));
        Assert.That(importedKeys.Accounts.Single().Email, Is.EqualTo("a@example.com"));
        Assert.That(storedImported.PrivateKeys.Accounts.Single().Email, Is.EqualTo("a@example.com"));
        Assert.That(storedImported.ProtectedPayloads.ContainsKey("privateKeys"), Is.False);
    }

    [Test]
    public async Task OpenFilesAndRecentFilesAreNotSupported()
    {
        IUserDataStore user = await _store.CreateUserAsync(NewUser("a@example.com", "A", 1));

        Assert.That(async () => await user.LoadOpenFilesAsync(), Throws.TypeOf<NotSupportedException>());
        Assert.That(async () => await user.LoadRecentFilesAsync(), Throws.TypeOf<NotSupportedException>());
    }

    private string DataPath => Path.Combine(_directory, "xecrets-data.json");

    private static NewUserData NewUser(string email, string name, byte marker) =>
        new(email, name, new SignInKey(email, DateTimeOffset.UtcNow, [marker]));

    private sealed class TestProtection : IXecretsProtection
    {
        public Task<byte[]> ProtectAsync(byte[] cleartext, string originalFilename) =>
            Task.FromResult(Encoding.UTF8.GetBytes(Convert.ToBase64String(cleartext)));

        public Task<byte[]> UnprotectAsync(byte[] protectedBytes) =>
            Task.FromResult(Convert.FromBase64String(Encoding.UTF8.GetString(protectedBytes)));
    }

    private sealed class TestCrashLogService : ICrashLogService
    {
        public bool HasPendingCrashLog => false;

        public void RegisterHandlers()
        {
        }

        public string ReadCurrent() => string.Empty;

        public void WriteCrashLog(string source, object? crash)
        {
        }
    }

    private sealed class TestFileService(string directory) : IFileService
    {
        public string PlatformId => "test";
        public string AppDataDirectory => directory;
        public string CacheDirectory => directory;
        public Task<PickedFile?> PickFileAsync(string pickerTitle, FilePickerKind pickerKind) => throw new NotSupportedException();
        public Task<bool> OpenInAsync(string filePath, string displayName) => throw new NotSupportedException();
        public Task SendToAsync(string filePath, string displayName, string contentType) => throw new NotSupportedException();
        public Task<SaveFileResult> SaveAsAsync(Stream stream, string displayName, string originalSourcePath) => throw new NotSupportedException();
        public Task<bool> CanViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public Task ViewFileAsync(DecryptedFileInfo file) => throw new NotSupportedException();
        public bool IsSelfHandoffReference(string reference) => false;
    }
}
