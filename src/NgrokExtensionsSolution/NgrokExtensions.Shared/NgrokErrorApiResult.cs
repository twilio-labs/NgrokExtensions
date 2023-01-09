namespace NgrokExtensions
{
    public class NgrokErrorApiResult
    {
        public int error_code { get; set; }
        public int status_code { get; set; }
        public string msg { get; set; }
        public Details details { get; set; }
    }

    public class Details
    {
        public string err { get; set; }
    }
}