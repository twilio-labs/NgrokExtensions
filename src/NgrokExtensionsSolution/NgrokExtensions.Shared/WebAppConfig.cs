// This file is licensed to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Copyright (c) 2019 David Prothero

using System.Text.RegularExpressions;

namespace NgrokExtensions
{
    public class WebAppConfig
    {
        private static readonly Regex HttpsPattern = new Regex(@"^https://[^/]+");
        private static readonly Regex NumberPattern = new Regex(@"\d+");

        public bool IsValid
        {
            get
            {
                return NgrokAddress != null;
            }
        }

        public string NgrokAddress { get; }
        public string SubDomain { get; set; }
        public string PublicUrl { get; set; }

        public WebAppConfig(string settingValue)
        {
            NgrokAddress = ParseNgrokAddress(settingValue);
        }

        private string ParseNgrokAddress(string settingValue)
        {
            var match = HttpsPattern.Match(settingValue);
            if (match.Success) return match.Value;

            match = NumberPattern.Match(settingValue);
            if (match.Success) return $"localhost:{match.Value}";

            return null;
        }
    }
}