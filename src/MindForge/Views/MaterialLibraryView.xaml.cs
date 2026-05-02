using System.Windows;
using System.Windows.Controls;
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
            Filter = "Supported Files|*.pdf;*.docx;*.txt;*.md|PDF|*.pdf|Word|*.docx|Text|*.txt;*.md",
            Multiselect = false
        };
        if (dlg.ShowDialog() != true) return;
        await _vm.IngestFileCommand.ExecuteAsync(dlg.FileName);
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
}
