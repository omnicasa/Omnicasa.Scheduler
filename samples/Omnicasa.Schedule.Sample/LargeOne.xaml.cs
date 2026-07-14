using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;
using SkiaSharp;

namespace Omnicasa.Schedule.Sample;

/// <summary>Test bench for <see cref="InfiniteScheduleView"/> — exercises every wired feature.</summary>
public partial class LargeOne : ContentPage
{
    private static readonly IList<IPerson> DemoPersons = new List<IPerson>
    {
        new Person { Id = "p1", Name = "Alice Murphy", Color = Color.FromArgb("#007AFF") },
        new Person { Id = "p2", Name = "Bob Reyes", Color = Color.FromArgb("#34C759") },
        new Person { Id = "p3", Name = "Charlie Mendes", Color = Color.FromArgb("#FF9500") },
    };

    /// <summary>Initializes a new instance of the <see cref="LargeOne"/> class.</summary>
    public LargeOne()
    {
        InitializeComponent();

        Schedule.ItemsSource = MainPage.Source.AllItems;

        Schedule.ItemTapped += (_, e) => Show($"ItemTapped: {e.Item.Title}");
        Schedule.Tapped += (_, e) => Show($"Tapped empty: {e.When:ddd d MMM HH:mm}");

        // Long-press empty space → drop a new draft (TypingItem) there; drag to place/size it.
        Schedule.LongTapped += (_, e) =>
        {
            var raw = e.When;
            var start = new DateTime(raw.Year, raw.Month, raw.Day, raw.Hour, raw.Minute / 15 * 15, 0);
            Schedule.TypingItem = new SchedulePageViewModel.TypingItemModel
            {
                Id = "typing-new",
                Title = "New event",
                Start = start,
                End = start.AddHours(1),
                Color = Color.FromArgb("#5856D6"),
            };
            Show($"Draft at {start:ddd d MMM HH:mm} — drag body to move, corners to resize.");
        };

        // Long-press an appointment → native quick-action menu (UIMenu on iOS, PopupMenu on Android).
        Schedule.ItemActionsProvider = _ => new[]
        {
            new ScheduleMenuAction("Edit", "pencil"),
            new ScheduleMenuAction("Move", "arrow.up.and.down.and.arrow.left.and.right"),
            new ScheduleMenuAction("Delete", "trash", isDestructive: true),
        };

        Schedule.ItemActionInvoked += (_, e) =>
        {
            Show($"{e.Action} → {e.Item.Title}");

            // "Move" picks the item up as a draggable holding block.
            if (e.Action == "Move")
            {
                Schedule.HoldingSchedule = e.Item;
            }
        };

        // Fallback long-tap (fires only when an item has no menu actions).
        Schedule.ItemLongTapped += (_, e) => Show($"ItemLongTapped: {e.Item.Title}");

        // Drop commits the move: the control is event-only (like ScheduleView), so we apply the new
        // time to the item, clear the holding block, and refresh so the grid shows it moved.
        // Follow the visible day window in the page title as you scroll / page.
        Schedule.VisibleRangeChanged += (_, e) =>
            Title = e.FirstDay == e.LastDay
                ? e.FirstDay.ToString("ddd d MMM yyyy")
                : $"{e.FirstDay:ddd d MMM} – {e.LastDay:ddd d MMM yyyy}";

        Schedule.HoldingDropped += (_, e) =>
        {
            if (e.Item is Appointment appt)
            {
                appt.Start = e.Start;
                appt.End = e.End;
                if (e.PersonId is not null)
                {
                    appt.PersonId = e.PersonId;
                }
            }

            Schedule.HoldingSchedule = null;
            Schedule.RefreshItems();
            Show($"Moved '{e.Item.Title}' to {e.Start:ddd d MMM HH:mm}");
        };
    }

    private void Show(string message) => Status.Text = message;

    private void OnDay(object? sender, EventArgs e) => Schedule.ViewMode = 1;

