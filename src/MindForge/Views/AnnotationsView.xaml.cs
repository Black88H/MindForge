using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Helpers;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class AnnotationsView : UserControl
{
    private readonly IAnnotationService _annotationService;
    private List<Annotation>            _allAnnotations = [];
    private Guid                        _userId;

    public AnnotationsView()
    {
        InitializeComponent();
        _annotationService = App.Services.GetRequiredService<IAnnotationService>();
        _userId            = UserSession.UserId;
        Loaded += async (_, _) => await LoadAsync(null);
    }

    private async Task LoadAsync(AnnotationType? filter)
    {
        try
        {
            _allAnnotations          = await _annotationService.GetForUserAsync(_userId, filter);
            AnnotationsList.ItemsSource = _allAnnotations;
            TxtCount.Text               = $"{_allAnnotations.Count} Einträge";
        }
        catch (Exception ex)
        {
            TxtCount.Text = "Fehler: " + ex.Message;
        }
    }

    private async void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboFilter.SelectedItem is not ComboBoxItem item) return;

        AnnotationType? filter = item.Content?.ToString() switch
        {
            "Highlight"  => AnnotationType.Highlight,
            "Wichtig"    => AnnotationType.Important,
            "Frage"      => AnnotationType.Question,
            "Konzept"    => AnnotationType.Concept,
            "Beispiel"   => AnnotationType.Example,
            "Todo"       => AnnotationType.Todo,
            "Verwirrend" => AnnotationType.Confusion,
            _            => null
        };

        await LoadAsync(filter);
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid id) return;

        try
        {
            await _annotationService.DeleteAsync(id);
            await LoadAsync(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Fehler: " + ex.Message);
        }
    }
}
