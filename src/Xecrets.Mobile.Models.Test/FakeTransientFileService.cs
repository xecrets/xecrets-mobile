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

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Models.Test;

/// <summary>
/// Temporary files in a folder of their own, made once the first one is asked for, and removed with
/// <see cref="Dispose"/>.
/// </summary>
internal sealed class FakeTransientFileService : ITransientFileService, IDisposable
{
    private string RootDirectory { get; } = Path.Combine(Path.GetTempPath(), $"xecrets-mobile-test-{Guid.NewGuid():N}");

    public ITransientFile CreateHandoffFile(string originalFileName) => Create("handoff", originalFileName);

    public ITransientFile CreateIncomingFile(string originalFileName) => Create("incoming", originalFileName);

    public ITransientFile CreateEncryptedInputFile(string originalFileName) => Create("decrypt", originalFileName);

    public ITransientFile CreateEncryptedOutputFile(string originalFileName) => Create("encrypt", originalFileName);

    public bool IsWiped { get; private set; }

    public Task SweepAllAsync()
    {
        IsWiped = true;
        return Task.CompletedTask;
    }

    // The max ages of the sweeps asked for.
    public List<TimeSpan> Sweeps { get; } = [];

    public Task SweepAsync(TimeSpan maxAge)
    {
        Sweeps.Add(maxAge);
        return Task.CompletedTask;
    }

    // The files left in the folder.
    public IReadOnlyList<string> RemainingFiles() =>
        Directory.Exists(RootDirectory)
            ? [.. Directory.EnumerateFiles(RootDirectory, "*", SearchOption.AllDirectories)]
            : [];

    public void Dispose()
    {
        // A decrypted file is made read-only, and Directory.Delete can't remove a read-only file on Windows.
        foreach (string file in RemainingFiles())
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        if (Directory.Exists(RootDirectory))
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }

    private ITransientFile Create(string scope, string originalFileName)
    {
        string directory = Path.Combine(RootDirectory, scope, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new FakeTransientFile(this, Path.Combine(directory, originalFileName));
    }

    // The folders left empty are removed as well, up to the root, so that a test leaves nothing behind.
    private void Delete(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath)!;
        if (Directory.Exists(directory))
        {
            // A decrypted file is made read-only, and Directory.Delete can't remove a read-only file on Windows.
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(directory, recursive: true);
        }

        for (string? parent = Path.GetDirectoryName(directory);
            parent is not null && parent.StartsWith(RootDirectory, StringComparison.Ordinal) &&
            Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any();
            parent = Path.GetDirectoryName(parent))
        {
            Directory.Delete(parent);
        }
    }

    // Deleted at once when released, unless handed off, which keeps it until the test is disposed.
    private sealed class FakeTransientFile(FakeTransientFileService owner, string filePath) : ITransientFile
    {
        private bool _isHandedOff;

        public string FilePath => filePath;

        public void MarkHandedOff() => _isHandedOff = true;

        public void Dispose()
        {
            if (!_isHandedOff)
            {
                owner.Delete(filePath);
            }
        }
    }
}
