using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Aurora {
    internal enum CloudProvider { GoogleDrive, OneDrive }

    internal static partial class CloudConnection {
        const string EngineVersion = "v1.76.0";
        const long PackageLimit = 64L * 1024 * 1024;
        const long ExecutableLimit = 128L * 1024 * 1024;
        const string GoogleScope = "drive.metadata.readonly";
        const string MicrosoftScopes = "Files.Read Files.Read.All offline_access";
        // rclone's disable_site_permission switch overrides custom scopes with
        // defaults that still allow writes. The explicit list omits Sites.Read.All.
        internal const string OneDriveProfile = "[ouo-onedrive]\ntype = onedrive\naccess_scopes = " + MicrosoftScopes + "\n";
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OuO.CloudAnalyzer.Config.1");
        static readonly Encoding Utf8 = new UTF8Encoding(false);
        static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuroraUtility", "CloudAnalyzer"); } }
        public static bool OnlineSupported { get { return Environment.OSVersion.Version.Major >= 10; } }

        [StructLayout(LayoutKind.Sequential)]
        struct SystemInfo {
            public ushort Architecture, Reserved;
            public uint PageSize;
            public IntPtr MinimumAddress, MaximumAddress, ActiveProcessors;
            public uint ProcessorCount, ProcessorType, AllocationGranularity;
            public ushort ProcessorLevel, ProcessorRevision;
        }

        [DllImport("kernel32.dll")]
        static extern void GetNativeSystemInfo(out SystemInfo info);

        sealed class Engine {
            public readonly string Name, PackageHash, ExecutableHash;
            public Engine(string architecture, string packageHash, string executableHash) {
                Name = "rclone-" + EngineVersion + "-windows-" + architecture;
                PackageHash = packageHash;
                ExecutableHash = executableHash;
            }
            public string Path { get { return System.IO.Path.Combine(Folder, Name + ".exe"); } }
        }

        static Engine CurrentEngine() {
            if (!OnlineSupported)
                throw new PlatformNotSupportedException(L.T("A análise online requer Windows 10 ou mais recente.", "Online analysis requires Windows 10 or newer."));
            SystemInfo info;
            GetNativeSystemInfo(out info);
            switch (info.Architecture) {
                case 12:
                    return new Engine("arm64", "C406C8A69A5A408E50624AA990FBB0A6266B3A4E83782A94DE3CB96F50F86F10", "666B57912D75ECC67B38960C4E28448E94B61D7977F5F709E25C0C7A3C6D7FCD");
                case 9:
                    return new Engine("amd64", "8B4FD2737ADE4E70C057EF4A49DEB7CFFE94B0FB9D7587C189D149823F21A1DC", "ACD80683E23151E93BBE0D6FFA74D26E542EC510332F630BEECB9944F8D1A623");
                case 0:
                    return new Engine("386", "B9C8D062FF61E2ADDFC31C1981219D90F99E0012CBA45B649F728082AD810D5C", "15AA6FC8C770CC4ADF15081CA725F725143CB1F86A089E5785653BA9DEC8B2F1");
                default:
                    throw new PlatformNotSupportedException(L.T("Arquitetura indisponível para a consulta online.", "Online analysis is unavailable on this architecture."));
            }
        }

        static string Remote(CloudProvider provider) {
            return provider == CloudProvider.GoogleDrive ? "ouo-google" : "ouo-onedrive";
        }

        static string SavedConfig(CloudProvider provider) {
            return Path.Combine(Folder, Remote(provider) + ".dat");
        }

        public static bool Ready(CloudProvider provider) {
            return File.Exists(SavedConfig(provider));
        }

        static FileStream Lock() {
            Directory.CreateDirectory(Folder);
            try { return new FileStream(Path.Combine(Folder, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { throw new IOException(L.T("Já existe uma consulta ou conexão em andamento no analisador.", "Cloud analysis or sign-in is already in progress.")); }
        }

        public static void Disconnect(CloudProvider provider) {
            using (Lock()) File.Delete(SavedConfig(provider));
        }

        static string Digest(Stream input) {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }

        static FileStream VerifiedExecutable(Engine engine) {
            var file = new FileStream(engine.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try {
                if (Digest(file) != engine.ExecutableHash)
                    throw new InvalidDataException(L.T("O arquivo do rclone não passou na verificação. Use Reconectar para baixar a cópia oficial.", "The rclone file failed verification. Use Reconnect to download the official copy."));
                return file;
            } catch {
                file.Dispose();
                throw;
            }
        }

        static void EnsureEngine(Engine engine, string workspace, CancellationToken token, Action<string> progress) {
            try {
                using (VerifiedExecutable(engine)) return;
            } catch (FileNotFoundException) {}
            catch (InvalidDataException) {}

            progress(L.T("Baixando o componente oficial rclone…", "Downloading the official rclone component…"));
            string archivePath = Path.Combine(workspace, "rclone.zip");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create("https://downloads.rclone.org/" + EngineVersion + "/" + engine.Name + ".zip");
            request.AllowAutoRedirect = false;
            request.Timeout = 20000;
            request.ReadWriteTimeout = 30000;
            using (token.Register(request.Abort))
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var input = response.GetResponseStream())
            using (var output = new FileStream(archivePath, FileMode.CreateNew)) {
                if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > PackageLimit)
                    throw new InvalidDataException("Unexpected rclone download.");
                var buffer = new byte[65536];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) != 0) {
                    token.ThrowIfCancellationRequested();
                    if (output.Length + count > PackageLimit) throw new InvalidDataException("rclone download exceeded its size limit.");
                    output.Write(buffer, 0, count);
                }
            }

            string extracted = Path.Combine(workspace, "rclone.exe");
            using (var file = File.OpenRead(archivePath)) {
                if (Digest(file) != engine.PackageHash)
                    throw new InvalidDataException(L.T("O download do rclone não passou na verificação.", "The rclone download failed verification."));
                file.Position = 0;
                using (var archive = new ZipArchive(file, ZipArchiveMode.Read)) {
                    var entry = archive.GetEntry(engine.Name + "/rclone.exe");
                    if (entry == null || entry.Length > ExecutableLimit) throw new InvalidDataException("Missing rclone executable in the verified package.");
                    using (var source = entry.Open())
                    using (var destination = new FileStream(extracted, FileMode.CreateNew)) source.CopyTo(destination);
                }
            }
            using (var file = File.OpenRead(extracted)) {
                if (Digest(file) != engine.ExecutableHash) throw new InvalidDataException("Unexpected rclone executable digest.");
            }
            token.ThrowIfCancellationRequested();
            ReplaceOwnedFile(extracted, engine.Path);
        }

        static void ReplaceOwnedFile(string incoming, string destination) {
            if (File.Exists(destination)) File.Replace(incoming, destination, null);
            else File.Move(incoming, destination);
        }

        static IDictionary<string, object> Json(string json) {
            return new JavaScriptSerializer { MaxJsonLength = 65536 }.DeserializeObject(json) as IDictionary<string, object>;
        }

        static string StringValue(IDictionary<string, object> values, string key) {
            object value;
            return values != null && values.TryGetValue(key, out value) ? value as string ?? "" : "";
        }

        internal static string GoogleSeed(string json) {
            var values = Json(json);
            object installed;
            var client = values != null && values.TryGetValue("installed", out installed) ? installed as IDictionary<string, object> : null;
            string id = StringValue(client, "client_id");
            string secret = StringValue(client, "client_secret");
            if (!Regex.IsMatch(id, @"\A[0-9a-z-]{8,160}\.apps\.googleusercontent\.com\z") ||
                !Regex.IsMatch(secret, @"\A[A-Za-z0-9_-]{8,200}\z"))
                throw new InvalidDataException(L.T("Selecione o JSON do cliente OAuth do tipo App para computador no Google Cloud.", "Select the OAuth Desktop app client JSON from Google Cloud."));
            return "[ouo-google]\ntype = drive\nscope = " + GoogleScope + "\nclient_id = " + id + "\nclient_secret = " + secret + "\n";
        }

        static string Seed(CloudProvider provider, string googleJson) {
            if (provider == CloudProvider.OneDrive)
                return OneDriveProfile;
            if (String.IsNullOrEmpty(googleJson)) {
                string application = PublishedGoogleSeed();
                if (application == null)
                    throw new InvalidOperationException(L.T("O cadastro do OuO no Google ainda precisa ser configurado nesta compilação.",
                        "The OuO Google registration still needs to be configured for this build."));
                return application;
            }
            try {
                using (var reader = new StreamReader(googleJson, Utf8)) {
                    char[] buffer = new char[65537];
                    int count = reader.ReadBlock(buffer, 0, buffer.Length);
                    if (count == buffer.Length) throw new InvalidDataException();
                    return GoogleSeed(new string(buffer, 0, count));
                }
            } catch (Exception error) {
                if (!(error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidOperationException || error is InvalidDataException)) throw;
                throw new InvalidDataException(L.T("Não foi possível ler o JSON do cliente OAuth para computador do Google.", "The Google desktop OAuth client JSON could not be read."));
            }
        }

        static bool HasToken(string clearConfig, CloudProvider provider) {
            string type = provider == CloudProvider.GoogleDrive ? "drive" : "onedrive";
            string scopeKey = provider == CloudProvider.GoogleDrive ? "scope" : "access_scopes";
            string scope = provider == CloudProvider.GoogleDrive ? GoogleScope : MicrosoftScopes;
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            bool selected = false;
            foreach (string raw in File.ReadLines(clearConfig)) {
                string line = raw.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal)) selected = line.Equals("[" + Remote(provider) + "]", StringComparison.Ordinal);
                int separator = line.IndexOf('=');
                if (selected && separator > 0) entries[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
            string actualType, actualScope, token;
            if (!entries.TryGetValue("type", out actualType) || actualType != type ||
                !entries.TryGetValue(scopeKey, out actualScope) || actualScope != scope ||
                !entries.TryGetValue("token", out token)) return false;
            if (provider == CloudProvider.OneDrive && (!entries.ContainsKey("drive_id") || entries["drive_id"].Length == 0 ||
                entries.ContainsKey("disable_site_permission"))) return false;
            try { return StringValue(Json(token), "access_token").Length > 0; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        static void SaveConfig(string clearConfig, string config) {
            string pending = Path.Combine(Path.GetDirectoryName(clearConfig), "protected.dat");
            byte[] clear = File.ReadAllBytes(clearConfig);
            try {
                File.WriteAllBytes(pending, ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser));
                ReplaceOwnedFile(pending, config);
            } finally { Array.Clear(clear, 0, clear.Length); }
        }

        static T WithWorkspace<T>(Func<string, T> operation) {
            using (Lock()) {
                string workspace = Path.Combine(Folder, "active-session");
                if (Directory.Exists(workspace)) {
                    if ((File.GetAttributes(workspace) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException(L.T("A pasta de trabalho da análise foi redirecionada.", "The cloud analysis workspace was redirected."));
                    Directory.Delete(workspace, true);
                }
                Directory.CreateDirectory(workspace);
                try { return operation(workspace); }
                finally {
                    // rclone can leave a temporary or previous copy of its config in this directory.
                    try { Directory.Delete(workspace, true); } catch (IOException) {} catch (UnauthorizedAccessException) {}
                }
            }
        }

        static ProcessStartInfo StartInfo(Engine engine, string config, string arguments) {
            var start = new ProcessStartInfo(engine.Path, arguments + " --config " + Args.Q(config)) {
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (string key in start.EnvironmentVariables.Keys.Cast<string>().Where(x => x.StartsWith("RCLONE_", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.EnvironmentVariables.Remove(key);
            start.StandardOutputEncoding = Utf8;
            return start;
        }

        static void CheckTime(Stopwatch watch, TimeSpan duration, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            if (watch.Elapsed >= duration)
                throw new TimeoutException(L.T("O tempo de espera pela conexão foi excedido.", "The connection timed out."));
        }

        static void Stop(Process process) {
            try { if (!process.HasExited) { process.Kill(); process.WaitForExit(3000); } }
            catch (InvalidOperationException) {}
            catch (System.ComponentModel.Win32Exception) {}
        }

        public static void Connect(CloudProvider provider, string googleJson, CancellationToken token, Action<string> progress) {
            string seed = Seed(provider, googleJson);
            try {
                WithWorkspace(workspace => {
                    Engine engine = CurrentEngine();
                    EnsureEngine(engine, workspace, token, progress);
                    string clearConfig = Path.Combine(workspace, "account.conf");
                    File.WriteAllText(clearConfig, seed, Utf8);
                    progress(L.T("Aguardando autorização no navegador…", "Waiting for browser authorization…"));
                    using (VerifiedExecutable(engine)) {
                        if (provider == CloudProvider.OneDrive) {
                            ConnectOneDrive(engine, clearConfig, token, progress);
                        } else {
                            ConnectGoogle(engine, clearConfig, seed, token);
                        }
                        if (!HasToken(clearConfig, provider))
                            throw new IOException(L.T("A autorização não foi concluída. Revise as etapas da conexão e tente novamente.", "Authorization did not complete. Review the connection steps and try again."));
                    }
                    token.ThrowIfCancellationRequested();
                    SaveConfig(clearConfig, SavedConfig(provider));
                    return true;
                });
            } catch (WebException) {
                token.ThrowIfCancellationRequested();
                throw new IOException(L.T("Não foi possível baixar o rclone. Verifique a conexão e tente novamente.", "Could not download rclone. Check your connection and retry."));
            }
        }

        static Tuple<int, T> ReadCommand<T>(Engine engine, string config, string arguments, int minutes,
            CancellationToken token, Func<StreamReader, T> parse, Func<T, bool> doneEarly = null) {
            token.ThrowIfCancellationRequested();
            using (var process = Process.Start(StartInfo(engine, config, arguments + " --ask-password=false --log-level ERROR --stats 0 --retries 1 --low-level-retries 3 --contimeout 10s --timeout 45s"))) {
                try {
                    process.BeginErrorReadLine();
                    var read = Task.Factory.StartNew(() => parse(process.StandardOutput), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    var watch = Stopwatch.StartNew();
                    var duration = TimeSpan.FromMinutes(minutes);
                    while (!read.IsCompleted) {
                        CheckTime(watch, duration, token);
                        Thread.Sleep(80);
                    }
                    token.ThrowIfCancellationRequested();
                    T data = read.GetAwaiter().GetResult();
                    if (doneEarly != null && doneEarly(data)) return Tuple.Create(0, data);
                    while (!process.WaitForExit(80)) CheckTime(watch, duration, token);
                    token.ThrowIfCancellationRequested();
                    return Tuple.Create(process.ExitCode, data);
                } finally { Stop(process); }
            }
        }

        static string ReadQuotaText(StreamReader reader) {
            var buffer = new char[65537];
            int length = reader.ReadBlock(buffer, 0, buffer.Length);
            if (length == buffer.Length) throw new InvalidDataException("Unexpected quota response length.");
            return new string(buffer, 0, length);
        }

        static T WithAccount<T>(CloudProvider provider, CancellationToken token, Func<Engine, string, T> read) {
            return WithWorkspace(workspace => {
                if (!Ready(provider)) throw new IOException(L.T("Conecte a conta antes de iniciar a análise.", "Connect the account before scanning."));
                Engine engine = CurrentEngine();
                string config = SavedConfig(provider);
                string clearConfig = Path.Combine(workspace, "account.conf");
                byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(config), Entropy, DataProtectionScope.CurrentUser);
                try { File.WriteAllBytes(clearConfig, clear); }
                finally { Array.Clear(clear, 0, clear.Length); }

                using (VerifiedExecutable(engine))
                    try {
                        token.ThrowIfCancellationRequested();
                        return read(engine, clearConfig);
                    } finally {
                        // A successful token refresh should survive later scans and cancel operations.
                        if (HasToken(clearConfig, provider)) SaveConfig(clearConfig, config);
                    }
            });
        }

        static CloudQuota AccountQuota(Engine engine, string config, CloudProvider provider, CancellationToken token, Action<string> progress) {
            CloudQuota quota = null;
            progress(L.T("Consultando a cota da conta…", "Checking your account quota…"));
            try {
                var result = ReadCommand(engine, config, "about " + Remote(provider) + ": --json", 1, token, ReadQuotaText);
                if (result.Item1 == 0) quota = CloudQuota.Parse(result.Item2);
            } catch (ArgumentException) {} catch (InvalidOperationException) {} catch (InvalidDataException) {} catch (TimeoutException) {}
            if (provider == CloudProvider.GoogleDrive) {
                progress(L.T("Separando o uso entre os serviços do Google…", "Checking storage by Google service…"));
                quota = GoogleServices(config, quota, token);
            }
            return quota;
        }

        public static CloudQuota ReadGoogleAccountQuota(CancellationToken token, Action<string> progress) {
            return ReadAccountQuota(CloudProvider.GoogleDrive, token, progress);
        }

        public static CloudQuota ReadAccountQuota(CloudProvider provider, CancellationToken token, Action<string> progress) {
            return WithAccount(provider, token, (engine, config) => AccountQuota(engine, config, provider, token, progress));
        }

        public static CloudStorageReport Scan(CloudProvider provider, CancellationToken token, Action<string> progress) {
            return WithAccount(provider, token, (engine, config) => {
                CloudQuota quota = AccountQuota(engine, config, provider, token, progress);
                progress(L.T("Somando os tamanhos dos arquivos e pastas…", "Adding up file and folder sizes…"));
                string options = provider == CloudProvider.GoogleDrive ?
                    " --drive-size-as-quota --drive-auth-owner-only --drive-skip-shortcuts --drive-show-all-gdocs" :
                    " --onedrive-delta --onedrive-expose-onenote-files";
                var result = ReadCommand(engine, config, "lsf " + Remote(provider) + ": --recursive --files-only --csv --separator , --format psim" + options, 20, token,
                    reader => CloudStorageReport.ParseListing(reader, token, CloudStorageReport.ItemLimit,
                        count => progress(count.ToString("N0") + L.T(" arquivos consultados…", " files checked…"))), report => report.LimitReached);
                result.Item2.Quota = quota;
                result.Item2.ListingFailed = result.Item1 != 0;
                return result.Item2;
            });
        }
    }
}