    private void OnThreeDay(object? sender, EventArgs e) => Schedule.ViewMode = 3;

    private void OnWeek(object? sender, EventArgs e) => Schedule.ViewMode = 7;

    private async void OnScroll8(object? sender, EventArgs e)
        => await Schedule.ScrollToTimeAsync(TimeSpan.FromHours(8), animated: true);

    private async void OnScroll18(object? sender, EventArgs e)
        => await Schedule.ScrollToTimeAsync(TimeSpan.FromHours(18), animated: true);

    private void OnPersonsToggled(object? sender, ToggledEventArgs e)
        => Schedule.Persons = e.Value ? DemoPersons : null;

    private void OnBoundToggled(object? sender, ToggledEventArgs e)
    {
        Schedule.MinDay = e.Value ? DateTime.Today.AddDays(-14) : null;
        Schedule.MaxDay = e.Value ? DateTime.Today.AddDays(14) : null;
    }

    private void OnGainChanged(object? sender, ValueChangedEventArgs e)
        => Schedule.FlingGain = e.NewValue;

    private void OnGlideChanged(object? sender, ValueChangedEventArgs e)
        => Schedule.FlingDecelerationTime = e.NewValue;

    private void OnTypingToggled(object? sender, ToggledEventArgs e)
    {
        if (!e.Value)
        {
            Schedule.TypingItem = null;
            return;
        }

        var start = DateTime.Today.AddHours(10);
        Schedule.TypingItem = new SchedulePageViewModel.TypingItemModel
        {
            Id = "typing",
            Title = "Draft",
            Start = start,
            End = start.AddHours(1),
            Color = Color.FromArgb("#5856D6"),
        };
        Show("Typing draft added — drag its body to move, corners to resize.");
    }

    private void OnHoldingToggled(object? sender, ToggledEventArgs e)
    {
        if (!e.Value)
        {
            Schedule.HoldingSchedule = null;
            return;
        }

        var start = DateTime.Today.AddHours(13);
        Schedule.HoldingSchedule = new Appointment
        {
            Id = "holding",
            Title = "Hold me",
            Start = start,
            End = start.AddHours(1),
            Color = Color.FromArgb("#FF2D55"),
        };
        Show("Holding block added — drag it; release fires HoldingDropped.");
    }

    private void OnCustomToggled(object? sender, ToggledEventArgs e)
    {
        Schedule.SkiaRenderer = e.Value ? new StripeRenderer() : null;
        Show(e.Value ? "Custom renderer on (left-accent style)." : "Default renderer.");
    }

    private void OnPagingToggled(object? sender, ToggledEventArgs e)
    {
        Schedule.PagingEnabled = e.Value;
        Show(e.Value ? "Paging on — swipe snaps one page." : "Paging off — free momentum scroll.");
    }

    // Demo custom look: soft-tinted block with a colored left accent bar and colored title.
    private sealed class StripeRenderer : InfiniteScheduleRenderer
    {
        public override void DrawAppointment(SKCanvas canvas, InfiniteScheduleBlock block)
        {
            using var bg = new SKPaint { Color = block.Color.WithAlpha(38), IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawRoundRect(block.Rect, 8, 8, bg);

            using var bar = new SKPaint { Color = block.Color, IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawRoundRect(new SKRect(block.Rect.Left, block.Rect.Top, block.Rect.Left + 4, block.Rect.Bottom), 2, 2, bar);

            if (!string.IsNullOrEmpty(block.Title))
            {
                float size = (float)block.Theme.BlockTitleFontSize;
                using var text = new SKPaint { Color = block.Color, IsAntialias = true };
                using var font = new SKFont { Size = size };
                canvas.Save();
                canvas.ClipRect(block.Rect);
                canvas.DrawText(block.Title, block.Rect.Left + 10, block.Rect.Top + size + 4, SKTextAlign.Left, font, text);
                canvas.Restore();
            }
        }
    }
}
