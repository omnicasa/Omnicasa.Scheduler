//
//  ScheduleWidgetExample.swift
//  Schedule widget — iOS (EXAMPLE)
//
//  A complete, self-contained WidgetKit widget showing the schedule time-grid.
//  Reads the same shared JSON the app already writes (App Group + Widgets/widget_appointments.json).
//  Drop ScheduleGridView/ScheduleWindow/WidgetAppointment into your existing extension and
//  register `ScheduleWidget()` in your @main WidgetBundle (or reuse your existing bundle).
//

import SwiftUI
import WidgetKit

// MARK: - Timeline

struct ScheduleEntry: TimelineEntry {
    let date: Date
    let appointments: [WidgetAppointment]
}

struct ScheduleProvider: TimelineProvider {
    /// App Group + relative path the MAUI app writes to. Adjust if your group id differs.
    static let appGroup = "group.com.omnicasa.mobile"
    static let relativePath = "Widgets/widget_appointments.json"

    func placeholder(in context: Context) -> ScheduleEntry {
        ScheduleEntry(date: Date(), appointments: ScheduleSampleData.items)
    }

    func getSnapshot(in context: Context, completion: @escaping (ScheduleEntry) -> Void) {
        let items = context.isPreview ? ScheduleSampleData.items : Self.resolved()
        completion(ScheduleEntry(date: Date(), appointments: items))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<ScheduleEntry>) -> Void) {
        let entry = ScheduleEntry(date: Date(), appointments: Self.resolved())
        // Refresh every 15 minutes so the dynamic window + now-line stay current.
        let next = Calendar.current.date(byAdding: .minute, value: 15, to: Date()) ?? Date().addingTimeInterval(900)
        completion(Timeline(entries: [entry], policy: .after(next)))
    }

    /// Appointments for the running widget. In the demo build (SCHEDULE_WIDGET_DEMO) it falls back to
    /// bundled sample data when the App Group file is absent, so it shows something on the simulator.
    /// In a normal build it returns exactly what's shared (empty stays empty).
    static func resolved() -> [WidgetAppointment] {
        let shared = loadShared()
#if SCHEDULE_WIDGET_DEMO
        return shared.isEmpty ? ScheduleSampleData.items : shared
#else
        return shared
#endif
    }

    /// Reads the shared JSON from the App Group container; empty list on any failure.
    static func loadShared() -> [WidgetAppointment] {
        guard let dir = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroup) else {
            return []
        }
        let url = dir.appendingPathComponent(relativePath)
        guard let data = try? Data(contentsOf: url),
              let share = try? JSONDecoder().decode(WidgetAppointmentShare.self, from: data) else {
            return []
        }
        return share.appointments
    }
}

// MARK: - Widget

struct ScheduleWidgetEntryView: View {
    var entry: ScheduleEntry

    var body: some View {
        let grid = ScheduleGridView(appointments: entry.appointments, day: entry.date, now: entry.date)
        if #available(iOS 17.0, *) {
            grid.containerBackground(ScheduleWidgetTheme.background, for: .widget)
        } else {
            grid.background(ScheduleWidgetTheme.background)
        }
    }
}

struct ScheduleWidget: Widget {
    let kind = "OmnicasaScheduleWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: ScheduleProvider()) { entry in
            ScheduleWidgetEntryView(entry: entry)
        }
        .configurationDisplayName("Schedule")
        .description("Your day at a glance, on a time grid.")
        .supportedFamilies([.systemLarge, .systemExtraLarge])
    }
}

// MARK: - Sample data (previews / placeholder)

enum ScheduleSampleData {
    static var items: [WidgetAppointment] {
        let day = Calendar.current.startOfDay(for: Date())
        func format(_ d: Date) -> String {
            let f = DateFormatter()
            f.dateFormat = WidgetDate.format
            f.locale = Locale(identifier: "en_US_POSIX")
            return f.string(from: d)
        }
        func at(_ h: Int, _ m: Int) -> String {
            format(Calendar.current.date(byAdding: .minute, value: h * 60 + m, to: day)!)
        }
        func make(_ id: Double, _ title: String, _ s: (Int, Int), _ e: (Int, Int), _ color: String) -> WidgetAppointment {
            WidgetAppointment(
                id: id, title: title, subTitle: nil,
                startTime: at(s.0, s.1), endTime: at(e.0, e.1),
                isSuperAppointment: false, isGroupAppointment: false, isPrivate: false,
                isRecurrence: false, isRecurrenceException: false,
                backgroundColor: color, place: nil, imageKey: nil, isCommented: false)
        }
        let nowH = Calendar.current.component(.hour, from: Date())
        return [
            make(1, "Standup", (nowH, 0), (nowH, 30), "#007AFF"),
            make(2, "Design review", (nowH + 1, 0), (nowH + 2, 0), "#FF9500"),
            make(3, "Lunch", (nowH + 2, 30), (nowH + 3, 30), "#34C759"),
            make(4, "1:1 with Alex", (nowH + 1, 30), (nowH + 2, 15), "#5856D6"),
        ]
    }
}

// SwiftUI canvas preview of the grid. Uses the legacy PreviewProvider so the file stays compatible
// with iOS 16 extensions (the `#Preview(as:)` widget macro requires iOS 17).
struct ScheduleGrid_Previews: PreviewProvider {
    static var previews: some View {
        ScheduleGridView(appointments: ScheduleSampleData.items)
            .frame(width: 360, height: 380)
            .previewLayout(.sizeThatFits)
    }
}
