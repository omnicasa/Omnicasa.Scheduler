//
//  ScheduleGridView.swift
//  Schedule widget — iOS
//
//  SwiftUI rendering of the ScheduleView day time-grid for a WidgetKit widget.
//  Static snapshot (widgets can't scroll): draws a dynamic window of hours with
//  hour lines, a left time rail, the current-time line, and appointment blocks
//  styled like the MAUI ScheduleView (soft fill + accent strip + title + range).
//

import SwiftUI
import WidgetKit

struct ScheduleGridView: View {
    let appointments: [WidgetAppointment]
    var day: Date = Date()
    var now: Date = Date()
    var windowHours: Int = 6

    private let railWidth: CGFloat = 44

    var body: some View {
        let window = ScheduleWindow.build(
            appointments: appointments,
            day: day,
            now: now,
            windowHours: windowHours)

        GeometryReader { geo in
            ZStack(alignment: .topLeading) {
                ScheduleWidgetTheme.background

                Canvas { context, size in
                    draw(window: window, in: context, size: size)
                }
            }
        }
    }

    private func draw(window: ScheduleWindow, in context: GraphicsContext, size: CGSize) {
        let contentX = railWidth
        let contentW = size.width - contentX
        guard contentW > 0, size.height > 0 else { return }

        let span = CGFloat(window.spanMinutes)
        func y(forMinute m: Int) -> CGFloat {
            (CGFloat(m - window.startMinutes) / span) * size.height
        }

        // Hour grid lines + left-rail labels.
        let firstHour = window.startMinutes / 60
        let lastHour = window.endMinutes / 60
        for hour in firstHour...lastHour {
            let yy = y(forMinute: hour * 60)
            var line = Path()
            line.move(to: CGPoint(x: contentX, y: yy))
            line.addLine(to: CGPoint(x: size.width, y: yy))
            context.stroke(line, with: .color(ScheduleWidgetTheme.gridLine), lineWidth: 0.5)

            if hour < 24 {
                let label = Self.hourLabel(hour)
                let text = Text(label).font(.system(size: 10)).foregroundColor(ScheduleWidgetTheme.muted)
                context.draw(text, at: CGPoint(x: contentX - 6, y: yy + 8), anchor: .trailing)
            }
        }

        // Appointment blocks.
        for item in window.items {
            drawBlock(item, in: context, contentX: contentX, contentW: contentW, size: size, y: y)
        }

        // Current-time line.
        if let nowMin = window.nowMinutes, nowMin >= window.startMinutes, nowMin <= window.endMinutes {
            let yy = y(forMinute: nowMin)
            var dot = Path(ellipseIn: CGRect(x: contentX - 3, y: yy - 3, width: 6, height: 6))
            context.fill(dot, with: .color(ScheduleWidgetTheme.today))
            var line = Path()
            line.move(to: CGPoint(x: contentX, y: yy))
            line.addLine(to: CGPoint(x: size.width, y: yy))
            context.stroke(line, with: .color(ScheduleWidgetTheme.today), lineWidth: 1.5)
        }
    }

    private func drawBlock(
        _ item: LaidOutWidgetItem,
        in context: GraphicsContext,
        contentX: CGFloat,
        contentW: CGFloat,
        size: CGSize,
        y: (Int) -> CGFloat
    ) {
        let startM = minuteOfDay(item.start)
        let endM = minuteOfDay(item.end)
        let y1 = max(0, y(startM))
        let y2 = min(size.height, y(endM))

        let slotW = contentW / CGFloat(max(1, item.columnsInGroup))
        let x = contentX + (CGFloat(item.column) * slotW) + 2
        let w = slotW - 4
        let h = max(y2 - y1, 16)
        let rect = CGRect(x: x, y: y1, width: w, height: h)

        let bg = item.appointment.color
        let soft = bg.opacity(0.18)
        let rounded = Path(roundedRect: rect, cornerRadius: 6)
        context.fill(rounded, with: .color(soft))

        // Accent strip on the leading edge.
        let strip = Path(roundedRect: CGRect(x: x, y: y1, width: 3, height: h), cornerRadius: 1.5)
        context.fill(strip, with: .color(bg))

        // Title (dark variant of the block color) + time range, if there's room.
        let titleColor = bg.opacity(0.95)
        let title = item.appointment.title ?? ""
        if h >= 16 {
            let t = Text(title).font(.system(size: 11, weight: .semibold)).foregroundColor(titleColor)
            context.draw(t, at: CGPoint(x: x + 8, y: y1 + 4), anchor: .topLeading)
        }
        if h >= 32 {
            let range = "\(Self.timeLabel(item.start)) – \(Self.timeLabel(item.end))"
            let r = Text(range).font(.system(size: 9)).foregroundColor(ScheduleWidgetTheme.muted)
            context.draw(r, at: CGPoint(x: x + 8, y: y1 + 18), anchor: .topLeading)
        }
    }

    // Minute-of-day for a date, relative to the window's day.
    private func minuteOfDay(_ date: Date) -> Int {
        let cal = Calendar.current
        let dayStart = cal.startOfDay(for: day)
        return Int(date.timeIntervalSince(dayStart) / 60.0)
    }

    private static func hourLabel(_ hour: Int) -> String {
        switch hour {
        case 0: return "12 AM"
        case 12: return "12 PM"
        case let h where h < 12: return "\(h) AM"
        default: return "\(hour - 12) PM"
        }
    }

    private static func timeLabel(_ date: Date) -> String {
        let f = DateFormatter()
        f.dateFormat = Calendar.current.component(.minute, from: date) == 0 ? "h a" : "h:mm a"
        return f.string(from: date).lowercased()
    }
}
