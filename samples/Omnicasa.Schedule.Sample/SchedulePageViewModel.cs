using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;
using Omnicasa.Schedule;

namespace Omnicasa.Schedule.Sample;

/// <summary>View-model backing <see cref="SchedulePage"/>. Exposes every <see cref="ScheduleView"/> binding target.</summary>
public class SchedulePageViewModel : INotifyPropertyChanged
{
    private static readonly IList<Person> AllPersonsList = new List<Person>
    {
        new Person { Id = "p1", Name = "Alice Murphy", Color = Color.FromArgb("#007AFF") },
        new Person { Id = "p2", Name = "Bob Reyes", Color = Color.FromArgb("#34C759") },
        new Person { Id = "p3", Name = "Charlie Mendes", Color = Color.FromArgb("#FF9500") },
    };

    private DateTime startDay = DateTime.Today;

    private DateTime endDay = DateTime.Today.AddDays(13);

    private int viewModeIndex = 2;

    private bool showPersons;

    private IList<Person>? persons;

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
    public IList<Person>? Persons
    {
        get => persons;
        private set => Set(ref persons, value);
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
}
