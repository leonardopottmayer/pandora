/** API origin used in tests — must match `VITE_API_URL` in vitest.config.ts. */
export const TEST_API_BASE = 'http://localhost'

/** Convenience prefix for finances endpoints. */
export const FINANCES_BASE = `${TEST_API_BASE}/api/v1.0/finances`

/** Convenience prefix for notes endpoints. */
export const NOTES_BASE = `${TEST_API_BASE}/api/v1/notes`

/** Convenience prefix for agenda endpoints. */
export const AGENDA_BASE = `${TEST_API_BASE}/api/v1.0/agenda`
