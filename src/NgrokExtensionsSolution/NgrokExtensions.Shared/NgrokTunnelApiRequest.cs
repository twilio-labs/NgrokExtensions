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