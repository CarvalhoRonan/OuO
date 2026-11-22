using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace Aurora {
    internal sealed class CloudServiceUsage {
        public readonly string Id;
        public readonly long Bytes;
        public CloudServiceUsage(string id, long bytes) { Id = id; Bytes = bytes; }
    }

    internal sealed partial class CloudQuota {
        public readonly List<CloudServiceUsage> Services = new List<CloudServiceUsage>();
        public long? GoogleAggregate;
        public bool ServicesChecked, ServiceRowsInvalid, ServicesInconsistent;

        public long? GoogleAccountUsed {
            get {
                if (GoogleAggregate.HasValue) return GoogleAggregate;
                return Used.HasValue && Other.HasValue && Used.Value <= Int64.MaxValue - Other.Value ? Used + Other : null;
            }
        }

        public long? Unattributed {
            get {
                var used = GoogleAccountUsed;
                if (!used.HasValue || Services.Count == 0) return null;
                long sum = 0;
                foreach (var service in Services) {
                    if (service.Bytes > used.Value - sum) return null;
                    sum += service.Bytes;
                }
                return used.Value - sum;
            }
        }

        public static CloudQuota ParseGoogleServices(string json) {
            var values = new JavaScriptSerializer { MaxJsonLength = 65536 }.DeserializeObject(json) as Dictionary<string, object>;
            if (values == null) throw new InvalidDataException("Invalid Google service quota.");
            var quota = new CloudQuota {
                Total = Bytes(values, "quotaBytesTotal"), Used = Bytes(values, "quotaBytesUsed"),
                Trashed = Bytes(values, "quotaBytesUsedInTrash"), GoogleAggregate = Bytes(values, "quotaBytesUsedAggregate"),
                ServicesChecked = true
            };
            object raw;
            var rows = values.TryGetValue("quotaBytesByService", out raw) ? raw as object[] : null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var blocked = new HashSet<string>(StringComparer.Ordinal);
            if (rows != null) {
                foreach (var row in rows.Take(128)) {
                    var fields = row as Dictionary<string, object>;
                    object name;
                    string id = fields != null && fields.TryGetValue("serviceName", out name) ? name as string : null;
                    id = (id ?? "").Trim().ToUpperInvariant();
                    long? size = fields == null ? null : Bytes(fields, "bytesUsed");
                    if (!Regex.IsMatch(id, @"\A[A-Z][A-Z0-9_ .-]{0,63}\z") || !size.HasValue) {
                        quota.ServiceRowsInvalid = true;
                        continue;
                    }
                    // Conflicting duplicate names cannot be assigned an arbitrary value.
                    if (!ids.Add(id)) {
                        var previous = quota.Services.FirstOrDefault(x => x.Id == id);
                        if (previous != null && previous.Bytes == size.Value && !blocked.Contains(id)) continue;
                        quota.Services.RemoveAll(x => x.Id == id);
                        blocked.Add(id);
                        quota.ServiceRowsInvalid = true;
                        continue;
                    }
                    quota.Services.Add(new CloudServiceUsage(id, size.Value));
                }
                if (rows.Length > 128) quota.ServiceRowsInvalid = true;
            } else if (raw != null) quota.ServiceRowsInvalid = true;

            if (quota.GoogleAggregate.HasValue && quota.Used.HasValue && quota.GoogleAggregate.Value >= quota.Used.Value)
                quota.Other = quota.GoogleAggregate.Value - quota.Used.Value;
            if (quota.Total.HasValue && quota.GoogleAggregate.HasValue)
                quota.Free = Math.Max(0, quota.Total.Value - quota.GoogleAggregate.Value);
            quota.ServicesInconsistent = quota.GoogleAggregate.HasValue && quota.Services.Count > 0 && !quota.Unattributed.HasValue;
            // Missing per-service data is different from an explicitly reported zero.
            return quota.Total.HasValue || quota.Used.HasValue || quota.GoogleAggregate.HasValue || quota.Services.Count > 0 ? quota : null;
        }
    }

    internal static partial class CloudConnection {
        internal const string GoogleServiceQuotaUrl = "https://www.googleapis.com/drive/v2/about?fields=quotaBytesByService(serviceName,bytesUsed),quotaBytesTotal,quotaBytesUsed,quotaBytesUsedInTrash,quotaBytesUsedAggregate";

        static string GoogleAccessToken(string config) {
            if (!HasToken(config, CloudProvider.GoogleDrive)) throw new InvalidDataException("Google metadata session unavailable.");
            string text;
            using (var reader = new StreamReader(config, Utf8)) text = ReadQuotaText(reader);
            bool selected = false;
            foreach (string raw in text.Split('\n')) {
                string line = raw.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal)) selected = line == "[ouo-google]";
                int separator = line.IndexOf('=');
                if (!selected || separator < 1 || line.Substring(0, separator).Trim() != "token") continue;
                string access = StringValue(Json(line.Substring(separator + 1).Trim()), "access_token");
                if (String.IsNullOrWhiteSpace(access) || access.Any(Char.IsControl)) break;
                return access;
            }
            throw new InvalidDataException("Google metadata session unavailable.");
        }

        static CloudQuota GoogleServices(string config, CloudQuota fallback, CancellationToken token) {
            try {
                token.ThrowIfCancellationRequested();
                var request = (HttpWebRequest)WebRequest.Create(GoogleServiceQuotaUrl);
                request.Method = "GET";
                request.AllowAutoRedirect = false;
                request.Timeout = request.ReadWriteTimeout = 12000;
                request.Headers[HttpRequestHeader.Authorization] = "Bearer " + GoogleAccessToken(config);
                request.UserAgent = "OuO Cloud Analyzer/1.0";
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (token.Register(request.Abort))
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                    if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > 65536)
                        throw new InvalidDataException("Google service quota unavailable.");
                    string json = ReadQuotaText(reader);
                    token.ThrowIfCancellationRequested();
                    var quota = CloudQuota.ParseGoogleServices(json);
                    if (quota != null) return quota;
                }
            } catch (WebException error) {
                if (error.Response != null) error.Response.Dispose();
                token.ThrowIfCancellationRequested();
            } catch (IOException) {}
            catch (ArgumentException) {}
            catch (InvalidOperationException) {}
            token.ThrowIfCancellationRequested();
            var unavailable = fallback ?? new CloudQuota();
            unavailable.ServicesChecked = true;
            return unavailable;
        }
    }
}
