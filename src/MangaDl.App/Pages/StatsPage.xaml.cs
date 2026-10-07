using MangaDl.Core;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

/// <summary>One day in the activity heatmap (0–3 intensity).</summary>
public sealed record HeatDay(int Level);

public sealed partial class StatsPage : Page
{
    // TODO(backend): feed real reading stats.
    public IReadOnlyList<HeatDay> Activity { get; } = Sample.Activity.Select(level => new HeatDay(level)).ToList();
    public IReadOnlyList<Bar> ByCategory { get; } = Sample.ByCategory;
    public IReadOnlyList<Bar> BySource { get; } = Sample.BySource;

    public StatsPage() => InitializeComponent();
}
