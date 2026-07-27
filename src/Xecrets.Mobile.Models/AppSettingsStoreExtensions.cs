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
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models;

public static class AppSettingsStoreExtensions
{
    private const int _maximumSavedPasswordCount = 25;

    extension(IAppSettingsStore appSettingsStore)
    {
        public async Task RecordSuccessfulPasswordUseAsync(AppSettings settings, string password)
        {
            ExtraPasswordSetting? entry = settings.ExtraPasswords
                .FirstOrDefault(extraPassword => string.Equals(extraPassword.Password, password, StringComparison.Ordinal));

            if (entry is null)
            {
                if (settings.ExtraPasswords.Count >= _maximumSavedPasswordCount)
                {
                    settings.ExtraPasswords.RemoveRange(
                        _maximumSavedPasswordCount - 1,
                        settings.ExtraPasswords.Count - (_maximumSavedPasswordCount - 1));
                }

                settings.ExtraPasswords.Add(new ExtraPasswordSetting { Password = password, UsageCount = 1, });
            }
            else
            {
                entry.UsageCount++;
                Sort(settings);
            }

            await appSettingsStore.SaveAsync(settings);
        }

        public async Task<ThemePreference> GetThemeAsync()
        {
            AppSettings settings = await appSettingsStore.LoadAsync();
            return settings.Theme;
        }

        public async Task SetThemeAsync(ThemePreference theme)
        {
            AppSettings settings = await appSettingsStore.LoadAsync();
            settings.Theme = theme;
            await appSettingsStore.SaveAsync(settings);
        }
    }

    private static void Sort(AppSettings settings) =>
        settings.ExtraPasswords =
        [
            .. settings.ExtraPasswords
                .OrderByDescending(extraPassword => extraPassword.UsageCount)
        ];
}
