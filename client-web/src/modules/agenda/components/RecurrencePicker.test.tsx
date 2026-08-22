import { describe, it, expect, beforeAll, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { renderWithProviders } from '@/test/utils'
import { RecurrencePicker } from './RecurrencePicker'

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('RecurrencePicker', () => {
  it('emits an RRULE when a frequency is chosen', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<RecurrencePicker value={null} onChange={onChange} />)

    await user.click(screen.getByLabelText('Frequency'))
    await user.click(await screen.findByText('Daily'))

    expect(onChange).toHaveBeenLastCalledWith('FREQ=DAILY')
  })

  it('renders weekday checkboxes for weekly recurrence', async () => {
    renderWithProviders(
      <RecurrencePicker value="FREQ=WEEKLY;BYDAY=MO,WE" onChange={vi.fn()} />,
    )
    expect(screen.getByLabelText('Mon')).toBeChecked()
    expect(screen.getByLabelText('Wed')).toBeChecked()
    expect(screen.getByLabelText('Tue')).not.toBeChecked()
  })

  it('hides the "after N" end option when count is disallowed', () => {
    renderWithProviders(
      <RecurrencePicker value="FREQ=DAILY" onChange={vi.fn()} allowCount={false} />,
    )
    expect(screen.queryByText('After')).not.toBeInTheDocument()
    expect(screen.getByText('Never')).toBeInTheDocument()
  })
})
