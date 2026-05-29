//
//  WidgetAppointment.swift
//  Schedule widget — iOS
//
//  Drop-in compatible with the shared widget JSON written by the MAUI app
//  (App Group "group.com.omnicasa.mobile", file Widgets/widget_appointments.json).
//  The CodingKeys and date format match the existing agenda widget contract.
//

import Foundation
import SwiftUI

/// One appointment as serialized by the app into the shared widget JSON.
struct WidgetAppointment: Codable, Identifiable {
    var id: Double

    let title: String?
    let subTitle: String?
    let startTime: String   // "yyyy-MM-dd HH:mm:ss"
    let endTime: String     // "yyyy-MM-dd HH:mm:ss"
    let isSuperAppointment: Bool
    let isGroupAppointment: Bool
    let isPrivate: Bool
    let isRecurrence: Bool
    let isRecurrenceException: Bool
    let backgroundColor: String?   // hex "#RRGGBB"
    let place: String?
    let imageKey: String?
    let isCommented: Bool

    enum CodingKeys: String, CodingKey {
        case id = "AppointmentId"
        case title = "Title"
        case subTitle = "SubTitle"
        case startTime = "StartTime"
        case endTime = "EndTime"
        case isSuperAppointment = "IsSuperAppointment"
        case isGroupAppointment = "IsGroupAppointment"
        case isPrivate = "IsPrivate"
        case isRecurrence = "IsRecurrence"
        case isRecurrenceException = "IsRecurrenceException"
        case imageKey = "ImageKey"
        case backgroundColor = "BackgroundColor"
        case place = "Place"
        case isCommented = "IsCommented"
    }
}

/// The shared file wrapper the app writes (`{ "Time": ..., "Appointments": [...] }`).
struct WidgetAppointmentShare: Codable {
    let time: String?
    let appointments: [WidgetAppointment]

    enum CodingKeys: String, CodingKey {
        case time = "Time"
        case appointments = "Appointments"
    }
}

extension WidgetAppointment {
    /// Parsed start, or nil if the string is malformed.
    var start: Date? { WidgetDate.parse(startTime) }

    /// Parsed end, or nil if the string is malformed.
    var end: Date? { WidgetDate.parse(endTime) }

    /// Resolved block color (hex, else a neutral accent).
    var color: Color { Color(hex: backgroundColor) ?? ScheduleWidgetTheme.accent }
}

/// Shared date parsing for the widget transport format.
enum WidgetDate {
    static let format = "yyyy-MM-dd HH:mm:ss"

    private static let formatter: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = format
        f.locale = Locale(identifier: "en_US_POSIX")
        return f
    }()

    static func parse(_ value: String) -> Date? { formatter.date(from: value) }
}

extension Color {
    /// Parses "#RGB", "#RRGGBB" or "#AARRGGBB"; returns nil when empty/invalid.
    init?(hex: String?) {
        guard var s = hex?.trimmingCharacters(in: .whitespaces), !s.isEmpty else { return nil }
        if s.hasPrefix("#") { s.removeFirst() }
        var value: UInt64 = 0
        guard Scanner(string: s).scanHexInt64(&value) else { return nil }

        let r, g, b, a: Double
        switch s.count {
        case 6:
            r = Double((value >> 16) & 0xFF) / 255
            g = Double((value >> 8) & 0xFF) / 255
            b = Double(value & 0xFF) / 255
            a = 1
        case 8:
            a = Double((value >> 24) & 0xFF) / 255
            r = Double((value >> 16) & 0xFF) / 255
            g = Double((value >> 8) & 0xFF) / 255
            b = Double(value & 0xFF) / 255
        case 3:
            r = Double((value >> 8) & 0xF) / 15
            g = Double((value >> 4) & 0xF) / 15
            b = Double(value & 0xF) / 15
            a = 1
        default:
            return nil
        }
        self.init(.sRGB, red: r, green: g, blue: b, opacity: a)
    }
}
