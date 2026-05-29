# Schedule widget — standalone iOS demo

A throwaway Xcode project that wraps the reusable widget Swift files (`../*.swift`) in a host app +
WidgetKit extension, so you can **see and run** the widget without the full app.

## Generate & open

```bash
brew install xcodegen          # once, if you don't have it
cd widgets/ios/demo
xcodegen generate
open ScheduleWidgetDemo.xcodeproj
```

Then either:

- **Preview (fastest):** open `ScheduleWidgetExample.swift` (it's in the extension target) and use the
  SwiftUI canvas preview (`ScheduleGrid_Previews`) — renders the grid with built-in sample data.
- **Run on simulator (with mock data):** select the **ScheduleWidgetExtension** scheme, Run, and pick
  a widget family; or run the **ScheduleWidgetDemoApp** scheme, then add the **Schedule** widget from
  the Home Screen gallery. The demo target defines `SCHEDULE_WIDGET_DEMO`, so the *running* widget
  shows bundled mock appointments when there's no shared data file — you get a populated grid on the
  simulator with no App Group setup.

## No XcodeGen?

Create a new iOS App in Xcode, **File ▸ New ▸ Target ▸ Widget Extension**, then drag these into the
extension target:

- `../WidgetAppointment.swift`, `../ScheduleWindow.swift`, `../ScheduleGridView.swift`,
  `../ScheduleWidgetExample.swift`
- `Widget/ScheduleWidgetBundle.swift` (the `@main` bundle)

## Mock data vs real data

This demo target builds with the `SCHEDULE_WIDGET_DEMO` flag, so:

- **Preview, placeholder, and the running widget** all show bundled sample appointments when no
  shared file is present — i.e. you see a populated grid on the simulator with zero setup.
- If you *do* add the **App Group** capability (`group.com.omnicasa.mobile`) and drop a real
  `Widgets/widget_appointments.json` into the container, the widget uses that instead.

The real `OmnicasaNative` extension should **omit** `SCHEDULE_WIDGET_DEMO`, so there the widget reads
only the shared file and an empty schedule renders empty (no fake data) — that fallback is demo-only.

## Files

| File | Role |
| --- | --- |
| `project.yml` | XcodeGen spec (host app + widget extension targets). |
| `App/ScheduleWidgetDemoApp.swift` | Minimal SwiftUI host app. |
| `App/Info.plist` | Host app Info.plist. |
| `Widget/ScheduleWidgetBundle.swift` | `@main` WidgetBundle exposing `ScheduleWidget`. |
| `Widget/Info.plist` | WidgetKit extension Info.plist. |

The four widget source files live one level up (`widgets/ios/`) and are shared verbatim with the real
`OmnicasaNative` extension — this demo just references them.
