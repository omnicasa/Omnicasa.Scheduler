using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>View-model backing <see cref="SchedulePage"/>. Exposes every <see cref="ScheduleView"/> binding target.</summary>
public class SchedulePageViewModel : INotifyPropertyChanged
{
    private static readonly IList<IPerson> AllPersonsList = new List<IPerson>
    {
        new Person { Id = "p1", Name = "Alice Murphy", Color = Color.FromArgb("#007AFF") },
        new Person { Id = "p2", Name = "Bob Reyes", Color = Color.FromArgb("#34C759") },
        new Person { Id = "p3", Name = "Charlie Mendes", Color = Color.FromArgb("#FF9500") },
    };

    private DateTime startDay = DateTime.Today;

    private DateTime endDay = DateTime.Today.AddDays(13);

    private int viewModeIndex = 2;

    private bool showPersons;

    private IList<IPerson>? persons;

    private bool showTyping;

    private ITypingScheduleItem? typingItem;

    /// <summary>Initializes a new instance of the <see cref="SchedulePageViewModel"/> class.</summary>
    public SchedulePageViewModel()
    {
        Items = MainPage.Source.AllItems;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Appointments fed to the schedule.</summary>
    public IReadOnlyList<Appointment> Items { get; }

    /// <summary>First day to render (inclusive).</summary>
    public DateTime StartDay
    {
        get => startDay;
        set
        {
            if (Set(ref startDay, value))
            {
                OnPropertyChanged(nameof(HeaderText));
            }
        }
    }

    /// <summary>Last day to render (inclusive).</summary>
    public DateTime EndDay
    {
        get => endDay;
        set
        {
            if (Set(ref endDay, value))
            {
                OnPropertyChanged(nameof(HeaderText));
            }
        }
    }

    /// <summary>Zero-based picker index (0..6). Bound directly to <see cref="Microsoft.Maui.Controls.Picker.SelectedIndex"/>.</summary>
    public int ViewModeIndex
    {
        get => viewModeIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, 6);
            if (Set(ref viewModeIndex, clamped))
            {
                OnPropertyChanged(nameof(ViewMode));
                OnPropertyChanged(nameof(HeaderText));
            }
        }
    }

    /// <summary>Effective <see cref="ScheduleView.ViewMode"/> (1..7).</summary>
    public int ViewMode => viewModeIndex + 1;

    /// <summary>Toggle for the persons column split. Bound to a <see cref="Microsoft.Maui.Controls.Switch"/>.</summary>
    public bool ShowPersons
    {
        get => showPersons;
        set
        {
            if (Set(ref showPersons, value))
            {
                Persons = value ? AllPersonsList : null;
            }
        }
    }

    /// <summary>Current persons list passed to <see cref="ScheduleView.Persons"/>; null when persons mode is off.</summary>
    public IList<IPerson>? Persons
    {
        get => persons;
        private set => Set(ref persons, value);
    }

    /// <summary>Toggle that creates / clears a draft block bound to <see cref="ScheduleView.TypingItem"/>.</summary>
    public bool ShowTyping
    {
        get => showTyping;
        set
        {
            if (Set(ref showTyping, value))
            {
                if (value)
                {
                    var anchorStart = DateTime.Today.AddHours(10);
                    TypingItem = new TypingItemModel
                    {
                        Id = "draft",
                        Title = "New event",
                        Start = anchorStart,
                        End = anchorStart.AddHours(1),
                        Color = Color.FromArgb("#5856D6"),
                    };
                }
                else
                {
                    TypingItem = null;
                }
            }
        }
    }

    /// <summary>Bound to <see cref="ScheduleView.TypingItem"/>.</summary>
    public ITypingScheduleItem? TypingItem
    {
        get => typingItem;
        private set => Set(ref typingItem, value);
    }

    /// <summary>Friendly date range label for the header (derived).</summary>
    public string HeaderText
    {
        get
        {
            var rangeStart = StartDay.Date;
            var rangeEnd = EndDay.Date;
            if (rangeEnd < rangeStart)
            {
                rangeEnd = rangeStart;
            }

            int rangeDays = Math.Max(1, (int)(rangeEnd - rangeStart).TotalDays + 1);
            int days = Math.Min(ViewMode, rangeDays);
            var lastVisible = rangeStart.AddDays(days - 1);
            return $"{rangeStart.ToString("MMM d", CultureInfo.CurrentCulture)} – {lastVisible.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}";
        }
    }

    /// <summary>
    /// Creates (or repositions) the draft block at the given time — e.g. from a long-tap on empty
    /// space. The time is snapped to the nearest 15 minutes and the draft spans one hour.
    /// </summary>
    /// <param name="when">Date and time of the long-tap (day + time-of-day).</param>
    public void CreateDraftAt(DateTime when)
    {
        const int quarter = 15;
        int minutes = (int)(Math.Round(when.TimeOfDay.TotalMinutes / quarter) * quarter);
        var start = when.Date.AddMinutes(minutes);
        var end = start.AddHours(1);
        if (end.Date > start.Date)
        {
            end = start.Date.AddDays(1);
        }

        TypingItem = new TypingItemModel
        {
            Id = "draft",
            Title = "New event",
            Start = start,
            End = end,
            Color = Color.FromArgb("#5856D6"),
        };

        // Keep the "Draft" toggle in sync without re-running its setter (which resets the time).
        if (!showTyping)
        {
            showTyping = true;
            OnPropertyChanged(nameof(ShowTyping));
        }
    }

    /// <summary>Clears the draft block (animates out via the bubble dismiss).</summary>
    public void DismissDraft()
    {
        TypingItem = null;
        if (showTyping)
        {
            showTyping = false;
            OnPropertyChanged(nameof(ShowTyping));
        }
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for the given property name.</summary>
    /// <param name="name">Property name; defaults to the caller.</param>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Backing-field setter that raises <see cref="PropertyChanged"/> on change.</summary>
    /// <typeparam name="T">Field type.</typeparam>
    /// <param name="field">Backing field.</param>
    /// <param name="value">New value.</param>
    /// <param name="name">Property name; defaults to the caller.</param>
    /// <returns>True when the value was different and the field was updated.</returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    /// <summary>Simple <see cref="ITypingScheduleItem"/> backing the demo draft block.</summary>
    public sealed class TypingItemModel : ITypingScheduleItem
    {
        private string id = string.Empty;

        private string? title;

        private DateTime start;

        private DateTime end;

        private bool isAllDay;

        private Color? color;

        private string? personId;

        private string? notes;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc />
        public string Id
        {
            get => id;
            set => Set(ref id, value);
        }

        /// <inheritdoc />
        public string? Title
        {
            get => title;
            set => Set(ref title, value);
        }

        /// <inheritdoc />
        public DateTime Start
        {
            get => start;
            set => Set(ref start, value);
        }

        /// <inheritdoc />
        public DateTime End
        {
            get => end;
            set => Set(ref end, value);
        }

        /// <inheritdoc />
        public bool IsAllDay
        {
            get => isAllDay;
            set => Set(ref isAllDay, value);
        }

        /// <inheritdoc />
        public Color? Color
        {
            get => color;
            set => Set(ref color, value);
        }

        /// <inheritdoc />
        public string? PersonId
        {
            get => personId;
            set => Set(ref personId, value);
        }

        /// <inheritdoc />
        public string? Notes
        {
            get => notes;
            set => Set(ref notes, value);
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
