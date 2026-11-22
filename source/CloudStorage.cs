using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.VisualBasic.FileIO;

namespace Aurora {
    internal enum CloudAvailability { Unreported, Local, Online }

    internal sealed class CloudStorageFile {
        public string Path, Id, MimeType;
        public long Bytes;
        public CloudAvailability Availability;
        public CloudFileKind Kind { get { return CloudMedia.Kind(Path, MimeType); } }
    }

    internal sealed class CloudStorageFolder {
        public string Path;
        public long KnownBytes;
        public int Files, UnknownSizes;
        public readonly CloudMediaTotal Photos = new CloudMediaTotal(), Videos = new CloudMediaTotal();

        public CloudStorageFolder SelectKind(CloudFileKind kind) {
            if (kind == CloudFileKind.All) return this;
            var media = kind == CloudFileKind.Photo ? Photos : kind == CloudFileKind.Video ? Videos :
                new CloudMediaTotal { Files = Files - Photos.Files - Videos.Files, KnownBytes = KnownBytes - Photos.KnownBytes - Videos.KnownBytes,
                    UnknownSizes = UnknownSizes - Photos.UnknownSizes - Videos.UnknownSizes };
            return new CloudStorageFolder { Path = Path, Files = media.Files, KnownBytes = media.KnownBytes, UnknownSizes = media.UnknownSizes };
        }
    }

    internal sealed partial class CloudQuota {
        public long? Total, Used, Free, Trashed, Other;

        public static CloudQuota Parse(string json) {
            var fields = new JavaScriptSerializer { MaxJsonLength = 65536 }.DeserializeObject(json) as Dictionary<string, object>;
            if (fields == null) throw new InvalidDataException("Invalid cloud quota.");
            var quota = new CloudQuota {
                Total = Bytes(fields, "total"), Used = Bytes(fields, "used"), Free = Bytes(fields, "free"),
                Trashed = Bytes(fields, "trashed"), Other = Bytes(fields, "other")
            };
            return quota.Total.HasValue || quota.Used.HasValue || quota.Free.HasValue || quota.Trashed.HasValue || quota.Other.HasValue ? quota : null;
        }

