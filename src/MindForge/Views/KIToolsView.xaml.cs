using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Models;
using MindForge.ViewModels;

namespace MindForge.Views;

public partial class KIToolsView : UserControl
{
    private readonly KIToolsViewModel _vm;

    public KIToolsView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<KIToolsViewModel>();
        DataContext = _vm;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
        => await _vm.LoadSubjectsAsync();

    private async void OnSummarizeSubjectClick(object sender, RoutedEventArgs e)
        => await _vm.SummarizeSubjectCommand.ExecuteAsync(null);

    private async void OnStudyGuideClick(object sender, RoutedEventArgs e)
        => await _vm.GenerateStudyGuideCommand.ExecuteAsync(null);

    private async void OnAudioOverviewClick(object sender, RoutedEventArgs e)
        => await _vm.GenerateAudioOverviewCommand.ExecuteAsync(null);

    private async void OnAskClick(object sender, RoutedEventArgs e)
        => await _vm.AskWithSourcesCommand.ExecuteAsync(null);

    private async void OnSpeakClick(object sender, RoutedEventArgs e)
        => await _vm.SpeakOutputCommand.ExecuteAsync(null);

    private void OnStopSpeakClick(object sender, RoutedEventArgs e)
        => _vm.StopSpeakingCommand.Execute(null);

    private void OnSourceMaterialSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (Material added in e.AddedItems)
            _vm.ToggleMaterialSelection(added);
        foreach (Material removed in e.RemovedItems)
            _vm.ToggleMaterialSelection(removed);
    }
}
