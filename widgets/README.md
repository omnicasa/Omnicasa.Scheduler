# Schedule widget (native)

Native home-screen / WidgetKit renderings of `ScheduleView`'s **day time-grid**, for integration
into a host app's widget extension (e.g. `Omnicasa.Mobile`). These files are **reference
deliverables** — they live outside the `src/` library and are not part of the NuGet build. Copy
them into the target project and wire them up.

> Why native and not the MAUI control? Widgets can't host a live, scrolling MAUI view. iOS widgets
> are SwiftUI-only; Android widgets are RemoteViews (no custom view). So the grid is rendered as a
> **static snapshot** — iOS with SwiftUI `Canvas`, Android with a `Canvas`-drawn `Bitmap` shown in
> an `ImageView`. Both reproduce the `ScheduleView` look (hour grid + left rail, soft-fill blocks
> with an accent strip + title/time, current-time line).

## Behavior

- **Dynamic window around now** — instead of a full 24h, the grid shows ~6 hours (`windowHours`,
  default 6) starting one hour before the current time, floored to the hour and clamped into the
  day. When the day isn't today it anchors on the first event. The window keeps "now" and the
  upcoming events in frame, and slides as time passes (refresh every ~15 min).
- **Overlapping appointments** are packed into columns with the same greedy algorithm as the MAUI
  control (`ScheduleLayout`), so side-by-side meetings render the same way.
- **All-day / cross-date items are skipped** here (the grid is time-based) — show those in the
  existing agenda widget, or add a separate band if you want.

## Shared data contract

Both platforms read the **same JSON the app already writes** for the agenda widget — no app changes
needed:

- App Group `group.com.omnicasa.mobile`, file `Widgets/widget_appointments.json`
- Shape: `{ "Time": ..., "Appointments": [ WidgetAppointment, ... ] }`
- `WidgetAppointment`: `AppointmentId`, `Title`, `SubTitle`, `StartTime`/`EndTime`
  (`"yyyy-MM-dd HH:mm:ss"`), `BackgroundColor` (`#RRGGBB`), … (matches the existing model)

## iOS (`ios/`)

| File | Role |
| --- | --- |
| `WidgetAppointment.swift` | `Codable` matching the shared JSON (same `CodingKeys`) + hex `Color` + date parsing. |
| `ScheduleWindow.swift` | Theme colors + pure window/column-layout logic. |
| `ScheduleGridView.swift` | SwiftUI `Canvas` that draws the grid, blocks, rail and now-line. |
| `ScheduleWidgetExample.swift` | A complete `Widget` + `TimelineProvider` + sample data (the example). |

**Integrate:** drop the first three files into the existing WidgetKit extension
(`source/SwiftExtension/OmnicasaNative/…`). Either register `ScheduleWidget()` from the example in
your `@main WidgetBundle`, or just use `ScheduleGridView(appointments:day:now:)` inside your own
widget entry view. The example reads the App Group container exactly like `WidgetService`.

```swift
ScheduleGridView(appointments: entry.appointments, day: entry.date, now: entry.date)
```

Supported families in the example: `.systemLarge`, `.systemExtraLarge` (the grid needs height).

## Android (`android/`)

| File | Role |
| --- | --- |
| `WidgetAppointment.cs` | Model + `System.Text.Json` matching the shared JSON; `Start`/`End` parsing. |
| `ScheduleWindow.cs` | Pure window/column-layout logic (mirror of the Swift). |
| `ScheduleWidgetRenderer.cs` | `Android.Graphics.Canvas` → `Bitmap` (the grid look). |
| `ScheduleWidgetProvider.cs` | Example `AppWidgetProvider` (RemoteViews + `SetImageViewBitmap`). |
| `res/layout/schedule_widget_layout.xml` | Single `ImageView` host. |
| `res/xml/schedule_widget_info.xml` | `AppWidgetProviderInfo` (large, resizable). |

**Integrate:** copy the `.cs` into `Platforms/Android/Widget/`, the `res/` files into
`Platforms/Android/Resources/layout` and `…/xml`, and register the receiver in `AndroidManifest.xml`.
Matches the existing RemoteViews widget stack (C#, not Glance). Point `LoadShared` at wherever the
app writes the shared file (the agenda widget uses the same `Widgets/widget_appointments.json`), and
refresh by broadcasting `APPWIDGET_UPDATE` like the other widgets.

```csharp
var bmp = ScheduleWidgetRenderer.Render(appointments, widthPx, heightPx, DateTime.Now, density);
views.SetImageViewBitmap(Resource.Id.schedule_image, bmp);
```

## Testing

Three tiers, fastest first:

1. **Logic (runs in this repo now).** The window + overlap-column math is pure C# and is linked into
   the library test project (`tests/Omnicasa.Schedule.Tests`, class `ScheduleWindowTests`) — run
   `dotnet test`. Covers date parsing, the shared-JSON contract, the dynamic window
   (lead-in / clamping / now-line) and overlap columns.

2. **Visual, no widget host.**
   - **iOS:** the standalone demo under `ios/demo/` (`xcodegen generate` → SwiftUI `#Preview`) renders
     the grid with sample data — see `ios/demo/README.md`.
   - **Android:** the MAUI sample (`samples/Omnicasa.Schedule.Sample`) has a **Widget** toolbar button
     → `WidgetPreviewPage`, which calls `ScheduleWidgetRenderer` (the real widget code) and shows the
     Bitmap in an `Image`. Run the sample on an Android device/emulator:
     `dotnet build samples/Omnicasa.Schedule.Sample -f net9.0-android -t:Run`.

3. **Real widget on device.** Drop the files into the host app's WidgetKit extension (iOS) /
   `Platforms/Android/Widget` + manifest receiver (Android) and add the widget from the
   home-screen gallery. See the per-platform sections above.

## Keeping it faithful to `ScheduleView`

The window + column-packing logic is intentionally a 1:1 port of the library's `ScheduleLayout`, and
the colors match `ScheduleViewTheme` defaults (`#FF3B30` accent/today, `#8E8E93` muted, `#E5E5EA`
grid). If you restyle the control's renderer, mirror the change in `ScheduleGridView.swift` /
`ScheduleWidgetRenderer.cs`.