        static long? Bytes(Dictionary<string, object> fields, string key) {
            object value;
            long size;
            return fields.TryGetValue(key, out value) &&
                Int64.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.None, CultureInfo.InvariantCulture, out size) ? (long?)size : null;
        }
    }

    internal sealed class CloudStorageReport {
        internal const int ItemLimit = 200000;
        readonly Dictionary<string, CloudStorageFolder> folders = new Dictionary<string, CloudStorageFolder>(StringComparer.Ordinal);
        readonly List<CloudStorageFile> files = new List<CloudStorageFile>();
        public CloudQuota Quota;
        public string LocalRoot;
        public bool LimitReached, ListingFailed, QuotaOnly;
        public int UnknownSizes, Skipped;
        public long KnownBytes, LocalFileBytes, OnlineFileBytes;
        public int Count { get { return files.Count; } }
        public bool Partial { get { return Skipped != 0 || LimitReached || ListingFailed; } }

        public static string Parent(string path) {
            int separator = path.LastIndexOf('/');
            return separator < 0 ? "" : path.Substring(0, separator);
        }

        void Add(CloudStorageFile item) {
            if (String.IsNullOrEmpty(item.Path) || item.Path[0] == '/' || item.Path.EndsWith("/", StringComparison.Ordinal)) {
                Skipped++;
                return;
            }
            files.Add(item);
            if (item.Bytes < 0) UnknownSizes++;
            else {
                KnownBytes = checked(KnownBytes + item.Bytes);
                if (item.Availability == CloudAvailability.Local) LocalFileBytes += item.Bytes;
                if (item.Availability == CloudAvailability.Online) OnlineFileBytes += item.Bytes;
            }
            for (string parent = Parent(item.Path); parent.Length != 0; parent = Parent(parent)) {
                CloudStorageFolder folder;
                if (!folders.TryGetValue(parent, out folder)) {
                    folder = new CloudStorageFolder { Path = parent };
                    folders.Add(parent, folder);
                }
                folder.Files++;
                if (item.Bytes < 0) folder.UnknownSizes++;
                else folder.KnownBytes += item.Bytes;
                CloudMediaTotal media = item.Kind == CloudFileKind.Photo ? folder.Photos : item.Kind == CloudFileKind.Video ? folder.Videos : null;
                if (media != null) {
                    media.Files++;
                    if (item.Bytes < 0) media.UnknownSizes++;
                    else media.KnownBytes += item.Bytes;
                }
            }
        }

        void Sort() {
            files.Sort((left, right) => {
                int size = right.Bytes.CompareTo(left.Bytes);
                return size != 0 ? size : StringComparer.Ordinal.Compare(left.Path, right.Path);
            });
        }

        public IEnumerable<CloudStorageFile> LargestFiles(string scope, string filter) {
            return LargestFiles(scope, filter, CloudFileKind.All);
        }

        public IEnumerable<CloudStorageFile> LargestFiles(string scope, string filter, CloudFileKind kind) {
            string prefix = scope.Length == 0 ? "" : scope + "/";
            return files.Where(x => x.Path.StartsWith(prefix, StringComparison.Ordinal) && Matches(x.Path, filter) && (kind == CloudFileKind.All || x.Kind == kind));
        }

        public IEnumerable<CloudStorageFolder> LargestFolders(string scope, string filter) {
            return LargestFolders(scope, filter, CloudFileKind.All);
        }

        public IEnumerable<CloudStorageFolder> LargestFolders(string scope, string filter, CloudFileKind kind) {
            string prefix = scope.Length == 0 ? "" : scope + "/";
            var visible = filter.Length == 0 ? folders.Values.Where(x => Parent(x.Path) == scope) :
                folders.Values.Where(x => x.Path.StartsWith(prefix, StringComparison.Ordinal) && Matches(x.Path, filter));
            return visible.Select(x => x.SelectKind(kind)).Where(x => x.Files > 0).OrderByDescending(x => x.KnownBytes).ThenBy(x => x.Path, StringComparer.Ordinal);
        }

        public CloudMediaTotal MediaTotal(CloudFileKind kind) {
            var result = new CloudMediaTotal();
            foreach (var item in files.Where(x => kind == CloudFileKind.All || x.Kind == kind)) {
                result.Files++;
                if (item.Bytes < 0) result.UnknownSizes++;
                else result.KnownBytes += item.Bytes;
            }
            return result;
        }

        static bool Matches(string path, string filter) {
            return filter.Length == 0 || path.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        public static CloudStorageReport ParseListing(TextReader input, CancellationToken token, int limit = ItemLimit, Action<int> progress = null) {
            var report = new CloudStorageReport();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int seen = 0;
            using (var reader = new TextFieldParser(input)) {
                reader.SetDelimiters(",");
                reader.HasFieldsEnclosedInQuotes = true;
                reader.TrimWhiteSpace = false;
                while (!reader.EndOfData) {
                    token.ThrowIfCancellationRequested();
                    if (seen++ >= limit) { report.LimitReached = true; break; }
                    string[] row;
                    try { row = reader.ReadFields(); }
                    catch (MalformedLineException) { report.Skipped++; continue; }
                    long size;
                    if (row == null || (row.Length != 3 && row.Length != 4) ||
                        !Int64.TryParse(row[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out size) || size < -1) {
                        report.Skipped++;
                        continue;
                    }
                    if (row[2].Length != 0 && !ids.Add(row[2])) continue;
                    string mime = row.Length == 4 && row[3].Length <= 255 && !row[3].Any(Char.IsControl) ? row[3] : null;
                    report.Add(new CloudStorageFile { Path = row[0], Bytes = size, Id = row[2], MimeType = mime });
                    if (progress != null && report.Count % 1000 == 0) progress(report.Count);
                }
            }
            token.ThrowIfCancellationRequested();
            report.Sort();
            return report;
        }

        public static CloudStorageReport ScanLocal(string path, bool oneDrive, CancellationToken token) {
            string root = Args.LocalPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (root.Length == 2) root += Path.DirectorySeparatorChar;
            var report = new CloudStorageReport { LocalRoot = root };
            for (string folder = root; folder != null; folder = Path.GetDirectoryName(folder)) {
                CloudEntry entry = CloudFiles.Read(folder);
                if (!entry.Directory || (entry.Reparse && !entry.Cloud))
                    throw new IOException(L.T("Selecione uma pasta local ou sincronizada em vez de um atalho.", "Select a local or synced folder instead of a shortcut."));
            }
            var pending = new Stack<string>();
            pending.Push(root);
            int prefixLength = root.TrimEnd(Path.DirectorySeparatorChar).Length + 1;
            int seen = 0;
            while (pending.Count != 0 && !report.LimitReached) {
                token.ThrowIfCancellationRequested();
                try {
                    foreach (string itemPath in Directory.EnumerateFileSystemEntries(pending.Pop())) {
                        token.ThrowIfCancellationRequested();
                        if (seen++ >= ItemLimit) { report.LimitReached = true; break; }
                        try {
                            var item = CloudFiles.Read(itemPath);
                            if (item.Reparse && !item.Cloud) { report.Skipped++; continue; }
                            if (item.Directory) { pending.Push(itemPath); continue; }
                            var availability = item.OnlineOnly ? CloudAvailability.Online :
                                oneDrive || item.Cloud ? CloudAvailability.Local : CloudAvailability.Unreported;
                            report.Add(new CloudStorageFile {
                                Path = itemPath.Substring(prefixLength).Replace(Path.DirectorySeparatorChar, '/'),
                                Bytes = item.Bytes, Availability = availability
                            });
                        } catch (IOException) { report.Skipped++; }
                        catch (UnauthorizedAccessException) { report.Skipped++; }
                        catch (System.ComponentModel.Win32Exception) { report.Skipped++; }
                    }
                } catch (IOException) { report.Skipped++; }
                catch (UnauthorizedAccessException) { report.Skipped++; }
            }
            report.Sort();
            return report;
        }
    }
}
