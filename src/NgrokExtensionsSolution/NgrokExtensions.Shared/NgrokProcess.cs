using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NgrokExtensions
{
    public class NgrokProcess
    {
        private static readonly Regex VersionPattern = new Regex(@"\d+\.\d+\.\d+");
        private readonly string _exePath;
        private Process _osProcess;

        public NgrokProcess(string exePath)
        {
            _exePath = exePath;
        }

        public string GetNgrokVersion()
        {
            StartNgrokProcess("--version", false);
            var version = GetStandardOutput();
            WaitForExit();

            var match = VersionPattern.Match(version);
            return match.Success ? match.Value : null;
        }

        public void StartNgrokProcess(string args = "start --none", bool showWindow = true)
        {
            var path = GetNgrokPath();

            var pi = new ProcessStartInfo(path, args)
            {
                CreateNoWindow = !showWindow,
                WindowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
                RedirectStandardOutput = !showWindow,
                UseShellExecute = showWindow
            };

            Start(pi);
        }

        private string GetNgrokPath()
        {
            var path = "ngrok.exe";

            if (!string.IsNullOrWhiteSpace(_exePath) && File.Exists(_exePath))
            {
                path = _exePath;
            }

            return path;
        }

        protected virtual void Start(ProcessStartInfo pi)
        {
            _osProcess = Process.Start(pi);
        }

        protected virtual string GetStandardOutput()
        {
            return _osProcess.StandardOutput.ReadToEnd();
        }

        protected virtual void WaitForExit()
        {
            _osProcess.WaitForExit();
        }

        public bool IsInstalled()
        {
            var fileName = GetNgrokPath();

            if (File.Exists(fileName))
                return true;

            var values = Environment.GetEnvironmentVariable("PATH") ?? "";
            return values.Split(Path.PathSeparator)
                .Select(path => Path.Combine(path, fileName))
                .Any(File.Exists);
        }
    }
}