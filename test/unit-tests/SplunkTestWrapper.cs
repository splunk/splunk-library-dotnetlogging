using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Web;
using System.Xml;

namespace Splunk.Logging
{
    class SplunkTestWrapper
    {
        private static string UserName = "admin", Password = "changed!", SessionKey = null;

        public SplunkTestWrapper()
        {
            EnableSelfSignedCertificates();
            GetAuthenticationToken();
        }

        public static HttpResponseMessage Send(string endpoint, HttpMethod method,
            Dictionary<string, string> content = null)
        {
            HttpResponseMessage response = null;
            using (HttpClient httpClient = new HttpClient())
            {
                string BaseAddress = "https://localhost:8089/";

                HttpRequestMessage request = new HttpRequestMessage
                {
                    Content = content != null ? new FormUrlEncodedContent(content) : null,
                    Method = method,
                    RequestUri = new Uri($"{BaseAddress}{endpoint}")
                };
                if (SessionKey != null)
                {
                    request.Headers.Add($"Authorization", $"Splunk {SessionKey}");
                }
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                response = httpClient.SendAsync(request).Result;
                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    string res = response.Content.ReadAsStringAsync().Result;
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(res);
                    var msgs = xmlDoc.GetElementsByTagName("msg")[0].InnerText;
                    var type = xmlDoc.GetElementsByTagName("msg")[0].Attributes["type"].InnerText;
                    throw new HttpException($"{statusCode} --- {type} : {msgs}");
                }
            }
            return response;
        }

        private static void EnableSelfSignedCertificates()
        {
            // Enable self signed certificates
            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
                delegate (object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                    System.Security.Cryptography.X509Certificates.X509Chain chain,
                    System.Net.Security.SslPolicyErrors sslPolicyErrors)
                {
                    return true;
                };
        }

        public static void GetAuthenticationToken()
        {
            string endpoint = "services/auth/login";

            var content = new Dictionary<string, string>();
            content.Add("username", UserName);
            content.Add("password", Password);
            HttpResponseMessage response = Send(endpoint, HttpMethod.Post, content);

            string res = response.Content.ReadAsStringAsync().Result;
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(res);
            SessionKey = xmlDoc.GetElementsByTagName("sessionKey")[0].InnerXml;
            xmlDoc.RemoveAll();
        }

        public void CreateIndex(string indexName)
        {
            string endpoint = "services/data/indexes";
            var content = new Dictionary<string, string>();
            content.Add("name", indexName);
            HttpResponseMessage response = Send(endpoint, HttpMethod.Post, content);
        }

        public bool IsIndexExist(string indexName)
        {
            string endpoint = "services/data/indexes";
            var response = Send(endpoint, HttpMethod.Get, null);
            XmlDocument xmlDoc = new XmlDocument();
            string res = response.Content.ReadAsStringAsync().Result;
            xmlDoc.LoadXml(res);
            var nodeList = xmlDoc.GetElementsByTagName("entry");
            foreach (XmlNode node in nodeList)
            {
                var name = node.ChildNodes[0].InnerText;
                if (name == indexName)
                {
                    return true;
                }
            }
            xmlDoc.RemoveAll();
            return false;
        }

        public void DeleteIndex(string indexName)
        {
            if (!IsIndexExist(indexName))
            {
                return;
            }
            string endpoint = $"services/data/indexes/{indexName}";

            HttpResponseMessage response = Send(endpoint, HttpMethod.Delete);
        }

        public void EnableHttp()
        {
            string endpoint = "servicesNS/nobody/search/data/inputs/http/http/enable";

            HttpResponseMessage response = Send(endpoint, HttpMethod.Post, null);
        }

        public bool IsTokenExist(string tokenName)
        {
            string endpoint = "services/data/inputs/http";
            var response = Send(endpoint, HttpMethod.Get, null);
            XmlDocument xmlDoc = new XmlDocument();
            string res = response.Content.ReadAsStringAsync().Result;
            xmlDoc.LoadXml(res);
            var nodeList = xmlDoc.GetElementsByTagName("entry");
            foreach (XmlNode node in nodeList)
            {
                var name = node.ChildNodes[0].InnerText;
                name = name.Substring(7, name.Length - 7);
                if (name == tokenName)
                {
                    return true;
                }
            }
            xmlDoc.RemoveAll();
            return false;
        }

