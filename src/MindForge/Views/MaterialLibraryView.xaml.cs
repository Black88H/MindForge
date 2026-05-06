using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using MindForge.ViewModels;

namespace MindForge.Views;

public partial class MaterialLibraryView : UserControl
{
    private readonly MaterialLibraryViewModel _vm;

    public MaterialLibraryView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MaterialLibraryViewModel>();
        DataContext = _vm;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadSubjectsAsync();
    }

    private async void OnUploadFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Material hochladen",
            Filter = "Alle unterstützten Dateien|*.pdf;*.docx;*.txt;*.md;*.csv;*.json;*.html;*.htm;*.xml;*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.tiff;*.tif" +
                     "|Dokumente|*.pdf;*.docx;*.txt;*.md" +
                     "|Bilder (OCR)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.tiff;*.tif" +
                     "|Daten & Web|*.csv;*.json;*.html;*.htm;*.xml" +
                     "|Alle Dateien|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        foreach (var file in dlg.FileNames)
            await _vm.IngestFileCommand.ExecuteAsync(file);
    }

    private async void OnAddUrlClick(object sender, RoutedEventArgs e)
    {
        var url = TxtWebUrl.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;
        await _vm.IngestUrlCommand.ExecuteAsync(url);
        TxtWebUrl.Clear();
    }

    private async void OnSummarizeClick(object sender, RoutedEventArgs e)
        => await _vm.SummarizeMaterialCommand.ExecuteAsync(null);

    private async void OnTopicsClick(object sender, RoutedEventArgs e)
        => await _vm.GenerateTopicsCommand.ExecuteAsync(null);

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedMaterial == null) return;
        var result = MessageBox.Show(
            $"Delete '{_vm.SelectedMaterial.OriginalFileName}'?",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
            await _vm.DeleteMaterialCommand.ExecuteAsync(null);
    }

    // ── Drag & Drop ───────────────────────────────────────────────────────────

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            if (sender is Border border)
            {
                border.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
                border.BorderThickness = new Thickness(2);
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = Brushes.Transparent;
            border.BorderThickness = new Thickness(0);
        }
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = Brushes.Transparent;
            border.BorderThickness = new Thickness(0);
        }

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null) return;

        foreach (var file in files)
            await _vm.IngestFileCommand.ExecuteAsync(file);

        e.Handled = true;
    }
}
