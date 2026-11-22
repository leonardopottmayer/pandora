import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import timezone from 'dayjs/plugin/timezone'

dayjs.extend(utc)
dayjs.extend(timezone)

/** Today plus the next days in the account zone; each midnight resolves its own DST offset. */
export function agendaEventWindow(days: 7 | 30, timeZone: string, now = new Date()) {
  const first = dayjs(now).tz(timeZone).format('YYYY-MM-DD')
  const end = dayjs.utc(first).add(days, 'day').format('YYYY-MM-DD')
  return { from: dayjs.tz(first, timeZone).toISOString(), to: dayjs.tz(end, timeZone).toISOString() }
}
