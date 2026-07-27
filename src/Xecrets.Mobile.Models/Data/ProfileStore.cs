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

using System.Text.Json;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Data;

public sealed class ProfileStore(IFileService fileService) : IProfileStore
{
    private const string _profileFileName = "profile.json";

    private string ProfilePath =>
        Path.Combine(fileService.AppDataDirectory, _profileFileName);

    public Task<bool> HasProfileAsync()
        => Task.FromResult(File.Exists(ProfilePath));

    public async Task<StoredProfile?> LoadAsync()
    {
        if (!File.Exists(ProfilePath))
        {
            return null;
        }

        try
        {
            await using FileStream stream = File.Open(ProfilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync(stream, JsonContext.Default.StoredProfile);
        }
        catch (JsonException)
        {
            // An unreadable profile.json (an old schema, or a write that was interrupted mid-way)
            // must not wedge the app in a crash loop: HasProfileAsync() would keep reporting a
            // profile exists, routing back to Login, which would keep failing to load it. Treat
            // it the same as no profile at all so the app falls through to profile creation.
            File.Delete(ProfilePath);
            return null;
        }
    }

    public async Task SaveAsync(StoredProfile profile)
    {
        Directory.CreateDirectory(fileService.AppDataDirectory);
        await using FileStream stream = File.Open(ProfilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, profile, JsonContext.Default.StoredProfile);
    }
}
