using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NgrokExtensions
{
    public class OptionsPageGrid : Microsoft.VisualStudio.Shell.DialogPage
    {
        private string executablePath = "";

        [Category("ngrok")]
        [DisplayName("Executable Path")]
        [Description("Full path to the ngrok executable")]
        public string ExecutablePath
        {
            get { return executablePath; }
            set { executablePath = value; }
        }
    }
}
