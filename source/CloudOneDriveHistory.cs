using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace Aurora {
    internal sealed class CloudFileVersion {
        public string Id;
        public long? Bytes;
        public DateTimeOffset? Modified;
    }

    internal sealed class OneDriveHistoryReport {
        public const int VersionLimit = 2000, PageLimit = 20;
        public readonly List<CloudFileVersion> Versions = new List<CloudFileVersion>();
        public string FilePath;
        public bool Partial, LimitReached;

        internal static string VersionPath(string rootDrive, string normalizedId) {
            string drive = rootDrive, item = normalizedId ?? "";
            string[] parts = item.Split('#');
            if (parts.Length == 2) { drive = parts[0]; item = parts[1]; }
            else if (parts.Length != 1) throw new InvalidDataException("Invalid OneDrive item identifier.");
            if (!Regex.IsMatch(drive ?? "", @"\A[A-Za-z0-9_!-]{1,256}\z") ||
                !Regex.IsMatch(item, @"\A[A-Za-z0-9_!.-]{1,512}\z") || item == "." || item == "..")
                throw new InvalidDataException("Invalid OneDrive item identifier.");
            return "/drives/" + Uri.EscapeDataString(drive) + "/items/" + Uri.EscapeDataString(item) + "/versions";
        }

        internal static string NextPage(string next, string route) {
            Uri address;
            if (next.Length > 8192 || !Uri.TryCreate(next, UriKind.Absolute, out address) ||
                address.Scheme != "https" || address.Host != "graph.microsoft.com" || address.Port != 443 ||
                address.UserInfo.Length != 0 || address.Fragment.Length != 0 || Uri.UnescapeDataString(address.AbsolutePath) != Uri.UnescapeDataString("/v1.0" + route))
                throw new InvalidDataException("Unexpected OneDrive history continuation.");
            return route + address.Query;
        }

        internal static OneDriveHistoryReport Read(CloudStorageFile file, string drive,
            Func<string, string> get, CancellationToken token, Action<int> progress) {
            var report = new OneDriveHistoryReport { FilePath = file.Path };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var continuations = new HashSet<string>(StringComparer.Ordinal);
            string route = VersionPath(drive, file.Id);
            string path = route + "?$select=id,size,lastModifiedDateTime&$top=100";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int page = 0; page < PageLimit; page++) {
                token.ThrowIfCancellationRequested();
                if (watch.Elapsed.TotalMinutes >= 2) { report.Partial = true; break; }
                progress(page + 1);
                string text = get(path);
                token.ThrowIfCancellationRequested();
                var values = new JavaScriptSerializer { MaxJsonLength = 65536 }.DeserializeObject(text) as Dictionary<string, object>;
                object raw;
                var rows = values != null && values.TryGetValue("value", out raw) ? raw as object[] : null;
                if (rows == null) throw new InvalidDataException("Invalid OneDrive version list.");
                foreach (var rawRow in rows) {
                    if (report.Versions.Count >= VersionLimit) { report.LimitReached = report.Partial = true; break; }
                    var row = rawRow as Dictionary<string, object>;
                    object value;
                    string id = row != null && row.TryGetValue("id", out value) ? value as string : null;
                    if (String.IsNullOrWhiteSpace(id) || id.Length > 128 || id.Any(Char.IsControl)) { report.Partial = true; continue; }
                    if (!seen.Add(id)) continue;
                    long size;
                    long? bytes = row.TryGetValue("size", out value) && Int64.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                        NumberStyles.None, CultureInfo.InvariantCulture, out size) ? (long?)size : null;
                    DateTimeOffset modified;
                    DateTimeOffset? date = row.TryGetValue("lastModifiedDateTime", out value) && DateTimeOffset.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out modified) ? (DateTimeOffset?)modified : null;
                    report.Versions.Add(new CloudFileVersion { Id = id, Bytes = bytes, Modified = date });
                }
                if (report.LimitReached) break;
                string next = values.TryGetValue("@odata.nextLink", out raw) ? raw as string : null;
                if (String.IsNullOrEmpty(next)) break;
                if (!continuations.Add(next) || page == PageLimit - 1) { report.LimitReached = report.Partial = true; break; }
                path = NextPage(next, route);
            }
            return report;
        }
    }

    internal static partial class CloudConnection {
        static Dictionary<string, string> OneDriveSession(string config) {
            if (!HasToken(config, CloudProvider.OneDrive))
                throw new IOException(L.T("Reconecte o OneDrive para confirmar a autorização de leitura.", "Reconnect OneDrive to confirm read access."));
            string text;
            using (var reader = new StreamReader(config, Utf8)) text = ReadQuotaText(reader);
            bool selected = false;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in text.Split('\n')) {
                string line = raw.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal)) selected = line == "[ouo-onedrive]";
                int separator = line.IndexOf('=');
                if (selected && separator > 0) values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
            return values;
        }

        public static OneDriveHistoryReport InspectOneDriveHistory(CloudStorageFile file, CancellationToken token, Action<string> progress) {
            if (file == null) throw new ArgumentNullException("file");
            return WithAccount(CloudProvider.OneDrive, token, (engine, config) => {
                AccountQuota(engine, config, CloudProvider.OneDrive, token, progress);
                var session = OneDriveSession(config);
                string access = StringValue(Json(session["token"]), "access_token");
                if (String.IsNullOrWhiteSpace(access) || access.Any(Char.IsControl)) throw new InvalidDataException("OneDrive session unavailable.");
                return OneDriveHistoryReport.Read(file, session["drive_id"], path => ReadOneDrive(path, access, token), token,
                    page => progress(L.T("Consultando o histórico do arquivo • página ", "Checking file history • page ") + page));
            });
        }
    }
}
