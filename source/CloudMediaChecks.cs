using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Aurora {
    internal static class CloudMediaChecks {
        internal static CloudStorageReport Sample() {
            return CloudStorageReport.ParseListing(new StringReader(
                "Fotos/Grande.bin,700,TESTDRIVE#PHOTO1,image/jpeg\n" +
                "Fotos/Menor.HEIC,400,TESTDRIVE#PHOTO2,application/octet-stream\n" +
                "Vídeos/clip.MOV,900,TESTDRIVE#VIDEO1,video/quicktime\n" +
                "Documentos/falso.jpg,1000,TESTDRIVE#PDF1,application/pdf\n" +
                "Vídeos/sem-tamanho.mp4,-1,TESTDRIVE#VIDEO2,video/mp4\n" +
                "Fotos/duplicado.bin,700,TESTDRIVE#PHOTO1,image/jpeg"), CancellationToken.None);
        }

        internal static void Run(Action<bool, string> check) {
            var report = Sample();
            var photos = report.MediaTotal(CloudFileKind.Photo);
            var videos = report.MediaTotal(CloudFileKind.Video);
            check(report.Count == 5 && photos.Files == 2 && photos.KnownBytes == 1100 && videos.Files == 2 &&
                videos.KnownBytes == 900 && videos.UnknownSizes == 1 && report.MediaTotal(CloudFileKind.Other).KnownBytes == 1000,
                "cloud media categories prefer provider MIME types, fall back to extensions and do not double-count repeated IDs");
            var ranked = report.LargestFiles("", "", CloudFileKind.Photo).ToArray();
            check(ranked.Length == 2 && ranked[0].Bytes == 700 && ranked[1].Bytes == 400 &&
                report.LargestFiles("Fotos", "heic", CloudFileKind.Photo).Single().Bytes == 400 &&
                report.LargestFolders("", "", CloudFileKind.Photo).Single().KnownBytes == 1100,
                "cloud photo ranking and folder sizes use the same category and preserve size order through search and navigation");
            check(CloudMedia.Kind("anexo.jpg", "application/pdf") == CloudFileKind.Other &&
                CloudMedia.Kind("semextensao", "VIDEO/MP4") == CloudFileKind.Video &&
                CloudMedia.Kind("foto.CR3", null) == CloudFileKind.Photo && CloudMedia.Kind("pasta.jpg/arquivo", null) == CloudFileKind.Other,
                "cloud type classification handles renamed documents, extensionless media and camera formats without treating a folder extension as a file type");
            check(CloudMedia.Kind("projeto/index.d.ts", "application/octet-stream") == CloudFileKind.Other &&
                CloudMedia.Kind("projeto/index.ts", "application/octet-stream") == CloudFileKind.Other &&
                CloudMedia.Kind("projeto/index.d.mts", "video/mp2t") == CloudFileKind.Other &&
                CloudMedia.Kind("node_modules/pacote/index.ts", "video/mp2t") == CloudFileKind.Other &&
                CloudMedia.Kind("gravação.ts", "video/mp2t") == CloudFileKind.Video &&
                CloudMedia.Kind("gravação.MTS", "video/mp2t") == CloudFileKind.Video,
                "cloud media counts do not mistake TypeScript code for videos; ambiguous extensions require a provider media type");
            report.Quota = CloudQuota.Parse("{\"used\":4500,\"trashed\":200}");
            var difference = OneDriveSpaceSummary.From(report);
            check(difference.Remaining == 1300 && difference.Incomplete && !difference.TotalsConflict &&
                report.KnownBytes == 3000, "OneDrive reconciliation keeps unknown file sizes separate and subtracts the reported recycle bin only once");
            report.Quota = CloudQuota.Parse("{\"used\":1,\"trashed\":9223372036854775807}");
            check(OneDriveSpaceSummary.From(report).TotalsConflict && !OneDriveSpaceSummary.From(report).Remaining.HasValue,
                "OneDrive mismatched or oversized totals cannot create a negative remainder or imply free storage");
            report.QuotaOnly = true;
            check(!OneDriveSpaceSummary.From(report).Remaining.HasValue,
                "OneDrive quota-only queries cannot pretend an empty inventory explains the account balance");
            string route = OneDriveHistoryReport.VersionPath("ROOT", "DRIVE#ITEM!1");
            int calls = 0;
            var history = OneDriveHistoryReport.Read(new CloudStorageFile { Path = "owned.txt", Id = "DRIVE#ITEM!1" }, "ROOT", path => {
                calls++;
                return calls == 1 ? "{\"value\":[{\"id\":\"2.0\",\"size\":42,\"lastModifiedDateTime\":\"2026-01-02T03:04:05Z\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0" + route + "?$skiptoken=owned\"}" :
                    "{\"value\":[{\"id\":\"2.0\",\"size\":42},{\"id\":\"1.0\"}]}";
            }, CancellationToken.None, page => {});
            check(calls == 2 && history.Versions.Count == 2 && history.Versions[0].Bytes == 42 &&
                history.Versions[0].Modified.HasValue && !history.Versions[1].Bytes.HasValue && !history.Partial,
                "OneDrive version pages retain provider order and distinguish an unknown version size from zero without counting duplicate versions twice");
            bool rejected = false;
            calls = 0;
            try { OneDriveHistoryReport.Read(new CloudStorageFile { Path = "owned.txt", Id = "ITEM" }, "ROOT", path => {
                calls++; return "{\"value\":[],\"@odata.nextLink\":\"https://untrusted.invalid/versions\"}";
            }, CancellationToken.None, page => {}); } catch (InvalidDataException) { rejected = true; }
            check(rejected && calls == 1, "OneDrive pagination cannot forward an account authorization to a different host");
            rejected = false;
            try { OneDriveHistoryReport.NextPage("https://graph.microsoft.com/v1.0/drives/OTHER/items/ITEM/versions", "/drives/ROOT/items/ITEM/versions"); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, "OneDrive pagination stays on the selected file history even when a response names another file");
            rejected = false;
            try { OneDriveHistoryReport.VersionPath("ROOT", ".."); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "OneDrive history identifiers cannot be normalized into a parent resource path");
            using (var cancellation = new CancellationTokenSource()) {
                cancellation.Cancel(); calls = 0;
                bool canceled = false;
                try { OneDriveHistoryReport.Read(new CloudStorageFile { Id = "ITEM", Path = "owned.txt" }, "ROOT", path => { calls++; return "{}"; }, cancellation.Token, page => {}); }
                catch (OperationCanceledException) { canceled = true; }
                check(canceled && calls == 0, "OneDrive history cancellation prevents the next request before account data is queried");
            }
            calls = 0;
            var loop = OneDriveHistoryReport.Read(new CloudStorageFile { Id = "ITEM", Path = "owned.txt" }, "ROOT", path => {
                calls++; return "{\"value\":[],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/drives/ROOT/items/ITEM/versions?$skiptoken=repeat\"}";
            }, CancellationToken.None, page => {});
            check(loop.Partial && loop.LimitReached && calls == 2, "OneDrive repeated continuation cannot loop indefinitely");
        }
    }

    public partial class MainWindow {
        void VerifyOneDriveCloudUI() {
            ShowPage("cloud-storage", true);
            body.Children.Clear();
            var report = CloudMediaChecks.Sample();
            report.Quota = CloudQuota.Parse("{\"total\":5000,\"used\":4500,\"free\":500,\"trashed\":200}");
            var history = new OneDriveHistoryReport { FilePath = "Fotos/Grande.bin" };
            history.Versions.Add(new CloudFileVersion { Id = "2.0", Bytes = 700, Modified = DateTimeOffset.Parse("2026-01-02T03:04:05Z") });
            history.Versions.Add(new CloudFileVersion { Id = "1.0", Bytes = 650 });
            CloudStoragePage(report, CloudProvider.OneDrive, history);
            var files = AppearanceTree(body).OfType<ListBox>().Single(x => AutomationProperties.GetName(x) == T("Arquivos", "Files"));
            var kind = AppearanceTree(body).OfType<ComboBox>().Single(x => AutomationProperties.GetName(x) == T("Tipo de arquivo da lista", "File type in this list"));
            kind.SelectedIndex = 1;
            if (files.Items.Count != 2 || ((CloudStorageFile)((ListBoxItem)files.Items[0]).Tag).Bytes != 700 ||
                !AppearanceTree(body).OfType<Border>().Any(x => Convert.ToString(x.Tag) == "OuO.OneDriveSpace:unexplained"))
                throw new InvalidOperationException("OneDrive photo ranking or space reconciliation is missing.");
            Capture("diagnostics/onedrive-photos-pt.png");
            kind.SelectedIndex = 2;
            if (files.Items.Count != 2 || ((CloudStorageFile)((ListBoxItem)files.Items[0]).Tag).Bytes != 900)
                throw new InvalidOperationException("OneDrive video filtering lost size order.");
            var historyList = AppearanceTree(body).OfType<ListBox>().Single(x => AutomationProperties.GetName(x) == T("Versões do arquivo", "File versions"));
            if (historyList.Items.Count != 2) throw new InvalidOperationException("OneDrive returned version metadata is missing from its report.");
            pageScroll.ScrollToBottom();Capture("diagnostics/onedrive-history-pt.png");pageScroll.ScrollToTop();
            body.Children.Clear();
            var many = CloudStorageReport.ParseListing(new StringReader(String.Join("\n", Enumerable.Range(0,205).Select(x =>
                "Fotos/foto-" + x + ".jpg," + (300-x) + ",ITEM" + x + ",image/jpeg"))), CancellationToken.None);
            CloudStoragePage(many, CloudProvider.OneDrive);
            files = AppearanceTree(body).OfType<ListBox>().Single(x => AutomationProperties.GetName(x) == T("Arquivos", "Files"));
            var next = AppearanceTree(body).OfType<Button>().Single(x => Convert.ToString(x.Content) == T("Próxima página", "Next page"));
            if (files.Items.Count != 100 || !next.IsEnabled) throw new InvalidOperationException("OneDrive large media list was silently limited to 100 files.");
            next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            if (((CloudStorageFile)((ListBoxItem)files.Items[0]).Tag).Bytes != 200) throw new InvalidOperationException("OneDrive second page lost the largest-first ranking.");
            next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            if (files.Items.Count != 5 || next.IsEnabled) throw new InvalidOperationException("OneDrive final media page is incomplete or loops beyond the results.");
            Capture("diagnostics/onedrive-paging-pt.png");
        }
    }
}
