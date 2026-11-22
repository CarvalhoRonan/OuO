using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;

static class CloudAccountProbe {
    static object Value(object instance, string name) {
        var type = instance.GetType();
        var field = type.GetField(name);
        return field != null ? field.GetValue(instance) : type.GetProperty(name).GetValue(instance, null);
    }

    static int Main(string[] args) {
        string output = args[1];
        try {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var connection = assembly.GetType("Aurora.CloudConnection", true);
            var provider = Enum.Parse(assembly.GetType("Aurora.CloudProvider", true), "OneDrive");
            int lastBucket = 0;
            Action<string> progress = text => {
                string digits = System.Text.RegularExpressions.Regex.Match(text, @"^[\d., ]+").Value;
                int count;
                if (Int32.TryParse(System.Text.RegularExpressions.Regex.Replace(digits, @"\D", ""), out count)) {
                    int bucket = count / 10000;
                    if (bucket > lastBucket) { lastBucket = bucket; Console.WriteLine(count + " files checked"); }
                } else Console.WriteLine(text);
            };
            var watch = Stopwatch.StartNew();
            object report = connection.GetMethod("Scan").Invoke(null, new object[] { provider, CancellationToken.None, progress });
            watch.Stop();
            bool partial = (bool)Value(report, "Partial"), failed = (bool)Value(report, "ListingFailed");
            if (partial || failed) throw new IOException("Cloud inventory was incomplete.");
            var items = (IEnumerable)report.GetType().GetMethod("LargestFiles", new[] { typeof(string), typeof(string) }).Invoke(report, new object[] { "", "" });
            int photos = 0, videos = 0, mime = 0, total = 0;
            long photoBytes = 0, videoBytes = 0, previous = Int64.MaxValue;
            foreach (object item in items) {
                long size = (long)Value(item, "Bytes");
                if (size > previous) throw new InvalidDataException("Cloud media ranking lost size order.");
                previous = size;
                total++;
                if (!String.IsNullOrEmpty((string)Value(item, "MimeType"))) mime++;
                string kind = Convert.ToString(Value(item, "Kind"));
                if (kind == "Photo") { photos++; if (size >= 0) photoBytes = checked(photoBytes + size); }
                if (kind == "Video") { videos++; if (size >= 0) videoBytes = checked(videoBytes + size); }
            }
            var quota = Value(report, "Quota");
            object comparison = assembly.GetType("Aurora.OneDriveSpaceSummary", true).GetMethod("From").Invoke(null, new[] { report });
            var result = new Dictionary<string, object> {
                { "Status", "PASS" }, { "CheckedUtc", DateTime.UtcNow.ToString("o") },
                { "Seconds", Math.Round(watch.Elapsed.TotalSeconds, 3) },
                { "FileCount", total }, { "KnownBytes", Value(report, "KnownBytes") },
                { "UnknownSizes", Value(report, "UnknownSizes") }, { "Partial", partial },
                { "Used", quota == null ? null : Value(quota, "Used") },
                { "Trashed", quota == null ? null : Value(quota, "Trashed") },
                { "Unexplained", Value(comparison, "Remaining") }, { "TotalsConflict", Value(comparison, "TotalsConflict") },
                { "PhotoFiles", photos }, { "PhotoBytes", photoBytes }, { "VideoFiles", videos }, { "VideoBytes", videoBytes },
                { "FilesWithProviderMime", mime }, { "LargestFirst", true }, { "DownloadedContent", false }
            };
            File.WriteAllText(output, new JavaScriptSerializer().Serialize(result));
            Console.WriteLine("PASS OneDrive media inventory");
            return 0;
        } catch (Exception error) {
            File.WriteAllText(output, "FAIL\n" + error);
            Console.Error.WriteLine("Cloud account validation failed; see the local report.");
            return 1;
        }
    }
}
