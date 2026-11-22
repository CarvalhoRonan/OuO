using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Aurora {
    internal static partial class CloudConnection {
        internal const string GoogleAppResource = "OuO.GoogleOAuth.json";

        public static bool HasPublishedGoogleClient {
            get { return Assembly.GetExecutingAssembly().GetManifestResourceInfo(GoogleAppResource) != null; }
        }

        internal static string PublishedGoogleSeed() {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GoogleAppResource)) {
                if (stream == null) return null;
                using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                    var buffer = new char[65537];
                    int length = reader.ReadBlock(buffer, 0, buffer.Length);
                    if (length == buffer.Length) throw new InvalidDataException("Google application metadata is too large.");
                    return GoogleSeed(new string(buffer, 0, length));
                }
            }
        }
    }
}
