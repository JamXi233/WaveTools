// Copyright (c) 2021-2024, JamXi JSG-LLC.
// All rights reserved.

// This file is part of WaveTools.

// WaveTools is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// WaveTools is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with WaveTools.  If not, see <http://www.gnu.org/licenses/>.

// For more information, please refer to <https://www.gnu.org/licenses/gpl-3.0.html>

// GetJSGLatest.cs

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class GetJSGLatest
{
    private static readonly HttpClient httpClient = new HttpClient();

    public async Task<(string Name, string Version, string DownloadUrl, string Changelog)> GetLatestReleaseInfoAsync(string package)
    {
        string apiUrl = $"https://api.jamsg.cn/release/getversion.php?package={package}";
        httpClient.DefaultRequestHeaders.Add("User-Agent", "JSG-Official-Update-Client");

        var response = await httpClient.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        JObject jsonObj = JObject.Parse(content);

        var name = jsonObj["name"].ToString();
        var Changelog = jsonObj["changelog"].ToString();
        var version = jsonObj["version"].ToString();
        var downloadUrl = jsonObj["link"].ToString();

        return (name, version, downloadUrl, Changelog);
    }
}