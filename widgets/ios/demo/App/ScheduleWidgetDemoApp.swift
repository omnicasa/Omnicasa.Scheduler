//
//  ScheduleWidgetDemoApp.swift
//  Standalone host app for previewing / running the Schedule widget.
//

import SwiftUI

@main
struct ScheduleWidgetDemoApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
        }
    }
}

struct ContentView: View {
    var body: some View {
        VStack(spacing: 16) {
            Text("Schedule Widget Demo")
                .font(.title2).bold()
            Text("Add the **Schedule** widget to the Home Screen, or open `ScheduleWidgetExample.swift` "
                 + "in the widget target and use the SwiftUI **#Preview**.")
                .font(.callout)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .padding(.horizontal, 32)
        }
        .padding()
    }
}

#Preview {
    ContentView()
}
