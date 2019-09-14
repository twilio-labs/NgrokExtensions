using System.Diagnostics;

namespace NgrokExtensions.Test
{
    public class FakeNgrokProcess : NgrokProcess
    {
        private readonly string _stdout;

        public FakeNgrokProcess(string exePath, string stdout) : base(exePath)
        {
            _stdout = stdout;
        }

        public int StartCount { get; set; } = 0;
        public ProcessStartInfo LastProcessStartInfo { get; set; }

        protected override void Start(ProcessStartInfo pi)
        {
            StartCount++;
            LastProcessStartInfo = pi;
        }

        protected override string GetStandardOutput()
        {
            return _stdout;
        }

        protected override void WaitForExit()
        {
            // nothing to wait for in testing
        }
    }
}