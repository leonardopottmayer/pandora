import dayjs, { type Dayjs } from 'dayjs'
import utc from 'dayjs/plugin/utc'
import timezone from 'dayjs/plugin/timezone'

dayjs.extend(utc)
dayjs.extend(timezone)

/**
 * Today in the account zone (the Identity preference, not the device's), as a plain Dayjs at midnight —
 * the value date pickers and date math expect. A device in Lisbon at 01:00 is still on yesterday in
 * São Paulo.
 */
export function todayIn(timeZone: string): Dayjs {
  return dayjs(dayjs().tz(timeZone).format('YYYY-MM-DD'))
}
