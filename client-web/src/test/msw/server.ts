import { setupServer } from 'msw/node'

// Empty by default; each test registers handlers with `server.use(...)`.
export const server = setupServer()
