﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http.Json;

namespace NgrokExtensions
{
    public class NgrokUtils
    {
        public const string NgrokNotFoundMessage = "ngrok executable not found. Configure the path in the via the add-in options or add the location to your PATH.";
        private static readonly Version MinimumVersion = new Version("2.3.34");
        private readonly Dictionary<string, WebAppConfig> _webApps;
        private readonly Func<string, Task> _showErrorFunc;
        private readonly HttpClient _ngrokApi;
        private Tunnel[] _tunnels;
        private readonly NgrokProcess _ngrokProcess;

        public NgrokUtils(Dictionary<string, WebAppConfig> webApps, string exePath,
            Func<string, Task> asyncShowErrorFunc, 
            HttpClient client = null, NgrokProcess ngrokProcess = null)
        {
            _webApps = webApps;
            _ngrokProcess = ngrokProcess ?? new NgrokProcess(exePath);
            _showErrorFunc = asyncShowErrorFunc;
            _ngrokApi = client ?? new HttpClient();
            _ngrokApi.BaseAddress = new Uri("http://localhost:4040");
            _ngrokApi.DefaultRequestHeaders.Accept.Clear();
            _ngrokApi.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public bool NgrokIsInstalled()
        {
            if (!_ngrokProcess.IsInstalled()) return false;

            var versionString = _ngrokProcess.GetNgrokVersion();
            if (versionString == null) return false;

            var version = new Version(versionString);
            return version.CompareTo(MinimumVersion) >= 0;
        }

        public async Task StartTunnelsAsync()
        {
            Exception uncaughtException = null;

            try
            {
                await DoStartTunnelsAsync();
            }
            catch (FileNotFoundException)
            {
                await _showErrorFunc(NgrokNotFoundMessage);
            }
            catch (Win32Exception ex)
            {
                if (ex.ErrorCode.ToString("X") == "80004005")
                {
                    await _showErrorFunc(NgrokNotFoundMessage);
                }
                else
                {
                    uncaughtException = ex;
                }
            }
            catch (Exception ex)
            {
                uncaughtException = ex;
            }

            if (uncaughtException != null)
            {
                await _showErrorFunc($"Ran into a problem trying to start the ngrok tunnel(s): {uncaughtException}");
            }
        }

        private async Task DoStartTunnelsAsync()
        {
            //get first region
            string region = _webApps.Values.FirstOrDefault(c => !string.IsNullOrEmpty(c.Region))?.Region;

            await StartNgrokAsync(region: region);
            foreach (var projectName in _webApps.Keys)
            {
                await StartNgrokTunnelAsync(projectName, _webApps[projectName]);
            }
        }

        private async Task StartNgrokAsync(bool retry = false, string region = "")
        {
            if (await CanGetTunnelList()) return;

            _ngrokProcess.StartNgrokProcess(region: region);
            await Task.Delay(250);

            if (await CanGetTunnelList(retry:true)) return;
            await _showErrorFunc("Cannot start ngrok. Is it installed and in your PATH?");
        }

        private async Task<bool> CanGetTunnelList(bool retry = false)
        {
            try
            {
                await GetTunnelList();
            }
            catch
            {
                if (retry) throw;
            }
            return (_tunnels != null);
        }

        private async Task GetTunnelList()
        {
            var response = await _ngrokApi.GetAsync("/api/tunnels");
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"responseText: '{responseText}'");
                var apiResponse = JsonConvert.DeserializeObject<NgrokTunnelsApiResponse>(responseText);
                _tunnels = apiResponse.tunnels;
            }
        }

        private async Task StartNgrokTunnelAsync(string projectName, WebAppConfig config)
        {
            var addr = config.NgrokAddress;
            if (!TunnelAlreadyExists(addr))
            {
                await CreateTunnelAsync(projectName, config, addr);
            }
        }

        private bool TunnelAlreadyExists(string addr)
        {
            return _tunnels.Any(t => t.config.addr == addr);
        }

        private string StripProtocol(string addr)
        {
            return addr.Replace("https://", "");
        }

        private async Task CreateTunnelAsync(string projectName, WebAppConfig config, string addr, bool retry = false)
        {
            var request = new NgrokTunnelApiRequest
            {
                name = projectName,
                addr = addr,
                proto = "http",
                host_header = StripProtocol(addr)
            };
            if (!string.IsNullOrEmpty(config.HostName))
            {
                request.hostname = config.HostName;
            }
            else if (!string.IsNullOrEmpty(config.SubDomain))
            {
                request.subdomain = config.SubDomain;
            }

            Debug.WriteLine($"request: '{JsonConvert.SerializeObject(request)}'");
            var response = await _ngrokApi.PostAsJsonAsync("/api/tunnels", request);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"{response.StatusCode} errorText: '{errorText}'");
                NgrokErrorApiResult error;

                try
                {
                    error = JsonConvert.DeserializeObject<NgrokErrorApiResult>(errorText);
                }
                catch(JsonReaderException)
                {
                    error = null;
                }

                if (error != null)
                {
                    await _showErrorFunc($"Could not create tunnel for {projectName} ({addr}): " +
                                         $"\n[{error.error_code}] {error.msg}" +
                                         $"\nDetails: {error.details.err.Replace("\\n", "\n")}");
                }
                else
                {
                    if (retry)
                    {
                        await _showErrorFunc($"Could not create tunnel for {projectName} ({addr}): " +
                                             $"\n{errorText}");
                    }
                    else
                    {
                        await Task.Delay(1000);  // wait for ngrok to spin up completely?
                        await CreateTunnelAsync(projectName, config, addr, true);
                    }
                }
                return;
            }

            var responseText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"responseText: '{responseText}'");
            var tunnel = JsonConvert.DeserializeObject<Tunnel>(responseText);
            config.PublicUrl = tunnel.public_url;
            Debug.WriteLine(config.PublicUrl);
        }
    }
}