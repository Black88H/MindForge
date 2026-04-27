using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Models;
using MindForge.Services;

namespace MindForge.ViewModels;

public partial class KnowledgeGraphViewModel : ObservableObject
{
    private readonly IKnowledgeGraphService _graph;

    [ObservableProperty] private ObservableCollection<KnowledgeNode> _nodes = new();
    [ObservableProperty] private ObservableCollection<KnowledgeEdge> _edges = new();
    [ObservableProperty] private KnowledgeNode? _selectedNode;

    public bool HasNodes         => Nodes.Count > 0;
    public bool HasSelectedNode  => SelectedNode != null;

    public KnowledgeGraphViewModel(IKnowledgeGraphService graph) => _graph = graph;

    partial void OnNodesChanged(ObservableCollection<KnowledgeNode> value)
        => OnPropertyChanged(nameof(HasNodes));

    partial void OnSelectedNodeChanged(KnowledgeNode? value)
        => OnPropertyChanged(nameof(HasSelectedNode));

    public async Task LoadGraphAsync(Guid subjectId)
    {
        var nodes = await _graph.GetNodesForSubjectAsync(subjectId, Utils.UserSession.UserId);
        var edges = await _graph.GetEdgesForSubjectAsync(subjectId, Utils.UserSession.UserId);

        Nodes.Clear();
        Edges.Clear();

        foreach (var n in nodes) Nodes.Add(n);
        foreach (var e in edges) Edges.Add(e);
        OnPropertyChanged(nameof(HasNodes));
    }

    [RelayCommand]
    private void SelectNode(KnowledgeNode node)
    {
        SelectedNode = node;
    }
}
