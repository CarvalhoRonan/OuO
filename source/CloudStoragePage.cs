using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Aurora {
public partial class MainWindow {
    const int CloudRankLimit = 100;

    void CloudStoragePage(CloudStorageReport sample = null, CloudProvider sampleProvider = CloudProvider.GoogleDrive, OneDriveHistoryReport sampleHistory = null) {
        Heading(T("Armazenamento em nuvem", "Cloud storage"),
            T("Encontre os arquivos e pastas que mais ocupam espaço no Google Drive e no OneDrive.", "Find the files and folders using the most space in Google Drive and OneDrive."));

        var reports = new CloudStorageReport[4];
        int filePage = 0;
        OneDriveHistoryReport fileHistory = preview ? sampleHistory : null;
        if (preview) reports[(int)sampleProvider * 2] = sample;
        var locations = new string[2];
        if (!preview) locations[1] = CloudFiles.Roots().FirstOrDefault();
        bool local = false;
        string scope = "";
        CancellationTokenSource operation = null;
        var state = Text("", 13, Muted);
        Action refresh = null;
        Action<bool> changeMode = value => { local = value; scope = ""; filePage = 0; state.Text = ""; refresh(); };
        var accountTab = TabButton(T("Conta na nuvem", "Cloud account"), () => changeMode(false), true);
        var localTab = TabButton(T("Pasta sincronizada", "Synced folder"), () => changeMode(true));
        Row(body, accountTab, localTab);

        BeginColumns(2);
        var control = Card(T("Analisar Google Drive e OneDrive", "Analyze Google Drive and OneDrive"), "");
        var provider = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
        provider.Items.Add("Google Drive");
        provider.Items.Add("OneDrive");
        provider.SelectedIndex = (int)sampleProvider;
        System.Windows.Automation.AutomationProperties.SetName(provider, T("Serviço de armazenamento", "Storage provider"));
        control.Children.Add(provider);
        Func<CloudProvider> selection = () => provider.SelectedIndex == 1 ? CloudProvider.OneDrive : CloudProvider.GoogleDrive;
        Func<int> slot = () => provider.SelectedIndex * 2 + (local ? 1 : 0);
        Func<CloudStorageReport> selectedReport = () => reports[slot()];

        var context = Text("", 13, Muted);
        control.Children.Add(context);
        var currentFolder = Input("");
        currentFolder.IsReadOnly = true;
        control.Children.Add(currentFolder);
        var progress = new ProgressBar {
            Height = 6, IsIndeterminate = true, Foreground = Accent, Background = Bg,
            BorderThickness = new Thickness(0), Margin = new Thickness(0, 2, 0, 10), Visibility = Visibility.Collapsed
        };
        Action analyzeAction = null;

        Action<string, Func<CancellationToken, string>, Action<string>> run = (title, work, finished) => {
            if (preview || operation != null) return;
            operation = new CancellationTokenSource();
            var token = operation.Token;
            state.Text = title + "…";
            state.Foreground = Accent;
            refresh();
            Work(title, () => work(token), result => {
                operation.Dispose();
                operation = null;
                finished(result);
                state.Text = result;
                state.Foreground = OutcomeBrush(result);
                refresh();
            });
        };
        int cloudJobId = 0;
        Action<string> stage = message => {
            if (workerJobId != 0) cloudJobId = workerJobId;
            ReportStage(message, cloudJobId);
            Dispatcher.BeginInvoke(new Action(() => {
                if (operation != null) state.Text = UiLanguage.Current(message);
            }));
        };

        var connect = Button(T("Conectar conta", "Connect account"), () => {
            CloudProvider service = selection();
            bool chooseFile = service == CloudProvider.GoogleDrive && !CloudConnection.HasPublishedGoogleClient;
            string credentials = chooseFile ? PickFile(T("Arquivo de conexão do Google (*.json)|*.json", "Google connection file (*.json)|*.json")) : null;
            if (chooseFile && credentials == null) return;
            int index = slot();
            bool connected = false;
            run(T("Conectar armazenamento em nuvem", "Connect cloud storage"), token => {
                CloudConnection.Connect(service, credentials, token, stage);
                connected = true;
                return T("OK • Conta conectada. Iniciando análise…", "OK • Account connected. Starting analysis…");
            }, result => {
                if (connected) {
                    reports[index] = null;
                    if (service == CloudProvider.OneDrive) fileHistory = null;
                    scope = "";
                    Dispatcher.BeginInvoke(new Action(() => analyzeAction()));
                }
            });
        });
        var googleHelp = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 8) };
        googleHelp.Children.Add(Text(T("Primeiro acesso ao Google Drive", "First Google Drive connection"), 15, Ink));
        googleHelp.Children.Add(Text(T(
            "O Google pede um arquivo de conexão criado no Google Cloud. Essa preparação é feita uma vez para o seu aplicativo.",
            "Google requires a connection file created in Google Cloud. Prepare it once for your application."), 13, Muted));
        googleHelp.Children.Add(Text(T(
            "1. Ative a API do Google Drive no Google Cloud.\n2. Configure o consentimento e crie um cliente do tipo App para computador. Em um projeto em teste, adicione sua conta aos usuários de teste.\n3. Baixe o arquivo JSON. Clique em Conectar Google Drive no OuO e selecione esse arquivo.",
            "1. Enable the Google Drive API in Google Cloud.\n2. Configure consent and create a Desktop app client. If the project is in testing, add your account as a test user.\n3. Download the JSON file. Click Connect Google Drive in OuO and select that file."), 13, Ink));
        Row(googleHelp,
            Button(T("Abrir Google Cloud", "Open Google Cloud"), () => Open("https://console.cloud.google.com/apis/dashboard")),
            Button(T("Ver guia completo", "Open full guide"), () => Open("https://rclone.org/drive/#making-your-own-client-id")));
        var guide = Button(T("Como conectar o Google", "How to connect Google"), () =>
            googleHelp.Visibility = googleHelp.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible);
        var disconnect = Button(T("Desconectar neste PC", "Disconnect on this PC"), () => {
            if (preview) return;
            if (!Confirm(T("Remover a autorização salva pelo OuO neste computador?", "Remove the authorization saved by OuO on this computer?"))) return;
            try {
                CloudConnection.Disconnect(selection());
                reports[slot()] = null;
                if (selection() == CloudProvider.OneDrive) fileHistory = null;
                scope = "";
                state.Text = T("OK • Autorização local removida.", "OK • Local authorization removed.");
                state.Foreground = StateGreen;
                refresh();
            } catch (IOException error) { ShowNotice(error.Message); }
            catch (UnauthorizedAccessException) { ShowNotice(T("Não foi possível remover a autorização local.", "Could not remove local authorization.")); }
        });
        var browse = Button(T("Escolher pasta", "Choose folder"), () => {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog {
                Description = T("Escolha a pasta montada ou sincronizada pelo serviço.", "Choose the folder mounted or synced by the service."),
                ShowNewFolderButton = false, SelectedPath = locations[provider.SelectedIndex] ?? ""
            }) {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                locations[provider.SelectedIndex] = dialog.SelectedPath;
                reports[slot()] = null;
                scope = "";
                state.Text = "";
                refresh();
            }
        });
        analyzeAction = () => {
            int index = slot();
            bool scanLocal = local;
            CloudProvider service = selection();
            string path = locations[provider.SelectedIndex];
            CloudStorageReport report = null;
            run(T("Analisar espaço em nuvem", "Analyze cloud storage"), token => {
                report = scanLocal ? CloudStorageReport.ScanLocal(path, service == CloudProvider.OneDrive, token) : CloudConnection.Scan(service, token, stage);
                if (report.ListingFailed && report.Count == 0)
                    return T("ERROR • Não foi possível listar os arquivos. Confira a internet e, se necessário, reconecte a conta.",
                        "ERROR • Could not list files. Check your connection and reconnect if needed.");
                if (report.Partial)
                    return T("AVISO • Análise parcial. Os resultados cobrem apenas os itens consultados.", "WARNING • Partial analysis. Results cover only the items that were read.");
                if (!scanLocal && service == CloudProvider.GoogleDrive && (report.Quota == null || report.Quota.Services.Count == 0))
                    return T("AVISO • Arquivos analisados. A divisão por serviço está indisponível no momento.", "WARNING • Files analyzed. The service breakdown is currently unavailable.");
                if (!scanLocal && service == CloudProvider.GoogleDrive && (report.Quota.ServiceRowsInvalid || report.Quota.ServicesInconsistent))
                    return T("AVISO • Arquivos analisados. Confira as observações da divisão por serviço.", "WARNING • Files analyzed. Check the service breakdown notes.");
                return T("OK • Análise concluída.", "OK • Analysis complete.");
            }, result => { if (report != null) { reports[index] = report; scope = ""; filePage = 0; if (service == CloudProvider.OneDrive) fileHistory = null; } });
        };
        var analyze = Button(T("Analisar espaço", "Analyze storage"), () => analyzeAction());
        var cancel = Button(T("Cancelar", "Cancel"), () => { if (operation != null) operation.Cancel(); });
        var quotaLink = Button(T("Abrir armazenamento no navegador", "Open storage in browser"), () =>
            Open(selection() == CloudProvider.GoogleDrive ? "https://drive.google.com/drive/quota" : "https://onedrive.live.com/?v=managestorage"));
        var accountLink = Button(T("Atualizar uso por serviço", "Refresh service usage"), () => {
            int index = slot();
            CloudProvider service = selection();
            CloudQuota updated = null;
            run(T("Atualizar uso por serviço", "Refresh service usage"), token => {
                updated = CloudConnection.ReadAccountQuota(service, token, stage);
                if (service == CloudProvider.OneDrive)
                    return updated == null ? T("AVISO • A Microsoft não disponibilizou a cota nesta consulta.", "WARNING • Microsoft did not provide quota for this query.") :
                        T("OK • Cota e lixeira do OneDrive atualizadas.", "OK • OneDrive quota and recycle bin refreshed.");
                if (updated != null && (updated.ServiceRowsInvalid || updated.ServicesInconsistent))
                    return T("AVISO • A divisão por serviço está parcial. Confira as observações abaixo.", "WARNING • The service breakdown is partial. Check the notes below.");
                return updated != null && updated.Services.Count > 0 ?
                    T("OK • Uso por serviço atualizado.", "OK • Service usage updated.") :
                    T("AVISO • O Google não disponibilizou a divisão por serviço. O total da conta foi consultado quando disponível.",
                        "WARNING • Google did not provide the service breakdown. The account total was checked where available.");
            }, result => {
                if (updated == null) return;
                if (reports[index] == null) reports[index] = new CloudStorageReport { QuotaOnly = true };
                reports[index].Quota = updated;
            });
        });
        Row(control, connect, guide, disconnect, browse, analyze, cancel);
        Row(control, quotaLink, accountLink);
        control.Children.Add(googleHelp);
        control.Children.Add(progress);
        control.Children.Add(state);

        var summary = Card(T("Uso do espaço", "Storage usage"), "");
        var accountUsage = Text("", 18, Ink);
        var usageBar = new ProgressBar { Height = 7, Maximum = 100, Foreground = Accent, Background = Bg, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 10) };
        var accountDetails = Text("", 13, Muted);
        var totals = Text("", 14, Ink);
        summary.Children.Add(accountUsage);
        summary.Children.Add(usageBar);
        summary.Children.Add(accountDetails);
        summary.Children.Add(totals);

        cardHost = null;
        var serviceCard = Card(T("De onde vem o uso da conta", "Where account storage is used"), "");
        var serviceRows = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Margin = new Thickness(-4, 0, -4, 0) };
        serviceRows.SizeChanged += (s, e) => {
            serviceRows.Columns = CloudTileColumns(serviceRows.Children.Count, serviceRows.ActualWidth);
        };
        var serviceNote = Text("", 13, Muted);
        serviceCard.Children.Add(serviceRows);
        serviceCard.Children.Add(serviceNote);
        var results = Card(T("Arquivos e pastas por tamanho", "Files and folders by size"), "");
        var location = Text("", 13, Muted);
        results.Children.Add(location);
        results.Children.Add(Text(T("Tipo de arquivo da lista", "File type in this list"), 12, Muted));
        var fileKind = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
        foreach (string name in new[] { T("Todos os tipos", "All types"), T("Fotos e imagens", "Photos and images"), T("Vídeos", "Videos"), T("Outros arquivos", "Other files") }) fileKind.Items.Add(name);
        fileKind.SelectedIndex = 0;
        System.Windows.Automation.AutomationProperties.SetName(fileKind, T("Tipo de arquivo da lista", "File type in this list"));
        fileKind.ToolTip = T("Usa o tipo informado pelo serviço e, quando necessário, a extensão do arquivo. Extensões ambíguas, como .ts, precisam de um tipo confirmado pelo serviço. A lista do Google Drive não inclui a biblioteca do Google Fotos.",
            "Uses the type reported by the service, falling back to the file extension. Ambiguous extensions such as .ts need a media type reported by the service. The Google Drive list does not include the Google Photos library.");
        results.Children.Add(fileKind);
        var search = Field(results, T("Filtrar pelo nome ou caminho", "Filter by name or path"), "");
        System.Windows.Automation.AutomationProperties.SetName(search, T("Filtrar pelo nome ou caminho", "Filter by name or path"));
        var back = Button(T("Subir uma pasta", "Go to parent folder"), () => { scope = CloudStorageReport.Parent(scope); filePage = 0; refresh(); });
        var root = Button(T("Ver todas as pastas", "Go to root"), () => { scope = ""; filePage = 0; search.Text = ""; refresh(); });
        Row(results, back, root);
        var columns = new CardColumns { MaxColumns = 2 };
        results.Children.Add(columns);
        var folders = CloudRankList(columns, T("Pastas", "Folders"));
        var files = CloudRankList(columns, T("Arquivos", "Files"));
        Action openFolder = () => {
            var item = folders.SelectedItem as ListBoxItem;
            if (item == null) return;
            scope = ((CloudStorageFolder)item.Tag).Path;
            filePage = 0;
            search.Text = "";
            refresh();
        };
        folders.MouseDoubleClick += (s, e) => openFolder();
        folders.KeyDown += (s, e) => { if (e.Key == Key.Enter) { openFolder(); e.Handled = true; } };
        var enter = Button(T("Ver pasta selecionada", "View selected folder"), openFolder);
        var copy = Button(T("Copiar caminho", "Copy path"), () => {
            var item = files.SelectedItem as ListBoxItem;
            var folder = folders.SelectedItem as ListBoxItem;
            if (item == null && folder == null) return;
            string path = item != null ? ((CloudStorageFile)item.Tag).Path : ((CloudStorageFolder)folder.Tag).Path;
            if (local) path = Path.Combine(selectedReport().LocalRoot, path.Replace('/', Path.DirectorySeparatorChar));
            try { Clipboard.SetText(path); ShowToast(T("OK • Caminho copiado.", "OK • Path copied."), true); }
            catch (System.Runtime.InteropServices.ExternalException) { ShowNotice(T("Não foi possível copiar o caminho.", "Could not copy the path.")); }
        });
        var openFile = Button(T("Abrir arquivo no Drive", "Open file in Drive"), () => {
            var item = files.SelectedItem as ListBoxItem;
            var file = item == null ? null : item.Tag as CloudStorageFile;
            if (!local && selection() == CloudProvider.GoogleDrive && file != null && CloudDriveId(file.Id))
                Open("https://drive.google.com/open?id=" + Uri.EscapeDataString(file.Id));
        });
        var limit = Text("", 12, Muted);
        results.Children.Add(limit);
        var previousFiles = Button(T("Página anterior", "Previous page"), () => { filePage = Math.Max(0, filePage - 1); refresh(); });
        var nextFiles = Button(T("Próxima página", "Next page"), () => { filePage++; refresh(); });
        Row(results, previousFiles, nextFiles);
        var versions = Button(T("Checar histórico do arquivo", "Check file history"), () => {
            var item = files.SelectedItem as ListBoxItem;
            var file = item == null ? null : item.Tag as CloudStorageFile;
            if (file == null || local || selection() != CloudProvider.OneDrive) return;
            OneDriveHistoryReport fetched = null;
            run(T("Checar histórico do OneDrive", "Check OneDrive history"), token => {
                fetched = CloudConnection.InspectOneDriveHistory(file, token, stage);
                return fetched.Partial ? T("AVISO • Histórico parcial. Confira os detalhes disponíveis.", "WARNING • Partial history. Check the available details.") :
                    T("OK • Histórico do arquivo consultado.", "OK • File history checked.");
            }, result => { if (fetched != null) fileHistory = fetched; });
        });
        Row(results, enter, copy, openFile, versions);

        var historyCard = Card(T("Histórico do arquivo no OneDrive", "OneDrive file history"), "");
        var historyFile = Text("", 14, Ink); historyFile.Tag = "OuO.RawText";
        var historyNote = Text("", 13, Muted);
        var historyList = new ListBox { Height = 230, Background = Bg, Foreground = Ink, BorderBrush = Muted };
        System.Windows.Automation.AutomationProperties.SetName(historyList, T("Versões do arquivo", "File versions"));
        historyCard.Children.Add(historyFile);
        historyCard.Children.Add(historyNote);
        historyCard.Children.Add(historyList);

        bool changingListSelection = false;
        Action selectActions = () => {
            enter.IsEnabled = folders.SelectedItem != null;
            copy.IsEnabled = folders.SelectedItem != null || files.SelectedItem != null;
            var row = files.SelectedItem as ListBoxItem;
            openFile.IsEnabled = row != null && CloudDriveId(((CloudStorageFile)row.Tag).Id);
            versions.IsEnabled = operation == null && row != null && row.Tag is CloudStorageFile && !String.IsNullOrEmpty(((CloudStorageFile)row.Tag).Id);
        };
        folders.SelectionChanged += (s, e) => {
            if (changingListSelection) return;
            if (folders.SelectedItem != null) { changingListSelection = true; files.SelectedIndex = -1; changingListSelection = false; }
            selectActions();
        };
        files.SelectionChanged += (s, e) => {
            if (changingListSelection) return;
            if (files.SelectedItem != null) { changingListSelection = true; folders.SelectedIndex = -1; changingListSelection = false; }
            selectActions();
        };

        refresh = () => {
            bool google = selection() == CloudProvider.GoogleDrive;
            bool googleSetup = google && !CloudConnection.HasPublishedGoogleClient;
            bool connected = !preview && CloudConnection.Ready(selection());
            bool idle = operation == null;
            SelectTab(accountTab, !local);
            SelectTab(localTab, local);
            provider.IsEnabled = accountTab.IsEnabled = localTab.IsEnabled = idle;
            connect.Content = connected ? T("Reconectar conta", "Reconnect account") :
                google ? T("Conectar Google Drive", "Connect Google Drive") : T("Conectar OneDrive", "Connect OneDrive");
            connect.Visibility = CloudVisible(!local);
            guide.Visibility = CloudVisible(!local && googleSetup);
            guide.IsEnabled = idle;
            if (local || !googleSetup) googleHelp.Visibility = Visibility.Collapsed;
            disconnect.Visibility = CloudVisible(!local && connected);
            browse.Visibility = currentFolder.Visibility = CloudVisible(local);
            cancel.IsEnabled = !idle;
            cancel.Visibility = CloudVisible(!idle);
            progress.Visibility = CloudVisible(!idle);
            state.Visibility = CloudVisible(!String.IsNullOrEmpty(state.Text));
            disconnect.IsEnabled = browse.IsEnabled = idle;
            connect.IsEnabled = idle && CloudConnection.OnlineSupported;
            analyze.IsEnabled = idle && (local ? !String.IsNullOrEmpty(locations[provider.SelectedIndex]) : connected && CloudConnection.OnlineSupported);
            accountLink.Content = google ? T("Atualizar uso por serviço", "Refresh service usage") : T("Atualizar cota e lixeira", "Refresh quota and recycle bin");
            accountLink.Visibility = CloudVisible(!local);
            accountLink.IsEnabled = idle && connected && CloudConnection.OnlineSupported;
            currentFolder.Text = locations[provider.SelectedIndex] ?? T("Nenhuma pasta selecionada.", "No folder selected.");
            context.Text = local ?
                T("Escolha a pasta sincronizada. A leitura usa os tamanhos informados e preserva os arquivos somente online.", "Choose the synced folder. The scan uses reported sizes and leaves online-only files as they are.") :
                connected ? T("Autorização de leitura salva neste computador.", "Read access saved on this computer.") :
                (google ?
                    googleSetup ?
                        T("Selecione seu arquivo de conexão e entre no Google pelo navegador. Veja Como conectar o Google se for seu primeiro acesso.", "Select your connection file and sign in to Google in your browser. Open How to connect Google if this is your first connection.") :
                        T("Clique em Conectar Google Drive, escolha sua conta no navegador e autorize a análise dos nomes e tamanhos dos arquivos.", "Click Connect Google Drive, choose your account in the browser and authorize analysis of file names and sizes.") :
                    T("Entre na sua conta Microsoft no navegador. O OuO identifica e confirma seu OneDrive principal automaticamente. A conexão pede acesso de leitura.", "Sign in to your Microsoft account in your browser. OuO identifies and checks your primary OneDrive automatically. The connection requests read access."));
            if (!local && !CloudConnection.OnlineSupported)
                context.Text = T("A análise online requer Windows 10 ou mais recente. Você pode usar a pasta sincronizada ou abrir o armazenamento no navegador.",
                    "Online analysis requires Windows 10 or newer. Use a synced folder or open storage in your browser.");
            else if (!local && !connected)
                context.Text += T(" Na primeira conexão, o OuO baixa o rclone oficial.", " On first connection, OuO downloads the official rclone tool.");

            var report = selectedReport();
            summary.Visibility = CloudVisible(report != null);
            serviceCard.Visibility = CloudVisible(!local && report != null);
            results.Visibility = CloudVisible(report != null && !report.QuotaOnly && (report.Count > 0 || !report.ListingFailed));
            openFile.Visibility = CloudVisible(!local && google);
            versions.Visibility = CloudVisible(!local && !google);
            historyCard.Visibility = CloudVisible(!local && !google && fileHistory != null);
            if (fileHistory != null) RenderOneDriveHistory(fileHistory, historyFile, historyNote, historyList);
            var priorRow = files.SelectedItem as ListBoxItem;
            var priorFile = priorRow == null ? null : priorRow.Tag as CloudStorageFile;
            folders.Items.Clear();
            files.Items.Clear();
            if (report == null) { selectActions(); return; }
            RenderCloudSummary(report, google, accountUsage, usageBar, accountDetails, totals);
            if (google) RenderCloudServices(report.Quota, serviceRows, serviceNote);
            else RenderOneDriveSpace(report, serviceRows, serviceNote);
            location.Text = scope.Length == 0 ? T("Todas as pastas analisadas", "All scanned folders") : scope.Replace("/", "  ›  ");
            location.Tag = scope.Length == 0 ? null : "OuO.RawText";
            back.IsEnabled = root.IsEnabled = scope.Length != 0;
            string query = search.Text.Trim();
            var kind = fileKind.SelectedIndex > 0 ? (CloudFileKind)fileKind.SelectedIndex : CloudFileKind.All;
            foreach (var item in report.LargestFolders(scope, query, kind).Take(CloudRankLimit)) {
                string amount = item.UnknownSizes == item.Files ? T("Tamanho indisponível", "Size unavailable") :
                    CloudSize(item.KnownBytes) + (item.UnknownSizes > 0 ? "+" : "");
                folders.Items.Add(CloudRankRow(item.Path, amount, item));
            }
            var filtered = report.LargestFiles(scope, query, kind).ToArray();
            filePage = Math.Min(filePage, Math.Max(0, (filtered.Length - 1) / CloudRankLimit));
            foreach (var item in filtered.Skip(filePage * CloudRankLimit).Take(CloudRankLimit)) {
                string amount = item.Bytes < 0 ? T("Tamanho indisponível", "Size unavailable") : CloudSize(item.Bytes);
                if (item.Availability == CloudAvailability.Online) amount += T(" · Só online", " · Online only");
                var row = CloudRankRow(item.Path, amount, item);
                files.Items.Add(row);
                if (priorFile != null && item.Path == priorFile.Path && item.Id == priorFile.Id) files.SelectedItem = row;
            }
            previousFiles.IsEnabled = filePage > 0;
            nextFiles.IsEnabled = (filePage + 1) * CloudRankLimit < filtered.Length;
            int first = filtered.Length == 0 ? 0 : filePage * CloudRankLimit + 1;
            int last = Math.Min((filePage + 1) * CloudRankLimit, filtered.Length);
            limit.Text = T("Arquivos ", "Files ") + first.ToString("N0") + "–" + last.ToString("N0") + T(" de ", " of ") + filtered.Length.ToString("N0") +
                T(" • maiores primeiro. Pastas: até 100 resultados por local.", " • largest first. Folders: up to 100 results per location.");
            if (folders.Items.Count == 0) folders.Items.Add(CloudEmptyRow());
            if (files.Items.Count == 0) files.Items.Add(CloudEmptyRow());
            selectActions();
        };
        provider.SelectionChanged += (s, e) => { scope = ""; filePage = 0; state.Text = ""; search.Text = ""; refresh(); };
        fileKind.SelectionChanged += (s, e) => { filePage = 0; refresh(); };
        search.TextChanged += (s, e) => { filePage = 0; refresh(); };
        refresh();
    }

    static Visibility CloudVisible(bool show) { return show ? Visibility.Visible : Visibility.Collapsed; }
    static bool CloudDriveId(string id) { return Regex.IsMatch(id ?? "", @"\A[A-Za-z0-9_-]{8,200}\z"); }

    static string CloudSize(long bytes) {
        string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        double scaled = bytes;
        int unit = 0;
        while (scaled >= 1000 && unit < units.Length - 1) { scaled /= 1000; unit++; }
        return scaled.ToString("0.##") + " " + units[unit];
    }

    ListBox CloudRankList(Panel parent, string title) {
        var panel = new StackPanel { MinWidth = 250 };
        panel.Children.Add(Text(title, 14, Ink));
        var list = new ListBox { Height = 272, Background = Bg, Foreground = Ink, BorderBrush = Muted, BorderThickness = new Thickness(1) };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        System.Windows.Automation.AutomationProperties.SetName(list, title);
        panel.Children.Add(list);
        parent.Children.Add(panel);
        return list;
    }

    ListBoxItem CloudRankRow(string path, string size, object data) {
        var panel = new StackPanel { Margin = new Thickness(5, 3, 5, 3) };
        var name = new TextBlock { Text = path, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Ink, Tag = "OuO.RawText" };
        panel.Children.Add(name);
        panel.Children.Add(new TextBlock { Text = size, Foreground = Muted, FontSize = 12 });
        return new ListBoxItem { Content = panel, Tag = data, ToolTip = path + "\n" + size };
    }

    ListBoxItem CloudEmptyRow() {
        return new ListBoxItem { Content = T("Nenhum resultado neste local.", "No results here."), IsEnabled = false };
    }

    void RenderCloudSummary(CloudStorageReport report, bool google, TextBlock usage, ProgressBar bar, TextBlock details, TextBlock totals) {
        var quota = report.Quota;
        bool local = report.LocalRoot != null;
        usage.Visibility = details.Visibility = CloudVisible(!local);
        bar.Visibility = Visibility.Collapsed;
        details.Text = "";
        if (!local && quota == null) usage.Text = T("Cota da conta indisponível", "Account quota unavailable");
        if (quota != null) {
            long? accountUsed = google ? quota.GoogleAccountUsed : quota.Used;
            usage.Text = accountUsed.HasValue ? CloudSize(accountUsed.Value) + T(" usados na conta", " used in the account") : T("Cota da conta", "Account quota");
            if (quota.Total.HasValue) usage.Text += "  /  " + CloudSize(quota.Total.Value);
            if (accountUsed.HasValue && quota.Total > 0) {
                bar.Value = Math.Min(100, 100.0 * accountUsed.Value / quota.Total.Value);
                bar.Visibility = Visibility.Visible;
            }
            var fields = new System.Collections.Generic.List<string>();
            if (google && quota.Used.HasValue) fields.Add(T("No Google Drive  ", "In Google Drive  ") + CloudSize(quota.Used.Value));
            if (quota.Free.HasValue) fields.Add(CloudSize(quota.Free.Value) + T(" livres", " free"));
            if (quota.Trashed.HasValue) fields.Add(T("Lixeira  ", "Trash  ") + CloudSize(quota.Trashed.Value));
            if (quota.Other.HasValue) fields.Add((google ? T("Fora do Google Drive  ", "Outside Google Drive  ") : T("Outros serviços  ", "Other services  ")) + CloudSize(quota.Other.Value));
            details.Text = String.Join("   ·   ", fields) + "\n" + (google ?
                T("Veja a divisão por serviço logo abaixo. A lista de arquivos corresponde ao Google Drive.", "See the service breakdown below. The file list covers Google Drive.") :
                T("A cota pessoal pode incluir outros serviços Microsoft. Versões de arquivos e lixeira podem afetar a diferença.", "Personal quota may include other Microsoft services. File versions and recycle bin can affect the difference."));
        }

        totals.Text = CloudSize(report.KnownBytes) + T(" em tamanhos informados", " in reported sizes") +
            "   ·   " + report.Count.ToString("N0") + T(" arquivos encontrados", " files found");
        if (report.QuotaOnly) totals.Text = google ? T("Use Analisar espaço para listar os arquivos e pastas do Drive.", "Use Analyze storage to list Drive files and folders.") :
            T("Use Analisar espaço para listar os arquivos e pastas do OneDrive.", "Use Analyze storage to list OneDrive files and folders.");
        if (report.UnknownSizes > 0) totals.Text += "   ·   " + report.UnknownSizes.ToString("N0") + T(" sem tamanho", " with no size");
        if (local) totals.Text += "\n" +
            T("Arquivos disponíveis no PC  ", "Files available on this PC  ") + CloudSize(report.LocalFileBytes) +
            "   ·   " + T("Somente online  ", "Online only  ") + CloudSize(report.OnlineFileBytes) +
            "\n" + T("Valores lógicos informados pelo serviço. Arquivos de disponibilidade desconhecida entram apenas no total.",
                "Logical sizes reported by the service. Files of unknown availability appear only in the total.");
        if (report.LimitReached) totals.Text += "\n" + T("Limite de 200 mil itens atingido.", "Reached the 200,000-item limit.");
        if (report.Skipped > 0) totals.Text += "\n" + report.Skipped.ToString("N0") + T(" itens ignorados ou inacessíveis.", " items skipped or inaccessible.");
        if (report.ListingFailed) totals.Text += "\n" + T("A consulta aos arquivos não foi concluída.", "The file listing did not complete.");
    }
}
}
