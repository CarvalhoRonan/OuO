using System;
using System.Collections.Generic;
using System.Linq;

namespace Aurora {
    internal enum CloudFileKind { All, Photo, Video, Other }

    internal sealed class CloudMediaTotal {
        public int Files, UnknownSizes;
        public long KnownBytes;
    }

    internal static class CloudMedia {
        static readonly HashSet<string> Photos = new HashSet<string>(
            "jpg jpeg jpe jfif png apng gif bmp webp heic heif hif avif tif tiff dng nef nrw cr2 cr3 arw sr2 srf orf rw2 raf pef srw raw mpo jxl".Split(' '), StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> Videos = new HashSet<string>(
            "mp4 m4v mov qt avi mkv webm mpg mpeg m2v m2ts 3gp 3g2 wmv asf flv vob ogv av1".Split(' '), StringComparer.OrdinalIgnoreCase);

        public static CloudFileKind Kind(string path, string mime) {
            string normalized = (path ?? "").Replace('\\', '/');
            // TypeScript declarations are code; .ts/.mts alone can also name transport-stream videos.
            if (normalized.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(".d.mts", StringComparison.OrdinalIgnoreCase) ||
                ((normalized.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(".mts", StringComparison.OrdinalIgnoreCase)) &&
                 ("/" + normalized).IndexOf("/node_modules/", StringComparison.OrdinalIgnoreCase) >= 0)) return CloudFileKind.Other;
            string contentType = (mime ?? "").Split(';')[0].Trim();
            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return CloudFileKind.Photo;
            if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return CloudFileKind.Video;
            if (contentType.Length != 0 && !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) &&
                !contentType.Equals("application/x-raw", StringComparison.OrdinalIgnoreCase)) return CloudFileKind.Other;
            int dot = normalized.LastIndexOf('.');
            int slash = normalized.LastIndexOf('/');
            string extension = dot > slash ? normalized.Substring(dot + 1) : "";
            return Photos.Contains(extension) ? CloudFileKind.Photo : Videos.Contains(extension) ? CloudFileKind.Video : CloudFileKind.Other;
        }
    }

    internal sealed class OneDriveSpaceSummary {
        public long? Remaining, Classified;
        public bool Incomplete, TotalsConflict;
        public static OneDriveSpaceSummary From(CloudStorageReport report) {
            var result = new OneDriveSpaceSummary { Incomplete = report.QuotaOnly || report.Partial || report.UnknownSizes != 0 };
            if (report.QuotaOnly || report.Quota == null || !report.Quota.Used.HasValue) return result;
            long used = report.Quota.Used.Value;
            long listed = report.KnownBytes;
            long trash = report.Quota.Trashed ?? 0;
            if (listed > used || trash > used - listed) { result.TotalsConflict = true; return result; }
            result.Classified = listed + trash;
            result.Remaining = used - result.Classified.Value;
            return result;
        }
    }
}
