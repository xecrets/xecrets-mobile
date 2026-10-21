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

    public string CreateHandoffPath(string originalFileName) => CreatePath("handoff", originalFileName);

    public string CreateIncomingPath(string originalFileName) => CreatePath("incoming", originalFileName);

    public string CreateEncryptedInputPath(string originalFileName) => CreatePath("decrypt", originalFileName);

    public string CreateEncryptedOutputPath(string originalFileName) => CreatePath("encrypt", originalFileName);

    // The folders left empty are removed as well, up to the root, so that a test leaves nothing behind.
    public Task WipeTransientFileAsync(string filePath)
    {
        File.Delete(filePath);
        string directory = Path.GetDirectoryName(filePath)!;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        for (string? parent = Path.GetDirectoryName(directory);
            parent is not null && parent.StartsWith(RootDirectory, StringComparison.Ordinal) &&
            Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any();
            parent = Path.GetDirectoryName(parent))
        {
            Directory.Delete(parent);
        }

        return Task.CompletedTask;
    }

    public Task RunExclusiveAsync(Func<Task> operation) => operation();

    public Task MaybeWipeTrackedFilesAsync() => Task.CompletedTask;

    // The files left in the folder.
    public IReadOnlyList<string> RemainingFiles() =>
        Directory.Exists(RootDirectory)
            ? [.. Directory.EnumerateFiles(RootDirectory, "*", SearchOption.AllDirectories)]
            : [];

    public void Dispose()
    {
        if (Directory.Exists(RootDirectory))
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }

    private string CreatePath(string scope, string originalFileName)
    {
        string directory = Path.Combine(RootDirectory, scope, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, originalFileName);
    }
}
