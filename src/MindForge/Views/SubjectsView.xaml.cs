using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MindForge.Helpers;

namespace MindForge.Views;

public class SubjectItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public double Progress { get; set; }
}

public partial class SubjectsView : UserControl
{
    public ObservableCollection<SubjectItem> Subjects { get; set; }

    public SubjectsView()
    {
        InitializeComponent();
        
        Subjects = new ObservableCollection<SubjectItem>
        {
            new SubjectItem { Id = Guid.NewGuid(), Name = "Informatik", Subtitle = "Algorithmen & Datenstrukturen", Icon = "💻", Progress = 65.1234 },
            new SubjectItem { Id = Guid.NewGuid(), Name = "Biologie", Subtitle = "Zellbiologie & Genetik", Icon = "🧬", Progress = 30.5678 },
            new SubjectItem { Id = Guid.NewGuid(), Name = "Mathematik", Subtitle = "Analysis & Lineare Algebra", Icon = "📈", Progress = 85.9876 }
        };

        SubjectsList.ItemsSource = Subjects;
    }

    private void OnAddSubjectClick(object sender, RoutedEventArgs e)
    {
        TxtSubjectName.Text = "";
        TxtSubjectSubtitle.Text = "";
        TxtSubjectIcon.Text = "📚";
        ModalOverlay.Visibility = Visibility.Visible;
    }

    private void OnCancelModalClick(object sender, RoutedEventArgs e)
    {
        ModalOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnSaveSubjectClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtSubjectName.Text)) return;

        Subjects.Add(new SubjectItem
        {
            Id = Guid.NewGuid(),
            Name = TxtSubjectName.Text,
            Subtitle = TxtSubjectSubtitle.Text,
            Icon = string.IsNullOrWhiteSpace(TxtSubjectIcon.Text) ? "📚" : TxtSubjectIcon.Text,
            Progress = 0.0000
        });
        
        ModalOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnDeleteSubjectClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            var subject = Subjects.FirstOrDefault(s => s.Id == id);
            if (subject != null)
            {
                Subjects.Remove(subject);
            }
        }
    }
}