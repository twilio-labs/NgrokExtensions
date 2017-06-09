using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NgrokExtensions
{
    public class NgrokProcess
    {
        private readonly string _exePath;

        public NgrokProcess(string exePath)
        {
            _exePath = exePath;
        }

        public void StartNgrokProcess()
        {
            var path = GetNgrokPath();

            var pi = new ProcessStartInfo(path, "start --none")
            {
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal    
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
            Process.Start(pi);
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