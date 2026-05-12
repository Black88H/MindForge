using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class GlobalSearchView : UserControl
{
    private readonly IGlobalSearchService _search;

    public GlobalSearchView()
    {
        InitializeComponent();
        _search = App.Services.GetRequiredService<IGlobalSearchService>();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _ = RunSearch();
    }

    private void OnSearch(object sender, RoutedEventArgs e)
        => _ = RunSearch();

    private async System.Threading.Tasks.Task RunSearch()
    {
        var query = TxtSearch.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            TxtStatus.Text       = "Bitte einen Suchbegriff eingeben.";
            TxtStatus.Visibility = Visibility.Visible;
            ResultsScroll.Visibility = Visibility.Collapsed;
            return;
        }

        TxtStatus.Text       = "Suche läuft…";
        TxtStatus.Visibility = Visibility.Visible;
        ResultsScroll.Visibility = Visibility.Collapsed;
        BtnSearch.IsEnabled  = false;

        try
        {
            var results = await _search.SearchAsync(query);
            ResultsList.ItemsSource = results;

            if (results.Count == 0)
            {
                TxtStatus.Text       = $"Keine Ergebnisse fuer \"{query}\" gefunden.";
                TxtStatus.Visibility = Visibility.Visible;
                ResultsScroll.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtStatus.Visibility  = Visibility.Collapsed;
                ResultsScroll.Visibility = Visibility.Visible;
            }
        }
        catch (System.Exception ex)
        {
            TxtStatus.Text       = $"Fehler: {ex.Message}";
            TxtStatus.Visibility = Visibility.Visible;
        }
        finally
        {
            BtnSearch.IsEnabled = true;
        }
    }

    private async void OnRebuildIndex(object sender, RoutedEventArgs e)
    {
        BtnRebuild.IsEnabled = false;
        TxtStatus.Text       = "Index wird aufgebaut…";
        TxtStatus.Visibility = Visibility.Visible;
        ResultsScroll.Visibility = Visibility.Collapsed;

        try
        {
            await _search.RebuildIndexAsync();
            TxtStatus.Text = "✅ Index erfolgreich aufgebaut.";
        }
        catch (System.Exception ex)
        {
            TxtStatus.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            BtnRebuild.IsEnabled = true;
        }
    }

    private void OnResultClick(object sender, MouseButtonEventArgs e)
    {
        // Future: navigate to the entity
    }
}
