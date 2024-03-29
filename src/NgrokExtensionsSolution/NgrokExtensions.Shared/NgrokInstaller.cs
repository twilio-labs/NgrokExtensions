﻿using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NgrokExtensions
{
    public class NgrokDownloadException : Exception
    {
        public NgrokDownloadException(string message) : base(message)
        {

        }
    }

    public class NgrokInstaller
    {
        private readonly HttpClient _httpClient;
        private readonly bool _is64Bit;

        public NgrokInstaller()
        {
            _httpClient = new HttpClient();
            _is64Bit = Environment.Is64BitOperatingSystem;
        }

        public NgrokInstaller(HttpClient httpClient, bool is64Bit)
        {
            _httpClient = httpClient;
            _is64Bit = is64Bit;
        }

        public async Task<string> GetNgrokDownloadUrlAsync()
        {
            var response = await _httpClient.GetAsync("https://ngrok.com/download");

            if (!response.IsSuccessStatusCode)
            {
                throw new NgrokDownloadException($"Error retrieving ngrok download page. ({response.StatusCode})");
            }

            var html = await response.Content.ReadAsStringAsync();

            var downloadLinkId = "windows-dl-link";
            var pattern = @"id=""" + downloadLinkId +
                @"""(?:.|\s)*?[^>]+?href=""(http[s]?:\/\/[^""]*?)""";

            var match = Regex.Match(html, pattern);

            if (!match.Success)
            {
                throw new NgrokDownloadException("Could not find ngrok download URL.");
            }

            var archDownloadUri = match.Groups[1].Value.Replace("&amp;", "&");

            // 07-Mar-2022: ngrok has dynamic download page now. This is a change to replace the arch version in their download URI
            if (!_is64Bit) archDownloadUri = archDownloadUri.Replace("amd64", "386");

            return archDownloadUri;
        }

        public async Task<Stream> DownloadNgrokAsync(string url = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                url = await GetNgrokDownloadUrlAsync();
            }

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new NgrokDownloadException($"Error trying to download {url}. ({response.StatusCode})");
            }

            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<string> InstallNgrokAsync()
        {
            var zip = new ZipArchive(await DownloadNgrokAsync(), ZipArchiveMode.Read);
            var exeEntry = zip.GetEntry("ngrok.exe");
            using (var exeData = exeEntry.Open())
            {
                var ngrokPath = Path.GetFullPath(".\\ngrok.exe");
                using (var exeFile = File.Create(ngrokPath))
                {
                    await exeData.CopyToAsync(exeFile);
                    return ngrokPath;
                }
            }
        }
    }
}