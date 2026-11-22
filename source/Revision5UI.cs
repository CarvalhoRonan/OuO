using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace Aurora {
public partial class MainWindow {
    void VerifyRevision5UI(){
        ShowPage("tweaks");if(tweakPreferenceRows==null||tweakPreferenceRows.Count(x=>x.Menu!=null)!=23||tweakPreferenceRows.Any(x=>x.Preference.Multiple&&x.Check!=null))throw new InvalidOperationException("Multi-option tweaks must have a single selector.");
        var search=tweakPreferenceRows.Single(x=>x.Preference.Id=="search-style");string before=journal.History();search.Menu.SelectedIndex=2;if(!search.Pending||journal.History()!=before)throw new InvalidOperationException("Choosing an option must only prepare a selection.");Capture("diagnostics/tweaks-selectors-pt.png");search.Select(null,false);
        ShowPage("cleanup");temporarySelection.SetAll(true);if(temporarySelection.Count!=2||temporarySelection.Bytes!=15728640||!temporarySelectionState.Text.Contains("2"))throw new InvalidOperationException("Select all temporary files must update selection and total size.");Capture("diagnostics/cleanup-selected-pt.png");temporarySelection.SetAll(false);
        ShowPage("storage");string root=diskBrowserLocation(),child=Path.Combine(root,"Projetos");int scans=diskBrowserScanCount();diskBrowserNavigate(child);UpdateLayout();var selected=diskFolderTree.SelectedItem as TreeViewItem;if(selected==null||!String.Equals(Convert.ToString(selected.Tag),child,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Folder tree must select the navigated path.");var parent=diskFileList.Items.OfType<ListBoxItem>().FirstOrDefault();if(parent==null||!String.Equals(parent.Tag as string,root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The file list must expose its parent folder.");
        if(!diskFileList.ContextMenu.Items.OfType<MenuItem>().Any(x=>Convert.ToString(x.Header)==T("Voltar","Back")))throw new InvalidOperationException("Disk context menu must include Back.");pageScroll.ScrollToVerticalOffset(500);Capture("diagnostics/disk-tree-navigated-pt.png");diskBrowserBack();if(diskBrowserLocation()!=root||diskBrowserScanCount()!=scans||diskBrowserCacheHits()<2)throw new InvalidOperationException("Returning to visited folders must reuse the cache.");
        Width=1024;Height=768;ShowPage("tweaks");Capture("diagnostics/tweaks-compact-pt.png");ShowPage("cleanup");Capture("diagnostics/cleanup-compact-pt.png");Width=1320;Height=900;
    }
}
}
