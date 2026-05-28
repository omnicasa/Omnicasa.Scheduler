using System.Collections;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule;

/// <summary>
/// A two-column agenda list: the date on the left, that day's appointments on the right. Bind
/// <see cref="ItemsSource"/> to a set of <see cref="IScheduleItem"/>s; the control groups them by
/// day, shows a placeholder for empty days, and loads more days as you scroll up or down (infinite,
/// anchored at <see cref="AnchorDate"/>). The current day's date stays pinned at the top-left.
/// Internally it is a flat, fully-virtualized <see cref="CollectionView"/> (one row per appointment;
/// the date renders on each day's first row), which keeps scrolling smooth.
/// </summary>
public class AgendaListView : ContentView
{
    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(AgendaListView),
            null,
            propertyChanged: (b, _, _) => ((AgendaListView)b).RebuildWindow());

    /// <summary>Bindable property for <see cref="AnchorDate"/>.</summary>
    public static readonly BindableProperty AnchorDateProperty =
        BindableProperty.Create(
            nameof(AnchorDate),
            typeof(DateTime),
            typeof(AgendaListView),
            DateTime.Today,
            propertyChanged: (b, _, _) => ((AgendaListView)b).RebuildWindow());

    /// <summary>Bindable property for <see cref="Theme"/>.</summary>
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(
            nameof(Theme),
            typeof(ScheduleTheme),
            typeof(AgendaListView),
            null,
            propertyChanged: (b, _, _) => ((AgendaListView)b).RebuildWindow());

    /// <summary>Bindable property for <see cref="EmptyDayText"/>.</summary>
    public static readonly BindableProperty EmptyDayTextProperty =
        BindableProperty.Create(
            nameof(EmptyDayText),
            typeof(string),
            typeof(AgendaListView),
            "No events",
            propertyChanged: (b, _, _) => ((AgendaListView)b).RebuildWindow());

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(AgendaListView), null);

    /// <summary>Bindable property for <see cref="DateTemplate"/>.</summary>
    public static readonly BindableProperty DateTemplateProperty =
        BindableProperty.Create(nameof(DateTemplate), typeof(DataTemplate), typeof(AgendaListView), null);

    /// <summary>Bindable property for <see cref="ShowEmptyDays"/>.</summary>
    public static readonly BindableProperty ShowEmptyDaysProperty =
        BindableProperty.Create(
            nameof(ShowEmptyDays),
            typeof(bool),
            typeof(AgendaListView),
            true,
            propertyChanged: (b, _, _) => ((AgendaListView)b).RebuildWindow());

    /// <summary>Bindable property for <see cref="LimitToItemsSource"/>.</summary>
    public static readonly BindableProperty LimitToItemsSourceProperty =
        BindableProperty.Create(
            nameof(LimitToItemsSource),
            typeof(bool),
            typeof(AgendaListView),
            false,
            propertyChanged: (b, _, _) => ((AgendaListView)b).RebuildWindow());

    private readonly ScheduleTheme fallbackTheme = new ScheduleTheme();

    private readonly ObservableCollection<AgendaRow> rows = new ObservableCollection<AgendaRow>();

    private readonly CollectionView collection;

    private readonly Grid stickyOverlay;

    private readonly Label stickyWeekday;

    private readonly Label stickyDayNumber;

    private DateOnly firstDay;

    private DateOnly lastDay;

    private bool built;

    private bool extending;

    private DateOnly? overlayDate;

    /// <summary>Initializes a new instance of the <see cref="AgendaListView"/> class.</summary>
    public AgendaListView()
    {
        collection = new CollectionView
        {
            ItemsSource = rows,
            ItemTemplate = BuildRowTemplate(),
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical),
            SelectionMode = SelectionMode.None,
            RemainingItemsThreshold = 8,
        };
        collection.RemainingItemsThresholdReached += (_, _) => ExtendForward();
        collection.Scrolled += OnScrolled;

        stickyWeekday = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.Start };
        stickyDayNumber = new Label { FontSize = 24, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Start };
        stickyOverlay = new Grid
        {
            IsVisible = false,
            InputTransparent = true,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            WidthRequest = 76,
            Padding = new Thickness(16, 8, 0, 6),
            BackgroundColor = fallbackTheme.Background,
            Children = { new VerticalStackLayout { Spacing = 0, Children = { stickyWeekday, stickyDayNumber } } },
        };

        Content = new Grid { Children = { collection, stickyOverlay } };
        Loaded += (_, _) =>
        {
            if (!built)
            {
                RebuildWindow();
            }
        };
    }

    /// <summary>Occurs when the user taps an appointment (placeholders are ignored).</summary>
    public event EventHandler<ScheduleItemTappedEventArgs>? ItemTapped;

    /// <summary>Gets or sets the number of days loaded before the anchor on first build.</summary>
    public int InitialBackDays { get; set; } = 7;

    /// <summary>Gets or sets the number of days loaded after the anchor on first build.</summary>
    public int InitialForwardDays { get; set; } = 21;

    /// <summary>Gets or sets the number of days appended/prepended each time the edge is reached.</summary>
    public int PageSize { get; set; } = 14;

    /// <summary>Gets or sets the items shown (any <see cref="IEnumerable"/> of <see cref="IScheduleItem"/>).</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Gets or sets the day the list is centered on when first built.</summary>
    public DateTime AnchorDate
    {
        get => (DateTime)GetValue(AnchorDateProperty);
        set => SetValue(AnchorDateProperty, value);
    }

    /// <summary>Gets or sets the color theme used by the default templates.</summary>
    public ScheduleTheme Theme
    {
        get => (ScheduleTheme)GetValue(ThemeProperty) ?? fallbackTheme;
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>Gets or sets the placeholder text shown on days with no events.</summary>
    public string EmptyDayText
    {
        get => (string)GetValue(EmptyDayTextProperty);
        set => SetValue(EmptyDayTextProperty, value);
    }

    /// <summary>Gets or sets the template for one appointment on the right (binds to <see cref="AgendaEntry"/>). Null uses the built-in look.</summary>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>Gets or sets the template for the date column on the left (binds to <see cref="AgendaRow"/>). Null uses the built-in look.</summary>
    public DataTemplate? DateTemplate
    {
        get => (DataTemplate?)GetValue(DateTemplateProperty);
        set => SetValue(DateTemplateProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether days with no appointments render a placeholder row. When false, empty days are skipped.</summary>
    public bool ShowEmptyDays
    {
        get => (bool)GetValue(ShowEmptyDaysProperty);
        set => SetValue(ShowEmptyDaysProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the scroll range is clamped to the date range of
    /// <see cref="ItemsSource"/> (first item's start day .. last item's end day). When false, the
    /// list scrolls infinitely in both directions.
    /// </summary>
    public bool LimitToItemsSource
    {
        get => (bool)GetValue(LimitToItemsSourceProperty);
        set => SetValue(LimitToItemsSourceProperty, value);
    }

    /// <summary>Scrolls so the given day is at the top, loading it into the window first if needed.</summary>
    /// <param name="date">Target day.</param>
    /// <param name="animated">Whether to animate the scroll.</param>
    public void ScrollToDate(DateOnly date, bool animated = true)
    {
        if (!built)
        {
            return;
        }

        var min = EffectiveMin();
        var max = EffectiveMax();
        if (min is { } mn && date < mn)
        {
            date = mn;
        }

        if (max is { } mx && date > mx)
        {
            date = mx;
        }

        if (date < firstDay)
        {
            PrependDays(date, firstDay.AddDays(-1));
        }
        else if (date > lastDay)
        {
            AppendDays(lastDay.AddDays(1), date);
        }

        int index = IndexOfDayOrAfter(date);
        if (index >= 0)
        {
            collection.ScrollTo(index, -1, ScrollToPosition.Start, animated);
        }
    }

    private IEnumerable<IScheduleItem> Items()
        => ItemsSource?.Cast<object?>().OfType<IScheduleItem>() ?? Enumerable.Empty<IScheduleItem>();

    // First row whose date equals (or, failing that, is the first after) the target. -1 if none.
    private int IndexOfDayOrAfter(DateOnly date)
    {
        int after = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Date == date)
            {
                return i;
            }

            if (after < 0 && rows[i].Date > date)
            {
                after = i;
            }
        }

        return after;
    }

    // Effective scroll bounds: the items' date range when LimitToItemsSource, else unbounded.
    private DateOnly? EffectiveMin()
        => LimitToItemsSource ? ItemsRange()?.Min : null;

    private DateOnly? EffectiveMax()
        => LimitToItemsSource ? ItemsRange()?.Max : null;

    private (DateOnly Min, DateOnly Max)? ItemsRange()
    {
        DateOnly? min = null;
        DateOnly? max = null;
        foreach (var item in Items())
        {
            var s = DateOnly.FromDateTime(item.Start);
            var e = DateOnly.FromDateTime(item.End);
            if (e < s)
            {
                e = s;
            }

            if (min is null || s < min)
            {
                min = s;
            }

            if (max is null || e > max)
            {
                max = e;
            }
        }

        return min is { } mn && max is { } mx ? (mn, mx) : null;
    }

    private void RebuildWindow()
    {
        var anchor = DateOnly.FromDateTime(AnchorDate.Date);
        var min = EffectiveMin();
        var max = EffectiveMax();
        if (min is { } mn && anchor < mn)
        {
            anchor = mn;
        }

        if (max is { } mx && anchor > mx)
        {
            anchor = mx;
        }

        firstDay = anchor.AddDays(-Math.Max(0, InitialBackDays));
        lastDay = anchor.AddDays(Math.Max(0, InitialForwardDays));
        if (min is { } mn2 && firstDay < mn2)
        {
            firstDay = mn2;
        }

        if (max is { } mx2 && lastDay > mx2)
        {
            lastDay = mx2;
        }

        if (lastDay < firstDay)
        {
            lastDay = firstDay;
        }

        rows.Clear();
        foreach (var row in AgendaGrouping.BuildRows(Items(), firstDay, lastDay, EmptyDayText, Theme, ShowEmptyDays))
        {
            rows.Add(row);
        }

        built = true;

        stickyOverlay.BackgroundColor = Theme.Background;
        int anchorIndex = IndexOfDayOrAfter(anchor);
        if (anchorIndex < 0 && rows.Count > 0)
        {
            anchorIndex = 0;
        }

        if (anchorIndex >= 0)
        {
            overlayDate = rows[anchorIndex].Date;
            UpdateOverlay(rows[anchorIndex]);
            stickyOverlay.IsVisible = true;
        }
        else
        {
            stickyOverlay.IsVisible = false;
        }

        // Defer so the CollectionView has its rows before we position on the anchor.
        Dispatcher.Dispatch(() => ScrollToDate(anchor, animated: false));
    }

    private void ExtendForward()
    {
        if (extending || !built)
        {
            return;
        }

        if (EffectiveMax() is { } max && lastDay >= max)
        {
            return;
        }

        extending = true;
        AppendDays(lastDay.AddDays(1), lastDay.AddDays(Math.Max(1, PageSize)));
        extending = false;
    }

    private void ExtendBackward()
    {
        if (extending || !built)
        {
            return;
        }

        if (EffectiveMin() is { } min && firstDay <= min)
        {
            return;
        }

        extending = true;

        var from = firstDay.AddDays(-Math.Max(1, PageSize));
        if (EffectiveMin() is { } mn && from < mn)
        {
            from = mn;
        }

        var to = firstDay.AddDays(-1);
        if (to >= from)
        {
            var newRows = AgendaGrouping.BuildRows(Items(), from, to, EmptyDayText, Theme, ShowEmptyDays);
            firstDay = from;   // advance the window even across empty stretches
            if (newRows.Count > 0)
            {
                for (int i = newRows.Count - 1; i >= 0; i--)
                {
                    rows.Insert(0, newRows[i]);
                }

                // Keep the viewport steady: the previously-first row is now at newRows.Count.
                Dispatcher.Dispatch(() => collection.ScrollTo(newRows.Count, -1, ScrollToPosition.Start, animate: false));
            }
        }

        extending = false;
    }

    private void AppendDays(DateOnly from, DateOnly to)
    {
        if (EffectiveMax() is { } max && to > max)
        {
            to = max;
        }

        if (to < from)
        {
            return;
        }

        foreach (var row in AgendaGrouping.BuildRows(Items(), from, to, EmptyDayText, Theme, ShowEmptyDays))
        {
            rows.Add(row);
        }

        lastDay = to;
    }

    private void PrependDays(DateOnly from, DateOnly to)
    {
        if (EffectiveMin() is { } min && from < min)
        {
            from = min;
        }

        if (to < from)
        {
            return;
        }

        var newRows = AgendaGrouping.BuildRows(Items(), from, to, EmptyDayText, Theme, ShowEmptyDays);
        for (int i = newRows.Count - 1; i >= 0; i--)
        {
            rows.Insert(0, newRows[i]);
        }

        firstDay = from;
    }

    private void OnScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        int idx = e.FirstVisibleItemIndex;
        if (idx >= 0 && idx < rows.Count && rows[idx].Date != overlayDate)
        {
            overlayDate = rows[idx].Date;
            UpdateOverlay(rows[idx]);
        }

        if (idx <= 1 && !extending)
        {
            ExtendBackward();
        }
    }

    private void UpdateOverlay(AgendaRow row)
    {
        stickyWeekday.Text = row.WeekdayText;
        stickyWeekday.TextColor = row.HeaderColor;
        stickyDayNumber.Text = row.DayNumberText;
        stickyDayNumber.TextColor = row.HeaderColor;

        // Subtle "push up" as the next day takes over the pin.
        stickyOverlay.AbortAnimation("StickyPush");
        stickyOverlay.TranslationY = 6;
        _ = stickyOverlay.TranslateTo(0, 0, 110, Easing.CubicOut);
    }

    // One flat row: date column (left, shown on a day's first row) + one appointment (right).
    private DataTemplate BuildRowTemplate()
    {
        return new DataTemplate(() =>
        {
            var grid = new Grid
            {
                Padding = new Thickness(16, 8),
                ColumnSpacing = 12,
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(60)),
                    new ColumnDefinition(GridLength.Star),
                },
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Star),
                    new RowDefinition(GridLength.Auto),
                },
            };

            var dateView = BuildDateColumn();
            Grid.SetColumn(dateView, 0);
            grid.Add(dateView);

            var appointment = BuildAppointmentCell();
            Grid.SetColumn(appointment, 1);
            grid.Add(appointment);

            var separator = new BoxView { HeightRequest = 1, Color = Theme.GridLine, Margin = new Thickness(0, 8, 0, 0) };
            separator.SetBinding(IsVisibleProperty, nameof(AgendaRow.ShowSeparator));
            Grid.SetRow(separator, 1);
            Grid.SetColumnSpan(separator, 2);
            grid.Add(separator);

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                if (grid.BindingContext is AgendaRow { Entry.Item: { } item })
                {
                    ItemTapped?.Invoke(this, new ScheduleItemTappedEventArgs(item));
                }
            };
            grid.GestureRecognizers.Add(tap);

            return grid;
        });
    }

    // The date column: a custom DateTemplate (materialized once per pooled row), else built-in labels.
    private View BuildDateColumn()
    {
        if (DateTemplate?.CreateContent() is View custom)
        {
            custom.SetBinding(IsVisibleProperty, nameof(AgendaRow.ShowDate));
            return custom;
        }

        var stack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Spacing = 0 };
        stack.SetBinding(IsVisibleProperty, nameof(AgendaRow.ShowDate));

        var weekday = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.Start };
        weekday.SetBinding(Label.TextProperty, nameof(AgendaRow.WeekdayText));
        weekday.SetBinding(Label.TextColorProperty, nameof(AgendaRow.HeaderColor));

        var dayNumber = new Label { FontSize = 24, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Start };
        dayNumber.SetBinding(Label.TextProperty, nameof(AgendaRow.DayNumberText));
        dayNumber.SetBinding(Label.TextColorProperty, nameof(AgendaRow.HeaderColor));

        stack.Add(weekday);
        stack.Add(dayNumber);
        return stack;
    }

    // The appointment cell on the right. Custom ItemTemplate (bound to Entry) or the built-in look.
    private View BuildAppointmentCell()
    {
        if (ItemTemplate?.CreateContent() is View custom)
        {
            custom.SetBinding(BindingContextProperty, new Binding(nameof(AgendaRow.Entry)));
            return custom;
        }

        var grid = new Grid
        {
            ColumnSpacing = 10,
            VerticalOptions = LayoutOptions.Center,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
            },
        };

        var bar = new BoxView { WidthRequest = 4, CornerRadius = 2, VerticalOptions = LayoutOptions.Fill };
        bar.SetBinding(BoxView.ColorProperty, "Entry.Accent");
        bar.SetBinding(IsVisibleProperty, "Entry.ShowAccent");
        Grid.SetColumn(bar, 0);
        grid.Add(bar);

        var stack = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
        var title = new Label { FontSize = 15 };
        title.SetBinding(Label.TextProperty, "Entry.Title");
        var time = new Label { FontSize = 12, TextColor = fallbackTheme.Muted };
        time.SetBinding(Label.TextProperty, "Entry.TimeText");
        time.SetBinding(IsVisibleProperty, "Entry.ShowAccent");
        stack.Add(title);
        stack.Add(time);
        Grid.SetColumn(stack, 1);
        grid.Add(stack);

        return grid;
    }
}
