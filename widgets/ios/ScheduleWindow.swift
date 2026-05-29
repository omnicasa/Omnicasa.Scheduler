//
//  ScheduleWindow.swift
//  Schedule widget — iOS
//
//  Pure layout logic (no SwiftUI/UIKit drawing): picks the dynamic time window
//  around "now" and packs overlapping appointments into columns, mirroring the
//  MAUI ScheduleView's ScheduleLayout. Kept separate so it's easy to reason about.
//

import Foundation
import SwiftUI

/// Colors matching the MAUI `ScheduleViewTheme` defaults, so the widget looks like the control.
enum ScheduleWidgetTheme {
    static let background = Color.white
    static let foreground = Color.black
    static let muted = Color(hex: "#8E8E93")!
    static let accent = Color(hex: "#FF3B30")!
    static let gridLine = Color(hex: "#E5E5EA")!
    static let today = Color(hex: "#FF3B30")!
}

/// An appointment placed within an overlap group: its column and the group's column count.
struct LaidOutWidgetItem {
    let appointment: WidgetAppointment
    let start: Date
    let end: Date
    let column: Int
    let columnsInGroup: Int
}

/// The computed time window plus the appointments laid out within it.
struct ScheduleWindow {
    /// First minute-of-day shown at the top of the grid (0...1440).
    let startMinutes: Int

    /// Number of minutes the window spans (e.g. 6h = 360).
    let spanMinutes: Int

    /// Items intersecting the window, with overlap columns assigned.
    let items: [LaidOutWidgetItem]

    /// "now" minute-of-day if today is in view, else nil (no current-time line).
    let nowMinutes: Int?

    var endMinutes: Int { startMinutes + spanMinutes }

    /// Builds the window for a given day.
    /// - Parameters:
    ///   - appointments: all shared appointments (filtered to `day` and to timed items here).
    ///   - day: the calendar day to show.
    ///   - now: current time (used to anchor the window and the now-line).
    ///   - windowHours: how many hours the grid spans.
    ///   - calendar: calendar for date math.
    static func build(
        appointments: [WidgetAppointment],
        day: Date,
        now: Date,
        windowHours: Int = 6,
        calendar: Calendar = .current
    ) -> ScheduleWindow {
        let dayStart = calendar.startOfDay(for: day)
        let dayEnd = calendar.date(byAdding: .day, value: 1, to: dayStart)!
        let span = max(1, windowHours) * 60

        // Timed appointments intersecting the day (drop all-day; the widget grid is time-based).
        let dayItems: [(WidgetAppointment, Date, Date)] = appointments.compactMap { appt in
            guard let s = appt.start, let e = appt.end, e > s else { return nil }
            guard s < dayEnd && e > dayStart else { return nil }
            return (appt, s, e)
        }

        let isToday = calendar.isDate(now, inSameDayAs: dayStart)
        let nowMin = isToday ? minuteOfDay(now, dayStart: dayStart, calendar: calendar) : nil

        // Anchor: one hour of lead-in before "now" (or the first event when not today),
        // floored to the hour and clamped so the window fits inside the day.
        let anchorSource: Int
        if let nowMin {
            anchorSource = nowMin - 60
        } else if let firstStart = dayItems.map({ minuteOfDay($0.1, dayStart: dayStart, calendar: calendar) }).min() {
            anchorSource = firstStart - 30
        } else {
            anchorSource = 8 * 60   // empty, not today: show the morning
        }

        var start = (anchorSource / 60) * 60                 // floor to the hour
        start = max(0, min(start, (24 * 60) - span))         // clamp into the day

        let windowStart = start
        let windowEnd = start + span

        // Items intersecting the window, sorted for greedy column packing.
        var visible: [(appt: WidgetAppointment, startM: Int, endM: Int)] = []
        for (appt, s, e) in dayItems {
            let sm = minuteOfDay(s, dayStart: dayStart, calendar: calendar)
            let em = minuteOfDay(e, dayStart: dayStart, calendar: calendar)
            if sm < windowEnd && em > windowStart {
                visible.append((appt, sm, em))
            }
        }
        visible.sort { lhs, rhs in
            lhs.startM != rhs.startM ? lhs.startM < rhs.startM : lhs.endM < rhs.endM
        }

        let laid = packColumns(visible, dayStart: dayStart, calendar: calendar)
        return ScheduleWindow(startMinutes: windowStart, spanMinutes: span, items: laid, nowMinutes: nowMin)
    }

    // Greedy overlap-column packing (matches ScheduleLayout in the MAUI control).
    private static func packColumns(
        _ items: [(WidgetAppointment, Int, Int)],
        dayStart: Date,
        calendar: Calendar
    ) -> [LaidOutWidgetItem] {
        var result: [LaidOutWidgetItem] = []
        var group: [(appt: WidgetAppointment, startM: Int, endM: Int, column: Int)] = []
        var groupEnd: Int? = nil

        func flush() {
            guard !group.isEmpty else { return }
            let cols = (group.map { $0.column }.max() ?? 0) + 1
            for g in group {
                result.append(LaidOutWidgetItem(
                    appointment: g.appt,
                    start: calendar.date(byAdding: .minute, value: g.startM, to: dayStart)!,
                    end: calendar.date(byAdding: .minute, value: g.endM, to: dayStart)!,
                    column: g.column,
                    columnsInGroup: cols))
            }
            group.removeAll()
            groupEnd = nil
        }

        for (appt, startM, endM) in items {
            if let ge = groupEnd, startM >= ge {
                flush()
            }

            var used = Set<Int>()
            for g in group where g.endM > startM {
                used.insert(g.column)
            }

            var c = 0
            while used.contains(c) { c += 1 }

            group.append((appt, startM, endM, c))
            groupEnd = max(groupEnd ?? endM, endM)
        }
        flush()
        return result
    }

    private static func minuteOfDay(_ date: Date, dayStart: Date, calendar: Calendar) -> Int {
        Int(date.timeIntervalSince(dayStart) / 60.0)
    }
}
