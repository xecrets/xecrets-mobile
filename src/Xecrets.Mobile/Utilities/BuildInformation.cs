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

using System.Linq;
using System.Reflection;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Utilities;

public sealed class BuildInformation : IBuildInformation
{
    private static Assembly AppAssembly => typeof(BuildInformation).Assembly;

    public bool IsDebug =>
        AppAssembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration == "Debug";

    public bool IsBeta => bool.Parse(GetMetadata("IsBeta"));

    public string ProductName => IsBeta ? $"{AppInfo.Current.Name} Beta" : AppInfo.Current.Name;

    public string Version => AppInfo.Current.VersionString;

    public string Platform => $"{DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString} ({DeviceInfo.Current.Idiom})";

    public string Publisher =>
        AppAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;

    public string Copyright =>
        AppAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()!.Copyright;

    public string License => GetMetadata("License");

    public string SourceCodeUrl => GetMetadata("RepositoryUrl");

    private static string GetMetadata(string key) => AppAssembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == key)
        .Value!;
}
