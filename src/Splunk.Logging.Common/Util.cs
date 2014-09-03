using System;
using System.Linq;
using System.Net;

namespace Splunk.Logging
{
    public static class Util
    {
        public static IPAddress HostnameToIPAddress(this string hostname)
        {
            IPHostEntry addresses = Dns.GetHostEntry(hostname);
            if (addresses.AddressList.Count() < 1)
                throw new Exception(string.Format("No IP address corresponding to hostname {0} found.", hostname));
            else
                return addresses.AddressList[0];
        }
    }
}
