// This file is licensed to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Copyright (c) 2016 David Prothero

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