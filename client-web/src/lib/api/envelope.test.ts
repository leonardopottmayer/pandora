import { describe, it, expect } from 'vitest'
import {
  isSuccessEnvelope,
  isErrorEnvelope,
  ApiResponseError,
  errorFromEnvelope,
  toErrorMessage,
  type TarsErrorEnvelope,
} from './envelope'

describe('isSuccessEnvelope', () => {
  it('recognises a success envelope with data', () => {
    expect(isSuccessEnvelope({ success: true, data: 1 })).toBe(true)
  })

  it('rejects an error envelope', () => {
    expect(isSuccessEnvelope({ success: false })).toBe(false)
  })

  it('rejects a success flag without a data field', () => {
    expect(isSuccessEnvelope({ success: true })).toBe(false)
  })

  it('rejects null and non-objects', () => {
    expect(isSuccessEnvelope(null)).toBe(false)
    expect(isSuccessEnvelope('x')).toBe(false)
  })
})

describe('isErrorEnvelope', () => {
  it('recognises an error envelope', () => {
    expect(isErrorEnvelope({ success: false, errorCode: 'X' })).toBe(true)
  })

  it('rejects a success envelope', () => {
    expect(isErrorEnvelope({ success: true, data: 1 })).toBe(false)
  })

  it('rejects null', () => {
    expect(isErrorEnvelope(null)).toBe(false)
  })
})

describe('ApiResponseError', () => {
  it('exposes the first field-error message via firstMessage', () => {
    const err = new ApiResponseError('top', 'CODE', [{ code: 'f', message: 'field message' }])
    expect(err.firstMessage).toBe('field message')
    expect(err.code).toBe('CODE')
    expect(err.name).toBe('ApiResponseError')
  })

  it('falls back to the main message when there are no field errors', () => {
    const err = new ApiResponseError('top')
    expect(err.firstMessage).toBe('top')
  })
})

describe('errorFromEnvelope', () => {
  it('maps the envelope error message and code', () => {
    const env: TarsErrorEnvelope = {
      success: false,
      errorCode: 'VALIDATION',
      errorMessage: 'Invalid',
      fieldErrors: [{ code: 'name', message: 'Required' }],
      traceId: 't1',
    }
    const err = errorFromEnvelope(env)
    expect(err).toBeInstanceOf(ApiResponseError)
    expect(err.message).toBe('Invalid')
    expect(err.code).toBe('VALIDATION')
    expect(err.traceId).toBe('t1')
  })

  it('falls back to the first field error when errorMessage is null', () => {
    const env: TarsErrorEnvelope = {
      success: false,
      errorCode: null,
      errorMessage: null,
      fieldErrors: [{ code: 'name', message: 'Required' }],
      traceId: null,
    }
    expect(errorFromEnvelope(env).message).toBe('Required')
  })

  it('falls back to a generic message when nothing is provided', () => {
    const env: TarsErrorEnvelope = {
      success: false,
      errorCode: null,
      errorMessage: null,
      fieldErrors: null,
      traceId: null,
    }
    expect(errorFromEnvelope(env).message).toBe('Request failed.')
  })
})

describe('toErrorMessage', () => {
  it('uses firstMessage for an ApiResponseError', () => {
    const err = new ApiResponseError('top', null, [{ code: 'f', message: 'field' }])
    expect(toErrorMessage(err, 'fallback')).toBe('field')
  })

  it('uses the message of a plain Error', () => {
    expect(toErrorMessage(new Error('boom'), 'fallback')).toBe('boom')
  })

  it('uses the fallback for unknown values', () => {
    expect(toErrorMessage(42, 'fallback')).toBe('fallback')
    expect(toErrorMessage(new Error(''), 'fallback')).toBe('fallback')
  })
})
