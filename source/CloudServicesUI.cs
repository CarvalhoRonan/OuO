using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Aurora {
    public partial class MainWindow {
        static int CloudTileColumns(int count, double width) {
            int maximum = Math.Max(1, Math.Min(count, width >= 900 ? 4 : width >= 500 ? 2 : 1));
            return Math.Max(1, (int)Math.Ceiling(count / Math.Ceiling((double)Math.Max(1, count) / maximum)));
        }

        string CloudServiceName(string id) {
            switch (id) {
                case "DRIVE": return "Google Drive";
                case "GMAIL": return "Gmail";
                case "PHOTOS": return T("Google Fotos", "Google Photos");
                default: return id.Replace('_', ' ');
            }
        }

        void RenderCloudServices(CloudQuota quota, UniformGrid rows, TextBlock note) {
            rows.Children.Clear();
            note.Foreground = Muted;
            if (quota == null || quota.Services.Count == 0) {
                note.Text = T("O Google não disponibilizou a divisão por serviço nesta consulta. Tente Atualizar uso por serviço. O total da conta continua separado da lista de arquivos.",
                    "Google did not provide a service breakdown for this request. Try Refresh service usage. Account usage remains separate from the file list.");
                return;
            }
            foreach (var service in quota.Services.OrderByDescending(x => x.Bytes).ThenBy(x => x.Id, StringComparer.Ordinal))
                rows.Children.Add(CloudServiceTile(CloudServiceName(service.Id), service.Bytes, quota.GoogleAccountUsed, service.Id));
            long? rest = quota.Unattributed;
            if (rest > 0)
                rows.Children.Add(CloudServiceTile(T("Não detalhado pelo Google", "Not itemized by Google"), rest.Value, quota.GoogleAccountUsed, "unattributed"));
            rows.Columns = CloudTileColumns(rows.Children.Count, rows.ActualWidth);
            note.Text = T("Totais informados pelo Google. Fotos e Gmail aparecem por serviço; a lista de arquivos abaixo é do Drive.",
                "Totals reported by Google. Photos and Gmail are shown by service; the file list below covers Drive.");
            if (rest > 0)
                note.Text += " " + T("O saldo não detalhado não recebeu um serviço identificado pelo Google.",
                    "Google did not identify a service for the unitemized remainder.");
            if (quota.ServiceRowsInvalid)
                note.Text += " " + T("A divisão está parcial porque alguns valores não puderam ser confirmados.",
                    "This breakdown is partial because some values could not be confirmed.");
            if (quota.ServicesInconsistent)
                note.Text += " " + T("Os serviços informados não coincidem com o total da conta. Atualize a consulta; nenhum saldo foi atribuído automaticamente.",
                    "The reported services do not match the account total. Refresh the query; no remainder was assigned automatically.");
        }

        Border CloudServiceTile(string name, long bytes, long? total, string id) {
            var contents = new StackPanel();
            var title = Text(name, 14, Ink);
            if (id != "DRIVE" && id != "GMAIL" && id != "PHOTOS" && id != "unattributed") title.Tag = "OuO.RawText";
            contents.Children.Add(title);
            var amount = Text(CloudSize(bytes), 22, Ink);
            amount.Tag = "OuO.RawText";
            contents.Children.Add(amount);
            if (total > 0)
                contents.Children.Add(new ProgressBar {
                    Height = 6, Maximum = 100, Value = Math.Min(100, 100.0 * bytes / total.Value),
                    Foreground = Accent, Background = Surface, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 6)
                });
            var tile = new Border {
                Background = Bg, Padding = new Thickness(12), CornerRadius = new CornerRadius(8),
                Margin = new Thickness(4, 2, 4, 10), Child = contents, Tag = "OuO.CloudService:" + id,
                ToolTip = bytes.ToString("N0") + " bytes"
            };
            System.Windows.Automation.AutomationProperties.SetName(tile, name + ": " + CloudSize(bytes));
            return tile;
        }
    }
}
