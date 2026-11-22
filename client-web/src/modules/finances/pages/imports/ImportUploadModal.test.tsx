import { beforeAll, afterEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { renderWithProviders } from '@/test/utils'
import type { ImportFileDto } from '../../models'
import * as importsService from '../../services/imports.service'
import { ImportUploadModal } from './ImportUploadModal'

beforeAll(async () => { await i18n.changeLanguage('en') })
afterEach(() => vi.restoreAllMocks())

function renderModal(onClose = vi.fn()) {
  return { onClose, ...renderWithProviders(<ImportUploadModal onClose={onClose} loadingDestinations={false}
    accountOptions={[{ value: 'a1', label: 'Checking (BRL)' }]} cardOptions={[{ value: 'c1', label: 'Mastercard (BRL)' }]} />) }
}
function uploadSpy() {
  return vi.spyOn(importsService, 'uploadImportFile').mockResolvedValue({ id: 'imp1' } as ImportFileDto)
}

describe('ImportUploadModal', () => {
  it('requires a destination and a file before any upload', async () => {
    const spy = uploadSpy()
    renderModal()
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    await user.click(screen.getByRole('button', { name: 'Upload file' }))
    expect(await screen.findByText('Select an account or card before uploading.')).toBeInTheDocument()
    expect(screen.getByText('Choose an OFX or CSV file.')).toBeInTheDocument()
    expect(spy).not.toHaveBeenCalled()
  })

  it('queues the selected file and uploads it only after confirming the account', async () => {
    const spy = uploadSpy()
    const { onClose } = renderModal()
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    await user.upload(document.querySelector('input[type="file"]') as HTMLInputElement, new File(['date,amount\n2026-06-12,10'], 'statement.csv', { type: 'text/csv' }))
    expect(spy).not.toHaveBeenCalled()
    await user.click(screen.getByRole('combobox', { name: 'Account' }))
    await user.click(screen.getByText('Checking (BRL)', { selector: '.ant-select-item-option-content' }))
    await user.click(screen.getByRole('button', { name: 'Upload file' }))
    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(spy).toHaveBeenCalledWith(expect.objectContaining({ name: 'statement.csv' }), 'a1', undefined, undefined)
  })

  it('clears the account when switching to a card and sends the chosen cutoff as an ISO date', async () => {
    const spy = uploadSpy()
    const { onClose } = renderModal()
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    await user.click(screen.getByRole('combobox', { name: 'Account' }))
    await user.click(screen.getByText('Checking (BRL)', { selector: '.ant-select-item-option-content' }))
    await user.click(screen.getByRole('radio', { name: 'Card' }))
    await user.upload(document.querySelector('input[type="file"]') as HTMLInputElement, new File(['data'], 'statement.ofx'))
    await user.click(screen.getByRole('button', { name: 'Upload file' }))
    expect(await screen.findByText('Select an account or card before uploading.')).toBeInTheDocument()
    expect(spy).not.toHaveBeenCalled()
    await user.click(screen.getByRole('combobox', { name: 'Card' }))
    await user.click(screen.getByText('Mastercard (BRL)', { selector: '.ant-select-item-option-content' }))
    await user.click(screen.getByText('Optional cutoff date'))
    await user.type(screen.getByRole('textbox', { name: 'Cutoff date' }), '12/06/2026')
    await user.keyboard('{Enter}')
    await user.click(screen.getByRole('button', { name: 'Upload file' }))
    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(spy).toHaveBeenCalledWith(expect.objectContaining({ name: 'statement.ofx' }), undefined, 'c1', '2026-06-12')
  })

  it('retains the file and destination after an upload failure so the user can retry', async () => {
    const spy = uploadSpy().mockRejectedValueOnce(new Error('Upload failed'))
    const { onClose } = renderModal()
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    await user.click(screen.getByRole('combobox', { name: 'Account' }))
    await user.click(screen.getByText('Checking (BRL)', { selector: '.ant-select-item-option-content' }))
    await user.upload(document.querySelector('input[type="file"]') as HTMLInputElement, new File(['data'], 'statement.csv'))
    await user.click(screen.getByRole('button', { name: 'Upload file' }))
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(1))
    expect(onClose).not.toHaveBeenCalled()
    expect(screen.getByText('statement.csv')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Upload file' }))
    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(spy).toHaveBeenCalledTimes(2)
  })
})
