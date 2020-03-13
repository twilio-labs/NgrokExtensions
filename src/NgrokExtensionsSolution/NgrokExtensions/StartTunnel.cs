// This file is licensed to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Copyright (c) 2019 David Prothero

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;

namespace NgrokExtensions
{
    internal sealed class StartTunnel
    {
        private static readonly HashSet<string> PortPropertyNames = new HashSet<string>
        {
            "WebApplication.DevelopmentServerPort",
            "WebApplication.IISUrl",
            "WebApplication.CurrentDebugUrl",
            "WebApplication.NonSecureUrl",
            "WebApplication.BrowseURL",
            "NodejsPort", // Node.js project
            "FileName",    // Azure functions if ends with '.funproj'
            "ProjectUrl"
        };

        public const int CommandId = 0x0100;
        private const string NgrokSubdomainSettingName = "ngrok.subdomain";
        private const string NgrokHostnameSettingName = "ngrok.hostname";
        private const string NgrokRegionSettingName = "ngrok.region";
        public static readonly Guid CommandSet = new Guid("30d1a36d-a03a-456d-b639-f28b9b23e161");
        private readonly Package _package;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartTunnel"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private StartTunnel(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            _package = package;

            var commandService =
                ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null) return;

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static StartTunnel Instance { get; private set; }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider => _package;

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new StartTunnel(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            var webApps = GetWebApps();

            if (webApps.Count == 0)
            {
                ShowErrorMessage("Did not find any Web projects.");
                return;
            }

            var page = (OptionsPageGrid)_package.GetDialogPage(typeof(OptionsPageGrid));
            var ngrok = new NgrokUtils(webApps, page.ExecutablePath, ShowErrorMessageAsync);

            var installPlease = false;
            if (!ngrok.NgrokIsInstalled())
            {
                if (AskUserYesNoQuestion(
                    "Ngrok 2.3.34 or above is not installed. Would you like me to download it from ngrok.com and install it for you?"))
                {
                    installPlease = true;
                }
                else
                {
                    return;
                }
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await TaskScheduler.Default;
                if (installPlease)
                {
                    try
                    {
                        var installer = new NgrokInstaller();
                        page.ExecutablePath = await installer.InstallNgrok();
                        ngrok = new NgrokUtils(webApps, page.ExecutablePath, ShowErrorMessageAsync);
                    }
                    catch (NgrokDownloadException ngrokDownloadException)
                    {
                        await ShowErrorMessageAsync(ngrokDownloadException.Message);
                        return;
                    }
                }
                await ngrok.StartTunnelsAsync();
            });
        }

        private async System.Threading.Tasks.Task ShowErrorMessageAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ShowErrorMessage(message);
        }

        private void ShowErrorMessage(string message)
        {
            VsShellUtilities.ShowMessageBox(
                this.ServiceProvider,
                message,
                "ngrok",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private bool AskUserYesNoQuestion(string message)
        {
            var result = VsShellUtilities.ShowMessageBox(
                this.ServiceProvider,
                message,
                "ngrok",
                OLEMSGICON.OLEMSGICON_QUERY,
                OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            return result == 6;  // Yes
        }

        private Dictionary<string, WebAppConfig> GetWebApps()
        {
            var webApps = new Dictionary<string, WebAppConfig>();
            var projects = GetSolutionProjects();
            if (projects == null) return webApps;

            foreach (Project project in projects)
            {
                if (project.Properties == null) continue; // Project not loaded yet

                foreach (Property prop in project.Properties)
                {
                    DebugWriteProp(prop);
                    if (!PortPropertyNames.Contains(prop.Name)) continue;

                    WebAppConfig webApp;

                    if (prop.Name == "FileName")
                    {
                        if (prop.Value.ToString().EndsWith(".funproj"))
                        {
                            // Azure Functions app - use port 7071
                            webApp = new WebAppConfig("7071");
                            LoadOptionsFromAppSettingsJson(project, webApp);
                        }
                        else
                        {
                            continue;  // FileName property not relevant otherwise
                        }
                    }
                    else
                    {
                        webApp = new WebAppConfig(prop.Value.ToString());
                        if (!webApp.IsValid) continue;
                        if (IsAspNetCoreProject(prop.Name))
                        {
                            LoadOptionsFromAppSettingsJson(project, webApp);
                        }
                        else
                        {
                            LoadOptionsFromWebConfig(project, webApp);
                        }
                    }

                    webApps.Add(project.Name, webApp);
                    break;
                }
            }
            return webApps;
        }

        private bool IsAspNetCoreProject(string propName)
        {
            return propName == "ProjectUrl";
        }

        private static void LoadOptionsFromWebConfig(Project project, WebAppConfig webApp)
        {
            foreach (ProjectItem item in project.ProjectItems)
            {
                if (item.Name.ToLower() != "web.config") continue;

                var path = item.FileNames[0];
                var webConfig = XDocument.Load(path);
                var appSettings = webConfig.Descendants("appSettings").FirstOrDefault();
                webApp.SubDomain = appSettings?.Descendants("add")
                    .FirstOrDefault(x => x.Attribute("key")?.Value == NgrokSubdomainSettingName)
                    ?.Attribute("value")?.Value;
                webApp.HostName = appSettings?.Descendants("add")
                    .FirstOrDefault(x => x.Attribute("key")?.Value == NgrokHostnameSettingName)
                    ?.Attribute("value")?.Value;
                webApp.Region = appSettings?.Descendants("add")
                    .FirstOrDefault(x => x.Attribute("key")?.Value == NgrokRegionSettingName)
                    ?.Attribute("value")?.Value;
                break;
            }
        }

        private static void LoadOptionsFromAppSettingsJson(Project project, WebAppConfig webApp)
        {
            // Read the settings from the project's appsettings.json first
            foreach (ProjectItem item in project.ProjectItems)
            {
                if (item.Name.ToLower() != "appsettings.json") continue;

                ReadOptionsFromJsonFile(item.FileNames[0], webApp);
            }

            // Override any additional settings from the secrets.json file if it exists
            var userSecretsId = project.Properties.OfType<Property>()
                .FirstOrDefault(x => x.Name.Equals("UserSecretsId", StringComparison.OrdinalIgnoreCase))?.Value as String;

            if (string.IsNullOrEmpty(userSecretsId)) return;

            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var secretsFile = Path.Combine(appdata, "Microsoft", "UserSecrets", userSecretsId, "secrets.json");

            ReadOptionsFromJsonFile(secretsFile, webApp);
        }

        private static void ReadOptionsFromJsonFile(string path, WebAppConfig webApp)
        {
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var appSettings = JsonConvert.DeserializeAnonymousType(json,
                new { IsEncrypted = false, Values = new Dictionary<string, string>() });
            
            if (appSettings.Values != null && appSettings.Values.TryGetValue(NgrokSubdomainSettingName, out var subdomain))
            {
                webApp.SubDomain = subdomain;
            }
            if (appSettings.Values != null && appSettings.Values.TryGetValue(NgrokHostnameSettingName, out var hostname))
            {
                webApp.HostName = hostname;
            }
            if (appSettings.Values != null && appSettings.Values.TryGetValue(NgrokRegionSettingName, out var region))
            {
                webApp.Region = region;
            }
        }

        private static void DebugWriteProp(Property prop)
        {
            try
            {
                Debug.WriteLine($"{prop.Name} = {prop.Value}");
            }
            catch
            {
                // ignored
            }
        }

        private IEnumerable<Project> GetSolutionProjects()
        {
            var solution = (ServiceProvider.GetService(typeof(SDTE)) as DTE)?.Solution;
            return solution == null ? null : ProcessProjects(solution.Projects.Cast<Project>());
        }

        private static IEnumerable<Project> ProcessProjects(IEnumerable<Project> projects)
        {
            var newProjectsList = new List<Project>();
            foreach (var p in projects)
            {

                if (p.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    newProjectsList.AddRange(ProcessProjects(GetSolutionFolderProjects(p)));
                }
                else
                {
                    newProjectsList.Add(p);
                }
            }

            return newProjectsList;
        }

        private static IEnumerable<Project> GetSolutionFolderProjects(Project project)
        {
            return project.ProjectItems.Cast<ProjectItem>()
                .Select(item => item.SubProject)
                .Where(subProject => subProject != null)
                .ToList();
        }
    }
}