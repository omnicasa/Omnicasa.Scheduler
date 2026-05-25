- Required feature

- A main control
  - ScheduleView
    - Support binding start-day, end-day to scope draw
    - Support binding view mode (1 day, 2 day, 3 day, 4 day, 5 day, 6 day, 7 day)
      - If range of start-end lower than view mode eg: 3,4,5 days, dont draw day not in range
    - Support binding theme, but theme object contains all (font size, colors..)
    - Support multiple persons
    - The first version have fixed size of hour, not support scale
    - No need YearCalendar or Month calendar at this moment, i want a simple version first for initialize
    - Support binding items-source, item souce should be define the interface for appointment obj, so we can inherit for future or make this an library

- Behaviors
  - Support zoom and scale
  - Support tap on scheduleview
    - Tap on scheduleview (empty space)  (eventarg is date time)
    - Tap on appointment on scheduleview (eventarg is appointment)
  - Suport long tap on scheduleview
    - Long tap on scheduleView (empty space)(eventarg is date time)
    - Long tap on appointment on scheduleview (eventarg is appointment)
  -

- I want to support a new feature like TypingScheduleItem
  - This is bindable property
  - The property look like IScheduleItem
  - If if set and not null
    - Showing a highlight appoiontmebnt, have shadow, can hold it and move anywhere in grid
    - If hold top and bottom corner, can resize the height, also update start/end date
