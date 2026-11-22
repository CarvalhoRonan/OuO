using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Aurora {
    internal static class CloudStorageChecks {
        internal static CloudStorageReport Sample() {
            const string csv = "\"Vídeos/2026/Evento, parte \"\"A\"\".mp4\",7200000000,owned_video_1\n" +
                "Vídeos/Evento antigo.mp4,3300000000,owned_video_2\n" +
                "Fotos/Arquivo.zip,1200000000,owned_photos\n" +
                "Documentos/Arquivo sem tamanho,-1,owned_docs_1\n" +
                "\"Documentos/Notas\nda reunião.txt\",2800,owned_docs_2\n" +
                "Atalho repetido.mp4,7200000000,owned_video_1";
            var report = CloudStorageReport.ParseListing(new StringReader(csv), CancellationToken.None);
            report.Quota = CloudQuota.ParseGoogleServices("{\"quotaBytesTotal\":\"15000000000\",\"quotaBytesUsed\":\"12300002800\",\"quotaBytesUsedAggregate\":\"14400002800\",\"quotaBytesUsedInTrash\":\"600000000\",\"quotaBytesByService\":[{\"serviceName\":\"DRIVE\",\"bytesUsed\":\"12300002800\"},{\"serviceName\":\"GMAIL\",\"bytesUsed\":\"1200000000\"},{\"serviceName\":\"PHOTOS\",\"bytesUsed\":\"800000000\"}]}");
            return report;
        }

        public static void Run(Action<bool, string> check, string folder) {
            var report = Sample();
            check(report.Count == 5 && report.UnknownSizes == 1 && report.KnownBytes == 11700002800L && !report.Partial,
                "cloud inventory deduplicates file IDs, handles quoted comma/quote and multiline UTF-8 paths, and separates unknown sizes");
            var root = report.LargestFolders("", "").ToArray();
            check(root.Length == 3 && root[0].Path == "Vídeos" && root[0].KnownBytes == 10500000000L && root[0].Files == 2 &&
                report.LargestFolders("Vídeos", "").Single().KnownBytes == 7200000000L &&
                report.LargestFiles("Vídeos/2026", "parte").Single().Path.EndsWith("parte \"A\".mp4") &&
                report.LargestFiles("", "Notas").Single().Path.Contains("Notas\nda"), "cloud ranking rolls sizes up, filters within a folder, and retains display paths");
            check(report.Quota.Total == 15000000000L && report.Quota.Other == 2100000000L && report.Quota.Trashed == 600000000L &&
                CloudQuota.Parse("{\"used\":0}").Free == null && CloudQuota.Parse("{}") == null,
                "cloud quota distinguishes provider-reported zero, missing quota, and separate trash and other services");
            CheckGoogleServices(check);
            CloudMediaChecks.Run(check);
            var capped = CloudStorageReport.ParseListing(new StringReader("a.txt,1,1\nmismatch\nb.txt,2,2\nc.txt,3,3\n"), CancellationToken.None, 3);
            check(capped.Partial && capped.LimitReached && capped.Skipped == 1 && capped.Count == 2,
                "incomplete cloud inventory is marked partial at the record cap and invalid rows are skipped");
            using (var stop = new CancellationTokenSource()) {
                stop.Cancel();
                bool canceled = false;
                try { CloudStorageReport.ParseListing(new StringReader("one.txt,12,item-one"), stop.Token); }
                catch (OperationCanceledException) { canceled = true; }
                check(canceled, "cloud listing stops on cancellation");
            }
            string seed = CloudConnection.GoogleSeed("{\"installed\":{\"client_id\":\"12345678-test.apps.googleusercontent.com\",\"client_secret\":\"fixture-key-123\"}}");
            bool invalid = false;
            try { CloudConnection.GoogleSeed("{\"installed\":{\"client_id\":\"12345678-test.apps.googleusercontent.com\",\"client_secret\":\"bad\\ntype = local\"}}"); }
            catch (InvalidDataException) { invalid = true; }
            check(seed.Contains("scope = drive.metadata.readonly") && invalid, "Google cloud profile requests metadata access only and rejects configuration injection");
            string googleArgs = CloudConnection.GoogleAuthorizeArguments(seed);
            string encoded = googleArgs.Substring("authorize drive ".Length).Trim('"');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            var googleOptions = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
            check(googleOptions["scope"] == "drive.metadata.readonly" && googleOptions["client_id"] == "12345678-test.apps.googleusercontent.com" &&
                googleOptions["client_secret"] == "fixture-key-123" && googleOptions.Count == 3,
                "Google browser-only authorization passes only the selected desktop client and metadata scope");
            string applicationSeed = CloudConnection.PublishedGoogleSeed();
            check(CloudConnection.HasPublishedGoogleClient == (applicationSeed != null) &&
                (applicationSeed == null || (applicationSeed.Contains("scope = drive.metadata.readonly\n") && !applicationSeed.Contains("refresh_token"))),
                "published Google registration can supply browser login without an end-user JSON file and contains no account refresh token");
            check(CloudConnection.OneDriveProfile == "[ouo-onedrive]\ntype = onedrive\naccess_scopes = Files.Read Files.Read.All offline_access\n",
                "OneDrive cloud profile explicitly requests read scopes without overriding them with rclone defaults");
            CheckOneDriveConnection(check);
            string local = Path.Combine(folder, "cloud-local");
            Directory.CreateDirectory(Path.Combine(local, "Fotos"));
            File.WriteAllBytes(Path.Combine(local, "Fotos", "local.bin"), new byte[1234]);
            var scan = CloudStorageReport.ScanLocal(local, true, CancellationToken.None);
            check(scan.Count == 1 && scan.KnownBytes == 1234 && scan.LocalFileBytes == 1234 && scan.OnlineFileBytes == 0 &&
                scan.Quota == null && scan.LocalRoot == local && !scan.Partial, "local cloud scan separates reported file sizes from the online quota");
        }

        static void CheckGoogleServices(Action<bool, string> check) {
            var sample = Sample().Quota;
            check(sample.GoogleAccountUsed == 14400002800L && sample.Unattributed == 100000000L &&
                sample.Free == 599997200L && sample.Services.Count == 3 && !sample.ServicesInconsistent,
                "Google service quota reconciles the account aggregate without adding trash twice and preserves an unclassified remainder");
            var missing = CloudQuota.ParseGoogleServices("{\"quotaBytesUsedAggregate\":\"100\",\"quotaBytesUsed\":\"20\",\"quotaBytesByService\":[{\"serviceName\":\"DRIVE\",\"bytesUsed\":\"20\"},{\"serviceName\":\"PHOTOS\",\"bytesUsed\":\"30\"}]}");
            check(missing.Unattributed == 50 && !missing.Services.Any(x => x.Id == "GMAIL") && missing.Other == 80,
                "Google missing services are not invented or assigned the unclassified storage balance");
            var zero = CloudQuota.ParseGoogleServices("{\"quotaBytesUsedAggregate\":\"0\",\"quotaBytesByService\":[{\"serviceName\":\"GMAIL\",\"bytesUsed\":\"0\"}]}");
            check(zero.Services.Single().Bytes == 0 && zero.Unattributed == 0 &&
                CloudQuota.ParseGoogleServices("{\"quotaBytesUsedAggregate\":\"0\"}").Services.Count == 0 && CloudQuota.ParseGoogleServices("{}") == null,
                "Google explicit zero usage remains distinct from an unavailable service breakdown");
            var duplicate = CloudQuota.ParseGoogleServices("{\"quotaBytesUsedAggregate\":\"100\",\"quotaBytesByService\":[{\"serviceName\":\"gmail\",\"bytesUsed\":\"30\"},{\"serviceName\":\"GMAIL\",\"bytesUsed\":\"30\"},{\"serviceName\":\"DRIVE\",\"bytesUsed\":\"20\"},{\"serviceName\":\"DRIVE\",\"bytesUsed\":\"25\"}]}");
            check(duplicate.ServiceRowsInvalid && duplicate.Services.Count == 1 && duplicate.Services.Single().Id == "GMAIL" && duplicate.Unattributed == 70,
                "Google equal service duplicates are counted once and conflicting values remain unclassified");
            var invalid = CloudQuota.ParseGoogleServices("{\"quotaBytesUsedAggregate\":\"100\",\"quotaBytesByService\":[{\"serviceName\":\"PHOTOS\",\"bytesUsed\":\"-1\"},{\"serviceName\":\"GMAIL\",\"bytesUsed\":\"9223372036854775808\"},{\"serviceName\":\"BAD\\nNAME\",\"bytesUsed\":\"7\"},{\"serviceName\":\"NEW_SERVICE\",\"bytesUsed\":\"40\"}]}");
            check(invalid.ServiceRowsInvalid && invalid.Services.Single().Id == "NEW_SERVICE" && invalid.Unattributed == 60,
                "Google invalid byte counts and control characters cannot turn into negative, overflowed or misleading service rows");
            var mismatch = CloudQuota.ParseGoogleServices("{\"quotaBytesUsedAggregate\":\"10\",\"quotaBytesByService\":[{\"serviceName\":\"PHOTOS\",\"bytesUsed\":\"11\"}]}");
            var overflow = CloudQuota.ParseGoogleServices("{\"quotaBytesUsedAggregate\":\"9223372036854775807\",\"quotaBytesByService\":[{\"serviceName\":\"PHOTOS\",\"bytesUsed\":\"9223372036854775807\"},{\"serviceName\":\"GMAIL\",\"bytesUsed\":\"1\"}]}");
            check(mismatch.ServicesInconsistent && !mismatch.Unattributed.HasValue && overflow.ServicesInconsistent && !overflow.Unattributed.HasValue,
                "Google service totals above the account aggregate cannot overflow or produce a fabricated negative remainder");
            var noAggregate = CloudQuota.ParseGoogleServices("{\"quotaBytesByService\":[{\"serviceName\":\"GMAIL\",\"bytesUsed\":\"30\"}]}");
            check(!noAggregate.GoogleAccountUsed.HasValue && !noAggregate.Unattributed.HasValue && noAggregate.Services.Single().Bytes == 30,
                "Google partial service data does not replace a missing account total with an assumed sum");
            var tooMany = new { quotaBytesUsedAggregate = "200", quotaBytesByService = Enumerable.Range(0, 129).Select(x => new { serviceName = "SERVICE_" + x, bytesUsed = "1" }).ToArray() };
            var capped = CloudQuota.ParseGoogleServices(new JavaScriptSerializer().Serialize(tooMany));
            check(capped.ServiceRowsInvalid && capped.Services.Count == 128 && capped.Unattributed == 72,
                "Google oversized service lists are bounded and explicitly retain incomplete coverage");
            check(CloudQuota.Parse("{\"used\":123,\"other\":77}").GoogleAccountUsed == 200 &&
                !CloudQuota.Parse("{\"used\":9223372036854775807,\"other\":1}").GoogleAccountUsed.HasValue,
                "Google fallback account totals preserve provider values without integer overflow");
        }

        static void CheckOneDriveConnection(Action<bool, string> check) {
            var serializer = new JavaScriptSerializer();
            string response = "Paste the following into your remote machine --->\n" +
                Convert.ToBase64String(Encoding.UTF8.GetBytes(serializer.Serialize(new {
                    token = serializer.Serialize(new { access_token = "owned-test-access", refresh_token = "owned-test-refresh" })
                }))).TrimEnd('=') + "\n<---End paste\n";
            var requests = new List<string>();
            string config = CloudConnection.CompleteOneDriveAuthorization(response, (path, token) => {
                if (token != "owned-test-access") throw new InvalidOperationException("Wrong access token in owned request.");
                requests.Add(path);
                return path.StartsWith("/me/drive?") ?
                    "{\"id\":\"PRIMARY-DRIVE\",\"driveType\":\"personal\"}" : "{\"id\":\"PRIMARY-ROOT\"}";
            });
            check(requests.SequenceEqual(new[] { "/me/drive?$select=id,driveType", "/drives/PRIMARY-DRIVE/root?$select=id" }) &&
                config.Contains("drive_id = PRIMARY-DRIVE\n") && config.Contains("drive_type = personal\n"),
                "OneDrive sign-in confirms the default drive directly without enumerating internal libraries or prompting for a drive");
            string business = CloudConnection.CompleteOneDriveAuthorization(response, (path, token) =>
                path.StartsWith("/me/drive?") ? "{\"id\":\"b!PRIMARY-DRIVE_ID\",\"driveType\":\"business\"}" : "{\"id\":\"BUSINESS-ROOT\"}");
            check(business.Contains("drive_id = b!PRIMARY-DRIVE_ID\n") && business.Contains("drive_type = business\n"),
                "OneDrive default-drive connection also supports business identifiers");
            int calls = 0;
            bool stopped = false;
            try {
                CloudConnection.CompleteOneDriveAuthorization(response, (path, token) => {
                    calls++;
                    if (calls == 1) return "{\"id\":\"PRIMARY-DRIVE\",\"driveType\":\"personal\"}";
                    throw new IOException("HTTP 400 - owned invalid-root fixture");
                });
            } catch (IOException) { stopped = true; }
            check(stopped && calls == 2, "OneDrive invalid-root failure exits after one attempt and cannot loop through the connection menu");
            calls = 0;
            bool rejected = false;
            try {
                CloudConnection.CompleteOneDriveAuthorization(response, (path, token) => {
                    calls++;
                    return "{\"id\":\"invalid\\ndrive_type = local\",\"driveType\":\"personal\"}";
                });
            } catch (InvalidDataException) { rejected = true; }
            check(rejected && calls == 1, "OneDrive rejects an invalid drive identifier before writing its account configuration");
            calls = 0;
            rejected = false;
            try { CloudConnection.CompleteOneDriveAuthorization("incomplete authorization", (path, token) => { calls++; return "{}"; }); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected && calls == 0, "OneDrive incomplete authorization cannot start account queries");
        }
    }

    public partial class MainWindow {
        void VerifyCloudStorageUI() {
            ShowPage("cloud-storage", true);
            body.Children.Clear();
            CloudStoragePage(CloudStorageChecks.Sample());
            var lists = AppearanceTree(body).OfType<ListBox>().ToArray();
            var folders = lists.Single(x => AutomationProperties.GetName(x) == T("Pastas", "Folders"));
            var files = lists.Single(x => AutomationProperties.GetName(x) == T("Arquivos", "Files"));
            if (folders.Items.Count != 3 || files.Items.Count != 5)
                throw new InvalidOperationException("Cloud storage preview did not show ranked folders and files.");
            var services = AppearanceTree(body).OfType<Border>().Where(x => Convert.ToString(x.Tag).StartsWith("OuO.CloudService:")).ToArray();
            if (services.Length != 4 || !services.Any(x => Convert.ToString(x.Tag) == "OuO.CloudService:GMAIL") ||
                !services.Any(x => Convert.ToString(x.Tag) == "OuO.CloudService:PHOTOS") ||
                !services.Any(x => Convert.ToString(x.Tag) == "OuO.CloudService:unattributed"))
                throw new InvalidOperationException("Google service totals or the unclassified remainder are missing from the app.");
            Capture("diagnostics/cloud-services-pt.png");
            double previousWidth = Width, previousHeight = Height;
            Width = 820; Height = 720;
            UpdateLayout();
            var serviceGrid = AppearanceTree(body).OfType<UniformGrid>().Single(x => x.Children.OfType<Border>().Any(tile =>
                Convert.ToString(tile.Tag).StartsWith("OuO.CloudService:")));
            pageScroll.ScrollToVerticalOffset(pageScroll.VerticalOffset + serviceGrid.TranslatePoint(new Point(0, 0), pageScroll).Y - 40);
            Capture("diagnostics/cloud-services-compact.png");
            if (serviceGrid.Columns > 2 || serviceGrid.Children.OfType<Border>().Any(tile =>
                tile.TranslatePoint(new Point(tile.ActualWidth, 0), serviceGrid).X > serviceGrid.ActualWidth + 1))
                throw new InvalidOperationException("Google service tiles do not fit the compact window.");
            pageScroll.ScrollToTop();
            Width = previousWidth; Height = previousHeight; UpdateLayout();
            Capture("diagnostics/cloud-storage-ranking-pt.png");
            var help = AppearanceTree(body).OfType<Button>().Single(x => Convert.ToString(x.Content) == T("Como conectar o Google", "How to connect Google"));
            if (CloudConnection.HasPublishedGoogleClient) {
                if (help.Visibility != Visibility.Collapsed || AppearanceTree(body).OfType<TextBlock>().Any(x =>
                    x.Text == T("Selecione seu arquivo de conexão e entre no Google pelo navegador. Veja Como conectar o Google se for seu primeiro acesso.",
                        "Select your connection file and sign in to Google in your browser. Open How to connect Google if this is your first connection.")))
                    throw new InvalidOperationException("Published Google login still asks the end user to prepare credentials.");
                Capture("diagnostics/cloud-storage-google-login.png");
            } else {
                help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                if (!AppearanceTree(body).OfType<StackPanel>().Any(x => x.Visibility == Visibility.Visible &&
                    x.Children.OfType<TextBlock>().Any(label => label.Text == T("Primeiro acesso ao Google Drive", "First Google Drive connection"))))
                    throw new InvalidOperationException("Google connection help did not expand inside the app.");
                Capture("diagnostics/cloud-storage-connection-help.png");
                help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }
            folders.SelectedIndex = 0;
            var view = AppearanceTree(body).OfType<Button>().Single(x => Convert.ToString(x.Content) == T("Ver pasta selecionada", "View selected folder"));
            view.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            if (folders.Items.Count != 1 || files.Items.Count != 2)
                throw new InvalidOperationException("Cloud storage preview did not navigate into the selected folder.");
            var goBack = AppearanceTree(body).OfType<Button>().Single(x => Convert.ToString(x.Content) == T("Subir uma pasta", "Go to parent folder"));
            goBack.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            if (files.Items.Count != 5) throw new InvalidOperationException("Cloud storage preview did not return to the root.");
            ShowPage("dashboard");
            ShowPage("cloud-storage");
            if (!AppearanceTree(body).Contains(files))
                throw new InvalidOperationException("Cloud storage ranking was discarded during navigation.");
            body.Children.Clear();
            CloudStoragePage(new CloudStorageReport { Quota = CloudStorageChecks.Sample().Quota, QuotaOnly = true });
            var inventory = AppearanceTree(body).OfType<StackPanel>().Single(x => x.Children.OfType<TextBlock>().Any(label =>
                label.Text == T("Arquivos e pastas por tamanho", "Files and folders by size")));
            if (inventory.Visibility != Visibility.Collapsed || !AppearanceTree(body).OfType<TextBlock>().Any(x =>
                x.Text == T("Use Analisar espaço para listar os arquivos e pastas do Drive.", "Use Analyze storage to list Drive files and folders.")))
                throw new InvalidOperationException("A quota-only refresh must not claim an empty file inventory.");
            Capture("diagnostics/cloud-services-quota-only.png");
            body.Children.Clear();
            CloudStoragePage(new CloudStorageReport { Quota = CloudQuota.Parse("{\"used\":10,\"other\":20}"), QuotaOnly = true });
            if (AppearanceTree(body).OfType<Border>().Any(x => Convert.ToString(x.Tag).StartsWith("OuO.CloudService:")))
                throw new InvalidOperationException("An unavailable service must not be displayed with an invented zero.");
            Capture("diagnostics/cloud-services-unavailable.png");
        }
    }
}
