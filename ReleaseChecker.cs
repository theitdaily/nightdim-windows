using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace NightDim
{
    // Read-only check against the public GitHub Releases API. NightDim never
    // downloads or executes release assets automatically.
    internal static class ReleaseChecker
    {
        internal const string ReleasesPage = "https://github.com/theitdaily/nightdim-windows/releases/latest";
        private const string ApiUrl = "https://api.github.com/repos/theitdaily/nightdim-windows/releases/latest";

        internal static Version CurrentVersion { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

        internal static Version LatestVersion()
        {
            // The official GitHub HTTPS API requires modern TLS. .NET Framework
            // versions prior to 4.7 may otherwise try older protocols.
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ApiUrl);
            request.Method = "GET";
            request.Accept = "application/vnd.github+json";
            request.UserAgent = "NightDim/" + CurrentVersion.ToString(3) + " (+https://github.com/theitdaily/nightdim-windows)";
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            using (WebResponse response = request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                object tag;
                if (data == null || !data.TryGetValue("tag_name", out tag) || !(tag is string))
                    throw new InvalidDataException("GitHub did not return a release version.");
                string name = ((string)tag).Trim().TrimStart('v', 'V');
                Version latest;
                if (!Version.TryParse(name, out latest))
                    throw new InvalidDataException("Unknown GitHub release version: " + tag);
                return latest;
            }
        }
    }
}
