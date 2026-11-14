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

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using Xecrets.Common.Abstractions;
using Xecrets.Common.Implementation;
using Xecrets.Common.Models;
using Xecrets.Core.Abstractions;
using Xecrets.Core.Models;
using Xecrets.Core.Public;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Data;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class ProfileServiceTests
{
    private static readonly ICoreServices CoreServices = CreateCoreServices();

    private string _directory = null!;
    private MobileDataStore _store = null!;
    private ProfileSession _session = null!;
    private ProfileService _profileService = null!;
    private FakeEzLogging _log = null!;

    [SetUp]
    public async Task SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"xecrets-mobile-test-{Guid.NewGuid():N}");
        _log = new FakeEzLogging();
        _store = new MobileDataStore(
            new MobileDataStoreTests.TestFileService(_directory),
            new MobileDataStoreTests.TestCrashLogService(),
            new FakeEzLogging(),
            TimeProvider.System,
            new ProtectedPayload());
        _session = new ProfileSession();
        _profileService = new ProfileService(CoreServices, _store, new ProfileStore(_store), _session, _log);

        await _profileService.CreateProfileAsync("old@example.com", "old password");
        await _profileService.LoginAsync("old password");
        _log.Clear();
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
    public async Task ChangeProfileChangesEmailAndPassword()
    {
        string publicKeyTag = _profileService.GetPublicKey().Tag!;

        ProfileActionResult result = await _profileService.ChangeProfileAsync("new@example.com", "new password");

        Assert.That(result.Status, Is.EqualTo(ProfileActionStatus.Success));
        Assert.That(_profileService.CurrentEmail, Is.EqualTo("new@example.com"));
        Assert.That(_profileService.CurrentPassword, Is.EqualTo("new password"));
        Assert.That((await _store.GetUsersAsync()).Single().Email, Is.EqualTo("new@example.com"));

        _profileService.SignOut();
        Assert.That((await _profileService.LoginAsync("old password")).Status, Is.EqualTo(ProfileActionStatus.WrongPassword));
        Assert.That((await _profileService.LoginAsync("new password")).Status, Is.EqualTo(ProfileActionStatus.Success));
        Assert.That(_profileService.CurrentEmail, Is.EqualTo("new@example.com"));
        Assert.That(_profileService.GetPublicKey().Tag, Is.EqualTo(publicKeyTag));
    }

    [Test]
    public async Task ChangeProfileKeepsOldPasswordAsExtraPassword()
    {
        await _profileService.RecordExtraPasswordUseAsync("extra password");

        await _profileService.ChangeProfileAsync("old@example.com", "new password");
        _profileService.SignOut();
        await _profileService.LoginAsync("new password");

        Assert.That(
            _profileService.GetExtraPasswords().Select(extraPassword => extraPassword.Password),
            Is.EquivalentTo(new[] { "extra password", "old password", }));
    }

    [Test]
    public async Task ChangeProfileWithSamePasswordAddsNoExtraPassword()
    {
        await _profileService.ChangeProfileAsync("new@example.com", "old password");
        _profileService.SignOut();
        await _profileService.LoginAsync("old password");

        Assert.That(_profileService.CurrentEmail, Is.EqualTo("new@example.com"));
        Assert.That(_profileService.GetExtraPasswords(), Is.Empty);
    }

    [Test]
    public async Task AddedExtraPasswordIsKeptOnceAndNotCountedAsUsed()
    {
        await _profileService.RecordExtraPasswordUseAsync("used password");
        await _profileService.AddExtraPasswordAsync("added password");
        await _profileService.AddExtraPasswordAsync("added password");
        _profileService.SignOut();
        await _profileService.LoginAsync("old password");

        Assert.That(
            _profileService.GetExtraPasswords()
                .Select(extraPassword => (extraPassword.Password, extraPassword.UsageCount)),
            Is.EqualTo(new[] { ("used password", 1), ("added password", 0), }));
    }

    [Test]
    public async Task AddingAnExtraPasswordAlreadyKeptDoesNotChangeItsUse()
    {
        await _profileService.RecordExtraPasswordUseAsync("password");

        await _profileService.AddExtraPasswordAsync("password");

        Assert.That(_profileService.GetExtraPasswords().Single().UsageCount, Is.EqualTo(1));
    }

    [Test]
    public async Task AddedExtraPasswordReplacesTheLastWhenFull()
    {
        for (int i = 0; i < 25; ++i)
        {
            await _profileService.RecordExtraPasswordUseAsync($"password {i}");
        }

        await _profileService.AddExtraPasswordAsync("added password");

        Assert.That(_profileService.GetExtraPasswords(), Has.Count.EqualTo(25));
        Assert.That(_profileService.GetExtraPasswords()[^1].Password, Is.EqualTo("added password"));
        Assert.That(
            _profileService.GetExtraPasswords().Select(extraPassword => extraPassword.Password),
            Does.Not.Contain("password 24"));
    }

    [Test]
    public async Task RemovedExtraPasswordIsNoLongerKept()
    {
        await _profileService.RecordExtraPasswordUseAsync("kept password");
        await _profileService.RecordExtraPasswordUseAsync("removed password");

        await _profileService.RemoveExtraPasswordAsync("removed password");
        _profileService.SignOut();
        await _profileService.LoginAsync("old password");

        Assert.That(
            _profileService.GetExtraPasswords().Select(extraPassword => extraPassword.Password),
            Is.EqualTo(new[] { "kept password", }));
    }

    [Test]
    public async Task ChangeProfileRejectsInvalidEmail()
    {
        ProfileActionResult result = await _profileService.ChangeProfileAsync("not an email", "new password");

        Assert.That(result.Status, Is.EqualTo(ProfileActionStatus.InvalidEmail));
        _profileService.SignOut();
        Assert.That((await _profileService.LoginAsync("old password")).Status, Is.EqualTo(ProfileActionStatus.Success));
        Assert.That(_profileService.CurrentEmail, Is.EqualTo("old@example.com"));
    }

    [Test]
    public async Task SignInKeepsTheProfileKeyPairAmongThePrivateKeys()
    {
        IUserDataStore userStore = await _store.OpenUserAsync((await _store.GetUsersAsync()).Single().Id);

        PrivateKeyData privateKeys = (await userStore.LoadPrivateKeysAsync()).Value;

        Assert.That(privateKeys.Accounts.SelectMany(account => account.Keys).Single().Email,
            Is.EqualTo("old@example.com"));
    }

    [Test]
    public async Task TheProfileKeyPairIsNotCountedAsAnExtraPrivateKey()
    {
        PrivateKeyCounts counts = await _profileService.GetPrivateKeyCountsAsync();

        Assert.That(counts, Is.EqualTo(new PrivateKeyCounts(Decryptable: 0, Total: 0)));
    }

    [Test]
    public async Task AfterResetProfileTheOldKeyPairIsCountedAsDecryptableOnceTheOldPasswordIsAdded()
    {
        _profileService.SignOut();
        await _profileService.ResetProfileAsync("new password");
        await _profileService.LoginAsync("new password");
        Assert.That(await _profileService.GetPrivateKeyCountsAsync(),
            Is.EqualTo(new PrivateKeyCounts(Decryptable: 0, Total: 1)));

        await _profileService.AddExtraPasswordAsync("old password");

        Assert.That(await _profileService.GetPrivateKeyCountsAsync(),
            Is.EqualTo(new PrivateKeyCounts(Decryptable: 1, Total: 1)));
    }

    [Test]
    public async Task ResetProfileSignsOutAndOnlyTheNewPasswordSignsIn()
    {
        _profileService.SignOut();

        await _profileService.ResetProfileAsync("new password");

        Assert.That(_profileService.IsAuthenticated, Is.False);
        Assert.That((await _profileService.LoginAsync("old password")).Status, Is.EqualTo(ProfileActionStatus.WrongPassword));
        Assert.That((await _profileService.LoginAsync("new password")).Status, Is.EqualTo(ProfileActionStatus.Success));
        Assert.That(_profileService.CurrentEmail, Is.EqualTo("old@example.com"));
    }

    [Test]
    public async Task ResetProfileClearsTheExtraPasswords()
    {
        await _profileService.AddExtraPasswordAsync("extra password");
        _profileService.SignOut();

        await _profileService.ResetProfileAsync("new password");
        await _profileService.LoginAsync("new password");
        Assert.That(_profileService.GetExtraPasswords(), Is.Empty);

        await _profileService.AddExtraPasswordAsync("added password");
        _profileService.SignOut();
        await _profileService.LoginAsync("new password");
        Assert.That(
            _profileService.GetExtraPasswords().Select(extraPassword => extraPassword.Password),
            Is.EqualTo(new[] { "added password", }));
    }

    [Test]
    public async Task SignInThatFailsToSaveThePrivateKeysLeavesTheProfileSignedOut()
    {
        _profileService.SignOut();
        // With a new key pair, signing in saves it among the private keys.
        await _profileService.ResetProfileAsync("new password");
        BlockSaving();

        Assert.That(async () => await _profileService.LoginAsync("new password"), Throws.Exception);
        Assert.That(_profileService.IsAuthenticated, Is.False);
    }

    [Test]
    public async Task AfterResetProfileTheOldPasswordAddedOpensFilesForTheOldKeyPairFromThenOn()
    {
        byte[] encrypted = await EncryptForAsync(_profileService.GetPublicKey());
        _profileService.SignOut();
        await _profileService.ResetProfileAsync("new password");
        await _profileService.LoginAsync("new password");
        Assert.That(await IsDecryptableAsync(encrypted), Is.False);

        await _profileService.AddExtraPasswordAsync("old password");
        Assert.That(await IsDecryptableAsync(encrypted), Is.True);

        await _profileService.RemoveExtraPasswordAsync("old password");
        _profileService.SignOut();
        await _profileService.LoginAsync("new password");
        Assert.That(await IsDecryptableAsync(encrypted), Is.True);
    }

    [Test]
    public async Task AfterResetProfileTheOldPasswordFirstUsedOpensFilesForTheOldKeyPairAtOnce()
    {
        byte[] encrypted = await EncryptForAsync(_profileService.GetPublicKey());
        _profileService.SignOut();
        await _profileService.ResetProfileAsync("new password");
        await _profileService.LoginAsync("new password");

        await _profileService.RecordExtraPasswordUseAsync("old password");

        Assert.That(await IsDecryptableAsync(encrypted), Is.True);
    }

    [Test]
    public async Task ChangeProfileKeepsOpeningFilesForAnOldKeyPairAfterResetProfile()
    {
        byte[] encrypted = await EncryptForAsync(_profileService.GetPublicKey());
        _profileService.SignOut();
        await _profileService.ResetProfileAsync("new password");
        await _profileService.LoginAsync("new password");
        await _profileService.AddExtraPasswordAsync("old password");
        await _profileService.RemoveExtraPasswordAsync("old password");

        await _profileService.ChangeProfileAsync("old@example.com", "newer password");
        await _profileService.RemoveExtraPasswordAsync("new password");
        _profileService.SignOut();
        await _profileService.LoginAsync("newer password");

        Assert.That(_profileService.GetExtraPasswords(), Is.Empty);
        Assert.That(await IsDecryptableAsync(encrypted), Is.True);
    }

    [Test]
    public async Task ResetProfileIsLoggedWithoutThePassword()
    {
        _profileService.SignOut();

        await _profileService.ResetProfileAsync("new password");

        Assert.That(_log.Entries, Is.EqualTo(new[]
        {
            (EzLogLevel.Information, "Password reset for old@example.com, new sign-in key, extra passwords cleared"),
        }));
    }

    [Test]
    public async Task ChangeProfileIsLoggedWithTheEmailsAndTheOldPasswordKeptWithoutThePasswords()
    {
        await _profileService.ChangeProfileAsync("new@example.com", "new password");

        Assert.That(_log.Texts, Does.Contain("Profile changed from old@example.com to new@example.com, password changed"));
        Assert.That(_log.Texts, Does.Contain("Extra password added from profile change, 1 in list"));
        AssertNoPasswordLogged("old password", "new password");
    }

    [Test]
    public async Task ChangeProfileWithTheSameEmailAndPasswordIsLogged()
    {
        await _profileService.ChangeProfileAsync("old@example.com", "old password");

        Assert.That(_log.Texts, Does.Contain("Profile changed for old@example.com, password unchanged"));
        Assert.That(_log.Texts, Has.None.StartsWith("Extra password"));
    }

    [Test]
    public async Task ChangesToTheExtraPasswordsAreLoggedButNotTheirUse()
    {
        await _profileService.RecordExtraPasswordUseAsync("used password");
        await _profileService.RecordExtraPasswordUseAsync("used password");
        await _profileService.AddExtraPasswordAsync("added password");
        await _profileService.RemoveExtraPasswordAsync("used password");

        Assert.That(_log.Entries.Where(entry => entry.Text.StartsWith("Extra password", StringComparison.Ordinal)),
            Is.EqualTo(new[]
            {
                (EzLogLevel.Information, "Extra password added on use, 1 in list"),
                (EzLogLevel.Information, "Extra password added in advance, 2 in list"),
                (EzLogLevel.Information, "Extra password removed, 1 in list"),
            }));
        AssertNoPasswordLogged("used password", "added password");
    }

    [Test]
    public async Task DroppedExtraPasswordsAreLogged()
    {
        for (int i = 0; i < 25; ++i)
        {
            await _profileService.RecordExtraPasswordUseAsync($"password {i}");
        }
        _log.Clear();

        await _profileService.AddExtraPasswordAsync("added password");

        Assert.That(_log.Texts, Does.Contain("Least used extra passwords dropped: 1"));
        Assert.That(_log.Texts, Does.Contain("Extra password added in advance, 25 in list"));
    }

    [Test]
    public async Task UpdatedPrivateKeysAreLoggedWithTheKeyPairsLoaded()
    {
        _profileService.SignOut();
        await _profileService.ResetProfileAsync("new password");
        _log.Clear();

        await _profileService.LoginAsync("new password");

        Assert.That(_log.Entries, Is.EqualTo(new[]
        {
            (EzLogLevel.Information, "Stored private keys updated, 1 keys before, 2 after, none loaded"),
        }));

        // The old password decrypts the key pair kept from before the reset, which is then re-encrypted.
        _log.Clear();
        await _profileService.AddExtraPasswordAsync("old password");

        Assert.That(_log.Entries, Has.Exactly(1).Matches<(EzLogLevel Level, string Text)>(entry =>
            entry.Level == EzLogLevel.Information &&
            entry.Text.StartsWith("Stored private keys updated, 2 keys before, 2 after, loaded old@example.com created ", StringComparison.Ordinal)));
        AssertNoPasswordLogged("old password", "new password");
    }

    private void AssertNoPasswordLogged(params string[] passwords)
    {
        foreach (string password in passwords)
        {
            Assert.That(_log.Texts, Has.None.Contains(password));
        }
    }

    // Every file is saved by writing a temporary file next to it first, which fails where a directory has its name.
    private void BlockSaving()
    {
        foreach (string path in Directory.GetFiles(_directory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory($"{path}.tmp");
        }
    }

    private static async Task<byte[]> EncryptForAsync(PublicKey publicKey)
    {
        DateTime utcNow = DateTime.UtcNow;
        using MemoryStream encrypted = new();
        await CoreServices.EncryptAsync(
            new MemoryStream([1, 2, 3]),
            encrypted,
            new EncryptRequest("file password", [publicKey], [], "a.txt", utcNow, utcNow, utcNow, true,
                new Progress<Progress>(_ => { })));
        return encrypted.ToArray();
    }

    private async Task<bool> IsDecryptableAsync(byte[] encrypted)
    {
        using IDecryptionSession session = await CoreServices.OpenDecryptionAsync(
            new MemoryStream(encrypted),
            new DecryptRequest([_profileService.GetIdentity()], new Progress<Progress>(_ => { })));
        return session.IsDecryptable;
    }

    private static ICoreServices CreateCoreServices()
    {
        ServiceCollection services = new();
        services.AddXecretsCore();
        return services.BuildServiceProvider().GetRequiredService<ICoreServices>();
    }
}
