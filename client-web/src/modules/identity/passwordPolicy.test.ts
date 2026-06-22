import { describe, it, expect } from 'vitest'
import { isStrongPassword, passwordRule } from './passwordPolicy'

describe('isStrongPassword', () => {
  it('accepts a password with upper, digit and special and length >= 8', () => {
    expect(isStrongPassword('Abcdef1!')).toBe(true)
  })

  it('rejects passwords shorter than 8 characters', () => {
    expect(isStrongPassword('Ab1!')).toBe(false)
  })

  it('rejects when there is no uppercase letter', () => {
    expect(isStrongPassword('abcdef1!')).toBe(false)
  })

  it('rejects when there is no digit', () => {
    expect(isStrongPassword('Abcdefg!')).toBe(false)
  })

  it('rejects when there is no special character', () => {
    expect(isStrongPassword('Abcdefg1')).toBe(false)
  })

  it('rejects empty input', () => {
    expect(isStrongPassword('')).toBe(false)
  })
})

describe('passwordRule.validator', () => {
  it('rejects empty values', async () => {
    await expect(passwordRule.validator(null, '')).rejects.toBeInstanceOf(Error)
  })

  it('rejects weak passwords', async () => {
    await expect(passwordRule.validator(null, 'weak')).rejects.toBeInstanceOf(Error)
  })

  it('resolves for a strong password', async () => {
    await expect(passwordRule.validator(null, 'Abcdef1!')).resolves.toBeUndefined()
  })
})
