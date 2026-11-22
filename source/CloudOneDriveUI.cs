using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Aurora {
    public partial class MainWindow {
        void RenderOneDriveSpace(CloudStorageReport report, UniformGrid rows, TextBlock note) {
            rows.Children.Clear();
            note.Foreground = Muted;
            long? used = report.Quota == null ? null : report.Quota.Used;
            bool inventory = !report.QuotaOnly && (!report.ListingFailed || report.Count > 0);
            if (inventory) {
                foreach (var kind in new[] { CloudFileKind.Photo, CloudFileKind.Video, CloudFileKind.Other }) {
                    var total = report.MediaTotal(kind);
                    string name = kind == CloudFileKind.Photo ? T("Fotos e imagens", "Photos and images") :
                        kind == CloudFileKind.Video ? T("Vídeos", "Videos") : T("Outros arquivos", "Other files");
                    var tile = CloudServiceTile(name, total.KnownBytes, used, kind.ToString());
                    tile.Tag = "OuO.OneDriveSpace:" + kind;
                    var contents = (StackPanel)tile.Child;
                    var amount = contents.Children.OfType<TextBlock>().Skip(1).First();
                    if (total.Files > 0 && total.UnknownSizes == total.Files) {
                        amount.Text = T("Não informado", "Not reported");
                        foreach (var bar in contents.Children.OfType<ProgressBar>().ToArray()) contents.Children.Remove(bar);
                    } else if (total.UnknownSizes > 0) amount.Text += "+";
                    contents.Children.Add(Text(total.Files.ToString("N0") + T(" arquivo(s)", " file(s)") +
                        (total.UnknownSizes > 0 ? " • " + total.UnknownSizes.ToString("N0") + T(" sem tamanho", " without a size") : ""), 12, Muted));
                    rows.Children.Add(tile);
                }
            }
            if (report.Quota != null && report.Quota.Trashed.HasValue) {
                var tile = CloudServiceTile(T("Lixeira", "Recycle bin"), report.Quota.Trashed.Value, used, "trash");
                tile.Tag = "OuO.OneDriveSpace:trash";
                rows.Children.Add(tile);
            }
            var summary = OneDriveSpaceSummary.From(report);
            if (inventory && summary.Remaining > 0) {
                var tile = CloudServiceTile(T("Saldo não explicado", "Unexplained balance"), summary.Remaining.Value, used, "unexplained");
                tile.Tag = "OuO.OneDriveSpace:unexplained";
                rows.Children.Add(tile);
            }
            rows.Columns = CloudTileColumns(rows.Children.Count, rows.ActualWidth);
            note.Text = inventory ? T("Categorias calculadas a partir dos arquivos listados, com lixeira informada pela Microsoft. A cota pessoal pode incluir outros serviços Microsoft.",
                "Categories calculated from listed files, with recycle-bin usage reported by Microsoft. Personal quota can include other Microsoft services.") :
                T("A cota foi consultada. Use Analisar espaço para listar arquivos, separar fotos e vídeos e comparar os tamanhos.",
                    "Quota was checked. Use Analyze storage to list files, separate photos and videos and compare sizes.");
            if (inventory && summary.Remaining > 0)
                note.Text += " " + T("O saldo pode incluir itens não listados e outros dados. Checar histórico do arquivo permite investigar versões, sem atribuir esse saldo automaticamente a elas.",
                    "The balance can include unlisted items and other data. Check file history can inspect versions without automatically attributing this balance to them.");
            if (inventory && summary.Incomplete)
                note.Text += " " + T("A comparação está parcial: há itens indisponíveis ou sem tamanho informado.",
                    "This comparison is partial: some items are unavailable or have no reported size.");
            if (summary.TotalsConflict)
                note.Text += " " + T("Os tamanhos listados e a cota não coincidem. Nenhum saldo negativo foi transformado em espaço disponível.",
                    "Listed sizes do not match the quota. A negative balance was not treated as available space.");
        }

        void RenderOneDriveHistory(OneDriveHistoryReport report, TextBlock file, TextBlock note, ListBox list) {
            file.Text = report.FilePath;
            note.Text = T("Versões retornadas pela Microsoft, da mais recente para a mais antiga. Os tamanhos são de cada versão e não medem, por si, o espaço adicional cobrado na conta.",
                "Versions returned by Microsoft, newest first. Per-version sizes do not, by themselves, measure the additional space charged to the account.");
            if (report.Partial) note.Text += " " + T("Histórico parcial; podem existir outras versões.", "Partial history; more versions may exist.");
            list.Items.Clear();
            foreach (var version in report.Versions) {
                string label = T("Versão ", "Version ") + version.Id;
                string details = (version.Bytes.HasValue ? CloudSize(version.Bytes.Value) : T("Tamanho indisponível", "Size unavailable")) + " • " +
                    (version.Modified.HasValue ? version.Modified.Value.LocalDateTime.ToString("g") : T("Data indisponível", "Date unavailable"));
                list.Items.Add(CloudRankRow(label, details, version));
            }
            if (list.Items.Count == 0) list.Items.Add(new ListBoxItem { Content = T("A Microsoft não retornou versões para este arquivo.", "Microsoft returned no versions for this file."), IsEnabled = false });
        }
    }
}
