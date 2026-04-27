using System.Windows;
using System.Windows.Controls;

namespace MindForge.Views;

public partial class MaterialLibraryView : UserControl
{
    public MaterialLibraryView()
    {
        InitializeComponent();
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;

        if (DataContext is ViewModels.MaterialLibraryViewModel vm)
        {
            // Upload first dropped file via existing command
            // UploadMaterialCommand opens a dialog; for drag-drop we call
            // the service directly. We pass the file path via a relay.
            vm.UploadDroppedFileAsync(files[0]).ConfigureAwait(false);
        }
    }
}
