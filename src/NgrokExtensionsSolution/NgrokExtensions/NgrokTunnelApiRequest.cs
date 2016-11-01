// This file is licensed to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Copyright (c) 2016 David Prothero

namespace NgrokExtensions
{
    public class NgrokTunnelApiRequest
    {
        public string name { get; set; }
        public string addr { get; set; }
        public string proto { get; set; }
        public string subdomain { get; set; }
        public string host_header { get; set; }
    }
}