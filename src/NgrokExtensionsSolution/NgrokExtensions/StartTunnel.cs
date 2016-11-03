// This file is licensed to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Copyright (c) 2016 David Prothero

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

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
            "WebApplication.BrowseURL"
        };

        private static readonly Regex NumberPattern = new Regex(@"\d+");

        public const int CommandId = 0x0100;
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

            var ngrok = new NgrokUtils(webApps, async delegate (string message)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ShowErrorMessage(message);
            });

            OptionsPageGrid page = (OptionsPageGrid)_package.GetDialogPage(typeof(OptionsPageGrid));

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await TaskScheduler.Default;

                await ngrok.StartTunnelsAsync(page.ExecutablePath);
            });
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
                    if (!PortPropertyNames.Contains(prop.Name)) continue;

                    var match = NumberPattern.Match(prop.Value.ToString());
                    if (!match.Success) continue;

                    var webApp = new WebAppConfig
                    {
                        PortNumber = int.Parse(match.Value),
                        SubDomain = ""
                    };

                    foreach (ProjectItem item in project.ProjectItems)
                    {
                        if (item.Name.ToLower() != "web.config") continue;

                        var path = item.FileNames[0];
                        var webConfig = XDocument.Load(path);
                        var appSettings = webConfig.Descendants("appSettings").FirstOrDefault();
                        webApp.SubDomain = appSettings?.Descendants("add")
                            .FirstOrDefault(x => x.Attribute("key")?.Value == "ngrok.subdomain")
                            ?.Attribute("value")?.Value;
                        break;
                    }

                    webApps.Add(project.Name, webApp);
                    break;
                }
            }
            return webApps;
        }

        private Projects GetSolutionProjects()
        {
            return (ServiceProvider.GetService(typeof(SDTE)) as DTE)?.Solution?.Projects;
        }
    }
}