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

using System.Diagnostics.CodeAnalysis;
using System.Text;

using Xecrets.Core.Abstractions;
using Xecrets.Core.Models;
using Xecrets.Core.Public;

namespace Xecrets.Mobile.Models.Test;

/// <summary>
/// Encrypted contents start with "ENC:", followed by the name of the file encrypted and a line break, and then the
/// contents of that file. The progress is reported once the contents are written, as the core reports it as they are.
/// </summary>
internal sealed class FakeCoreServices : ICoreServices
{
    private const string _marker = "ENC:";

    /// <summary>
    /// The password a file can only be decrypted with, when not null.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// The password a file of the original name can only be decrypted with, instead of <see cref="Password"/>.
    /// </summary>
    public Dictionary<string, string> PasswordsByName { get; } = [];

    /// <summary>
    /// What any file that is decrypted was encrypted with.
    /// </summary>
    public EncryptedWithParameters EncryptedWith { get; set; } = EncryptedWithParameters.Empty;

    public List<EncryptRequest> EncryptRequests { get; } = [];

    /// <summary>
    /// Done when the contents are written while encrypting or decrypting, before the progress is reported.
    /// </summary>
    public Action? Writing { get; set; }

    /// <summary>
    /// Done when a file is opened to be decrypted, before it is known whether it can be.
    /// </summary>
    public Action? Opening { get; set; }

    public async Task<bool> IsEncryptedAsync(Func<Task<Stream>> openReadAsync)
    {
        await using Stream stream = await openReadAsync();
        return (await new StreamReader(stream).ReadToEndAsync()).StartsWith(_marker, StringComparison.Ordinal);
    }

    public async Task EncryptAsync(Stream cleartext, Stream encrypted, EncryptRequest request)
    {
        EncryptRequests.Add(request);
        string contents = await new StreamReader(cleartext).ReadToEndAsync();
        byte[] bytes = Encoding.UTF8.GetBytes($"{_marker}{request.OriginalFileName}\n{contents}");
        await encrypted.WriteAsync(bytes);
        ReportWritten(request.Progress, bytes.Length);
    }

    public async Task<IDecryptionSession> OpenDecryptionAsync(Stream encrypted, DecryptRequest request)
    {
        Opening?.Invoke();
        string contents = await new StreamReader(encrypted).ReadToEndAsync();
        if (!contents.StartsWith(_marker, StringComparison.Ordinal))
        {
            throw new XecretsCoreException("No magic Guid was found.", ErrorCode.MagicGuidMissing,
                new InvalidDataException());
        }

        string[] parts = contents[_marker.Length..].Split('\n', 2);
        string? password = PasswordsByName.GetValueOrDefault(parts[0]) ?? Password;
        if (password is not null && request.Identities.All(identity => identity.Passphrase != password))
        {
            return new FakeDecryptionSession(
                this, request, false, string.Empty, string.Empty, EncryptedWithParameters.Empty);
        }

        return new FakeDecryptionSession(this, request, true, parts[0], parts[1], EncryptedWith);
    }

    private void ReportWritten(IProgress<Progress> progress, int count)
    {
        Writing?.Invoke();
        progress.Report(Progress.CountAdded(count));
    }

    public Task<KeyPair> CreateKeyPairAsync(string email, string passphrase, DateTimeOffset createdUtc) =>
        throw new NotSupportedException();
    public Task<KeyPair> ChangeKeyPairAsync(KeyPair keyPair, string email, string passphrase) =>
        throw new NotSupportedException();
    public bool TryLoadKeyPair(ReadOnlyMemory<byte> encryptedKeyPair, IReadOnlyList<string> passphrases,
        [NotNullWhen(true)] out LoadedKeyPair? loadedKeyPair) => throw new NotSupportedException();
    public string ExportPublicKey(PublicKey publicKey) => throw new NotSupportedException();
    public PublicKey? ImportPublicKey(string serializedPublicKey) => throw new NotSupportedException();
    public PrivateKeyImportResult ImportPrivateKeys(string serializedAccounts, PrivateKeyImportRequest request) =>
        throw new NotSupportedException();
    public bool TryParseEmail(string email, [NotNullWhen(true)] out string? address) =>
        throw new NotSupportedException();

    private sealed class FakeDecryptionSession(
        FakeCoreServices core,
        DecryptRequest request,
        bool isDecryptable,
        string originalFileName,
        string contents,
        EncryptedWithParameters encryptedWith)
        : IDecryptionSession
    {
        public bool IsDecryptable => isDecryptable;
        public string OriginalFileName => originalFileName;
        public DateTime CreationTimeUtc => DateTime.UnixEpoch;
        public DateTime LastAccessTimeUtc => DateTime.UnixEpoch;
        public DateTime LastWriteTimeUtc => DateTime.UnixEpoch;
        public EncryptedWithParameters EncryptedWithParameters => encryptedWith;
        public async Task DecryptAsync(Stream cleartext)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            await cleartext.WriteAsync(bytes);
            core.ReportWritten(request.Progress, bytes.Length);
        }
        public void Dispose() { }
    }
}
