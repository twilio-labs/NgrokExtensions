// This file is licensed to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Copyright (c) 2016 David Prothero

namespace NgrokExtensions
{

    public class NgrokTunnelsApiResponse
    {
        public Tunnel[] tunnels { get; set; }
        public string uri { get; set; }
    }

    public class Tunnel
    {
        public string name { get; set; }
        public string uri { get; set; }
        public string public_url { get; set; }
        public string proto { get; set; }
        public Config config { get; set; }
        public Metrics metrics { get; set; }
    }

    public class Config
    {
        public string addr { get; set; }
        public bool inspect { get; set; }
    }

    public class Metrics
    {
        public Conns conns { get; set; }
        public Http http { get; set; }
    }

    public class Conns
    {
        public int count { get; set; }
        public int gauge { get; set; }
        public decimal rate1 { get; set; }
        public decimal rate5 { get; set; }
        public decimal rate15 { get; set; }
        public decimal p50 { get; set; }
        public decimal p90 { get; set; }
        public decimal p95 { get; set; }
        public decimal p99 { get; set; }
    }

    public class Http
    {
        public int count { get; set; }
        public decimal rate1 { get; set; }
        public decimal rate5 { get; set; }
        public decimal rate15 { get; set; }
        public decimal p50 { get; set; }
        public decimal p90 { get; set; }
        public decimal p95 { get; set; }
        public decimal p99 { get; set; }
    }

}