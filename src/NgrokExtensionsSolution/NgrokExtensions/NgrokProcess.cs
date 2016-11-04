using System.Diagnostics;
using System.IO;

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
            var path = "ngrok.exe";

            if (!string.IsNullOrWhiteSpace(_exePath) && File.Exists(_exePath)) {
                path = _exePath;
            }

            var pi = new ProcessStartInfo(path, "start --none")
            {
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal    
            };

            Start(pi);
        }

        protected virtual void Start(ProcessStartInfo pi)
        {
            Process.Start(pi);
        }
    }
}