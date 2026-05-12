using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Helpers;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class ConceptMapView : UserControl
{
    private readonly IAIConceptGraphService _graphService;

    // Force-directed layout state
    private class NodeState
    {
        public string       Id          { get; set; } = "";
        public string       Label       { get; set; } = "";
        public string       Type        { get; set; } = "concept";
        public double       Importance  { get; set; } = 0.5;
        public string       Description { get; set; } = "";
        public double       X           { get; set; }
        public double       Y           { get; set; }
        public double       Vx          { get; set; }
        public double       Vy          { get; set; }
        public Ellipse?     Ellipse     { get; set; }
        public TextBlock?   Label_TB    { get; set; }
    }

    private readonly List<NodeState>          _nodes      = [];
    private readonly List<(string, string)>   _edges      = [];   // (sourceId, targetId)
    private          NodeState?               _dragging;
    private          Point                    _dragOffset;
    private          Guid                     _selectedNotebook = Guid.Empty;

    private static readonly Dictionary<string, Color> NodeColors = new()
    {
        ["concept"]   = Color.FromRgb(99,  102, 241),  // indigo
        ["definition"]= Color.FromRgb(16,  185, 129),  // green
        ["process"]   = Color.FromRgb(245, 158,  11),  // amber
        ["example"]   = Color.FromRgb(59,  130, 246),  // blue
        ["principle"] = Color.FromRgb(239,  68,  68),  // red
    };

    public ConceptMapView()
    {
        InitializeComponent();
        _graphService = App.Services.GetRequiredService<IAIConceptGraphService>();
        Loaded += async (_, _) => await InitAsync();
    }

    private async Task InitAsync()
    {
        var notebooks = await LoadNotebooksAsync();
        CboNotebook.ItemsSource = notebooks;
        if (notebooks.Count > 0)
        {
            CboNotebook.SelectedIndex = 0;
            _selectedNotebook = notebooks[0].Id;
            // Try to load cached graph
            await TryLoadCachedAsync();
        }
    }

    private async Task<List<Notebook>> LoadNotebooksAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MindForge.Data.MindForgeDbContext>();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Notebooks.Where(n => n.UserId == UserSession.UserId));
    }

    private async Task TryLoadCachedAsync()
    {
        if (_selectedNotebook == Guid.Empty) return;
        try
        {
            var graph = await _graphService.GetLatestAsync(_selectedNotebook);
            if (graph is not null)
                await Dispatcher.InvokeAsync(() => RenderGraph(graph));
        }
        catch { /* no cached graph */ }
    }

    private async void OnNotebookChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboNotebook.SelectedItem is Notebook nb)
        {
            _selectedNotebook = nb.Id;
            await TryLoadCachedAsync();
        }
    }

    private async void OnGenerate(object sender, RoutedEventArgs e)
    {
        if (_selectedNotebook == Guid.Empty)
        {
            MessageBox.Show("Bitte zuerst ein Notizbuch wählen.");
            return;
        }

        BtnGenerate.IsEnabled = false;
        TxtStatus.Text        = "⏳ KI generiert Konzeptkarte...";
        TxtStatus.Visibility  = Visibility.Visible;
        GraphCanvas.Children.Clear();
        _nodes.Clear();
        _edges.Clear();

        try
        {
            var graph = await _graphService.GenerateAsync(_selectedNotebook);
            TxtStatus.Visibility = Visibility.Collapsed;
            RenderGraph(graph);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "Fehler: " + ex.Message;
        }
        finally
        {
            BtnGenerate.IsEnabled = true;
        }
    }

    private void OnRelayout(object sender, RoutedEventArgs e)
    {
        RandomizePositions();
        RunForceDirected();
        UpdateCanvas();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void RenderGraph(ConceptGraphData graph)
    {
        GraphCanvas.Children.Clear();
        _nodes.Clear();
        _edges.Clear();

        var rng = new Random(42);
        var w   = Math.Max(GraphCanvas.ActualWidth,  800);
        var h   = Math.Max(GraphCanvas.ActualHeight, 600);

        // Build node states with random initial positions
        foreach (var n in graph.Nodes)
        {
            _nodes.Add(new NodeState
            {
                Id          = n.Id,
                Label       = n.Label,
                Type        = n.Type,
                Importance  = n.Importance,
                Description = n.Description,
                X           = rng.NextDouble() * (w - 100) + 50,
                Y           = rng.NextDouble() * (h - 100) + 50,
            });
        }

        // Store edges
        foreach (var e in graph.Edges)
            _edges.Add((e.Source, e.Target));

        // Run physics
        RunForceDirected();
        UpdateCanvas();

        TxtNodeInfo.Text = $"{_nodes.Count} Knoten · {_edges.Count} Verbindungen";
    }

    private void RandomizePositions()
    {
        var rng = new Random();
        var w   = Math.Max(GraphCanvas.ActualWidth,  800);
        var h   = Math.Max(GraphCanvas.ActualHeight, 600);
        foreach (var n in _nodes)
        {
            n.X  = rng.NextDouble() * (w - 100) + 50;
            n.Y  = rng.NextDouble() * (h - 100) + 50;
            n.Vx = 0; n.Vy = 0;
        }
    }

    private void RunForceDirected()
    {
        const double repulsion  = 4000;
        const double attraction = 0.04;
        const double damping    = 0.85;
        const double minDist    = 60;
        const int    iterations = 120;

        var nodeDict = _nodes.ToDictionary(n => n.Id);
        var w        = Math.Max(GraphCanvas.ActualWidth,  800);
        var h        = Math.Max(GraphCanvas.ActualHeight, 600);

        for (int iter = 0; iter < iterations; iter++)
        {
            // Repulsion between all node pairs
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var a = _nodes[i]; var b = _nodes[j];
                    var dx = b.X - a.X;
                    var dy = b.Y - a.Y;
                    var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), minDist);
                    var force = repulsion / (dist * dist);
                    a.Vx -= force * dx / dist;
                    a.Vy -= force * dy / dist;
                    b.Vx += force * dx / dist;
                    b.Vy += force * dy / dist;
                }
            }

            // Attraction along edges
            foreach (var (srcId, tgtId) in _edges)
            {
                if (!nodeDict.TryGetValue(srcId, out var src) || !nodeDict.TryGetValue(tgtId, out var tgt)) continue;
                var dx    = tgt.X - src.X;
                var dy    = tgt.Y - src.Y;
                var dist  = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1);
                var force = attraction * dist;
                src.Vx += force * dx / dist;
                src.Vy += force * dy / dist;
                tgt.Vx -= force * dx / dist;
                tgt.Vy -= force * dy / dist;
            }

            // Apply velocity + damping + bounds
            foreach (var n in _nodes)
            {
                n.Vx *= damping; n.Vy *= damping;
                n.X   = Math.Clamp(n.X + n.Vx, 50, w - 50);
                n.Y   = Math.Clamp(n.Y + n.Vy, 50, h - 50);
            }
        }
    }

    private void UpdateCanvas()
    {
        GraphCanvas.Children.Clear();
        var nodeDict = _nodes.ToDictionary(n => n.Id);

        // Draw edges first (below nodes)
        foreach (var (srcId, tgtId) in _edges)
        {
            if (!nodeDict.TryGetValue(srcId, out var src) || !nodeDict.TryGetValue(tgtId, out var tgt)) continue;
            var line = new Line
            {
                X1 = src.X, Y1 = src.Y,
                X2 = tgt.X, Y2 = tgt.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 99, 102, 241)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection([4, 3])
            };
            GraphCanvas.Children.Add(line);
        }

        // Draw nodes
        foreach (var n in _nodes)
        {
            double radius = 20 + n.Importance * 20;   // 20–40 px
            Color  color  = NodeColors.TryGetValue(n.Type, out var c) ? c : Color.FromRgb(99, 102, 241);

            var ellipse = new Ellipse
            {
                Width           = radius * 2,
                Height          = radius * 2,
                Fill            = new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
                Stroke          = new SolidColorBrush(Colors.White),
                StrokeThickness = 1.5,
                ToolTip         = n.Description.Length > 0 ? n.Label + ": " + n.Description : n.Label,
                Tag             = n.Id,
                Cursor          = Cursors.Hand
            };
            Canvas.SetLeft(ellipse, n.X - radius);
            Canvas.SetTop(ellipse,  n.Y - radius);

            var label = new TextBlock
            {
                Text             = n.Label.Length > 14 ? n.Label[..12] + "…" : n.Label,
                Foreground       = new SolidColorBrush(Colors.White),
                FontSize         = Math.Max(9, 9 + n.Importance * 3),
                FontWeight       = FontWeights.SemiBold,
                TextAlignment    = TextAlignment.Center,
                Width            = radius * 2,
                TextWrapping     = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, n.X - radius);
            Canvas.SetTop(label,  n.Y - radius + radius - 10);

            n.Ellipse  = ellipse;
            n.Label_TB = label;

            GraphCanvas.Children.Add(ellipse);
            GraphCanvas.Children.Add(label);
        }
    }

    // ── Drag support ──────────────────────────────────────────────────────────

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(GraphCanvas);
        _dragging = _nodes.FirstOrDefault(n =>
        {
            var dx = pos.X - n.X; var dy = pos.Y - n.Y;
            return Math.Sqrt(dx * dx + dy * dy) < 30;
        });
        if (_dragging is not null)
        {
            _dragOffset = new Point(pos.X - _dragging.X, pos.Y - _dragging.Y);
            GraphCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging is null) return;
        var pos = e.GetPosition(GraphCanvas);
        _dragging.X = pos.X - _dragOffset.X;
        _dragging.Y = pos.Y - _dragOffset.Y;

        // Move ellipse + label
        if (_dragging.Ellipse is not null)
        {
            double r = _dragging.Ellipse.Width / 2;
            Canvas.SetLeft(_dragging.Ellipse,  _dragging.X - r);
            Canvas.SetTop(_dragging.Ellipse,   _dragging.Y - r);
            Canvas.SetLeft(_dragging.Label_TB!, _dragging.X - r);
            Canvas.SetTop(_dragging.Label_TB!,  _dragging.Y - r + r - 10);
        }

        // Redraw all edges
        RedrawEdges();
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = null;
        GraphCanvas.ReleaseMouseCapture();
    }

    private void RedrawEdges()
    {
        // Remove existing lines (they're at lower z-order — indices below first ellipse)
        var lines = GraphCanvas.Children.OfType<Line>().ToList();
        foreach (var l in lines) GraphCanvas.Children.Remove(l);

        var nodeDict = _nodes.ToDictionary(n => n.Id);
        foreach (var (srcId, tgtId) in _edges)
        {
            if (!nodeDict.TryGetValue(srcId, out var src) || !nodeDict.TryGetValue(tgtId, out var tgt)) continue;
            var line = new Line
            {
                X1 = src.X, Y1 = src.Y,
                X2 = tgt.X, Y2 = tgt.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 99, 102, 241)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection([4, 3])
            };
            GraphCanvas.Children.Insert(0, line);
        }
    }
}
