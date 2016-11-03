// This file is licensed to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Copyright (c) 2016 David Prothero

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NgrokExtensions
{
    public class NgrokUtils
    {
        private readonly Dictionary<string, WebAppConfig> _webApps;
        private readonly Func<string, Task> _showErrorFunc;
        private readonly HttpClient _ngrokApi;
        private Tunnel[] _tunnels;

        public NgrokUtils(Dictionary<string, WebAppConfig> webApps, Func<string, Task> asyncShowErrorFunc)
        {
            _webApps = webApps;
            _showErrorFunc = asyncShowErrorFunc;
            _ngrokApi = new HttpClient {BaseAddress = new Uri("http://localhost:4040")};
            _ngrokApi.DefaultRequestHeaders.Accept.Clear();
            _ngrokApi.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task StartTunnelsAsync(string exepath)
        {
            try
            {
                await DoStartTunnelsAsync(exepath);
            }

            // does not appear to be the right exception in situations when no
            // path is specified in options and exeutable is also not found in PATH
            catch (FileNotFoundException exc) 
            {
                await _showErrorFunc($"ngrok executable not found.  Configure the path in the via the add-in options or add the location to your PATH.");
            }
            catch (Exception ex)
            {
                await _showErrorFunc($"Ran into a problem trying to start the ngrok tunnel(s): {ex}");
            }
        }

        private async Task DoStartTunnelsAsync(string exepath)
        {
            await StartNgrokAsync(exepath);
            foreach (var projectName in _webApps.Keys)
            {
                await StartNgrokTunnelAsync(projectName, _webApps[projectName]);
            }
        }

        private async Task StartNgrokAsync(string exepath, bool retry = false)
        {
            if (await CanGetTunnelList()) return;

            StartNgrokProcess(exepath);
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
                var apiResponse = await response.Content.ReadAsAsync<NgrokTunnelsApiResponse>();
                _tunnels = apiResponse.tunnels;
            }
        }

        private static void StartNgrokProcess(string exepath)
        {
            var path = "ngrok.exe";

            if (!string.IsNullOrWhiteSpace(exepath) && File.Exists(exepath)) {
                path = exepath;
            }

            var pi = new ProcessStartInfo(path, "start --none")
            {
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal,
                
            };

            Process.Start(pi);
        }

        private async Task StartNgrokTunnelAsync(string projectName, WebAppConfig config)
        {
            var addr = $"localhost:{config.PortNumber}";
            if (!TunnelAlreadyExists(addr))
            {
                await CreateTunnelAsync(projectName, config, addr);
            }
        }

        private bool TunnelAlreadyExists(string addr)
        {
            return _tunnels.Any(t => t.config.addr == addr);
        }

        private async Task CreateTunnelAsync(string projectName, WebAppConfig config, string addr)
        {
            var request = new NgrokTunnelApiRequest
            {
                name = projectName,
                addr = addr,
                proto = "http",
                host_header = addr
            };
            if (!string.IsNullOrEmpty(config.SubDomain))
            {
                request.subdomain = config.SubDomain;
            }

            var response = await _ngrokApi.PostAsJsonAsync("/api/tunnels", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsAsync<NgrokErrorApiResult>();
                await _showErrorFunc($"Could not create tunnel for {projectName} ({addr}): " + 
                                     $"\n[{error.error_code}] {error.msg}" + 
                                     $"\nDetails: {error.details.err.Replace("\\n", "\n")}");
                return;
            }

            var tunnel = await response.Content.ReadAsAsync<Tunnel>();
            config.PublicUrl = tunnel.public_url;
            Debug.WriteLine(config.PublicUrl);
        }
    }
}