        public string CreateToken(string tokenName, string indexes = null, string index = null)
        {
            string endpoint = "servicesNS/admin/splunk_httpinput/data/inputs/http";
            string token = null;
            var content = new Dictionary<string, string>();
            content.Add("name", tokenName);
            if (!string.IsNullOrEmpty(indexes))
                content.Add("indexes", indexes);
            if (!string.IsNullOrEmpty(index))
                content.Add("index", index);

            HttpResponseMessage response = Send(endpoint, HttpMethod.Post, content);

            Console.WriteLine("Successful.");
            string res = response.Content.ReadAsStringAsync().Result;

            int idx1 = res.IndexOf("name=\"token\">", StringComparison.Ordinal) + 13, idx2 = idx1 + 36;
            token = res.Substring(idx1, idx2 - idx1);
            content.Clear();

            return token;
        }

        public void DeleteToken(string tokenName)
        {
            if (!IsTokenExist(tokenName))
            {
                return;
            }
            string endpoint = $"servicesNS/admin/splunk_httpinput/data/inputs/http/{tokenName}";

            HttpResponseMessage response = Send(endpoint, HttpMethod.Delete);
        }

        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        public static double GetEpochTime()
        {
            return (DateTime.UtcNow - UnixEpoch).TotalSeconds;
        }

        public int GetSearchCount(string searchQuery)
        {
            int count = 0;
            string uri = "services/search/jobs/export";
            var content = new Dictionary<string, string>();
            content.Add("search", $"search {searchQuery} | stats count");
            HttpResponseMessage response = Send(uri, HttpMethod.Post, content);

            string res = response.Content.ReadAsStringAsync().Result;
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(res);
            count = Convert.ToInt32(xmlDoc.GetElementsByTagName("result")[0].InnerText);
            xmlDoc.RemoveAll();
            content.Clear();
            return count;
        }

        public List<string> GetSearchResults(string searchQuery)
        {
            List<string> searchResult = new List<string>();
            string uri = "services/search/jobs/export";
            var content = new Dictionary<string, string>();
            content.Add("search", $"search {searchQuery}");
            HttpResponseMessage responseSid = Send(uri, HttpMethod.Post, content);

            string res = responseSid.Content.ReadAsStringAsync().Result;
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(res);
            var nodeList = xmlDoc.SelectNodes("results/result/field[@k='_raw']/v");
            foreach (XmlNode node in nodeList)
                searchResult.Add(node.InnerText);
            xmlDoc.RemoveAll();
            content.Clear();
            return searchResult;
        }

        public List<string> GetMetadataResults(string searchQuery)
        {
            List<string> searchResult = new List<string>();
            string uri = "services/search/jobs/export";
            var content = new Dictionary<string, string>();
            content.Add("search", $"search {searchQuery} | stats count by host, sourcetype, source");
            HttpResponseMessage responseSid = Send(uri, HttpMethod.Post, content);

            string res = responseSid.Content.ReadAsStringAsync().Result;
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(res);
            var nodeList = xmlDoc.SelectNodes("results/result/field");
            foreach (XmlNode node in nodeList)
                searchResult.Add(node.InnerText);
            xmlDoc.RemoveAll();
            content.Clear();
            return searchResult;
        }

        public void WaitForIndexingToComplete(string indexName, double startTime = 0, int stabilityPeriod = 10)
        {
            string query = $"index={indexName} earliest={startTime:F2}";
            int eventCount = GetSearchCount(query);
            for (int i = 0; i < stabilityPeriod; i++) // Exit only if there were no indexing activities for %stabilityPeriod% straight seconds
            {
                do
                {
                    Thread.Sleep(1000);
                    int updatedEventCount = GetSearchCount(query);
                    if (updatedEventCount == eventCount)
                        break;
                    eventCount = updatedEventCount;
                    i = 0; // Indexing is still goes on, reset waiting 
                } while (true);
            }
        }
    }
}
