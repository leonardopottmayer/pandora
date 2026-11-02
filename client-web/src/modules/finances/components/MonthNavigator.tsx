import { useTranslation } from 'react-i18next'
import { Button, DatePicker } from 'antd'
import { LeftOutlined, RightOutlined } from '@ant-design/icons'
import type { Dayjs } from 'dayjs'

/** ◀ month ▶, the month picker in between. `value` is the first day of the month. */
export function MonthNavigator({ value, onChange }: { value: Dayjs; onChange: (month: Dayjs) => void }) {
  const { t } = useTranslation()
  return (
    <span className="flex items-center gap-1">
      <Button
        type="text"
        size="small"
        icon={<LeftOutlined />}
        aria-label={t('finances.accounts.previousMonth')}
        onClick={() => onChange(value.subtract(1, 'month'))}
      />
      <DatePicker
        picker="month"
        size="small"
        variant="borderless"
        allowClear={false}
        format="MMMM YYYY"
        value={value}
        onChange={(month) => month && onChange(month.startOf('month'))}
        aria-label={t('finances.accounts.month')}
      />
      <Button
        type="text"
        size="small"
        icon={<RightOutlined />}
        aria-label={t('finances.accounts.nextMonth')}
        onClick={() => onChange(value.add(1, 'month'))}
      />
    </span>
  )
}
