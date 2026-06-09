interface JwtPayload {
  sub?: string
  Id?: string
  exp?: number
  [key: string]: unknown
}

function decodePayload(token: string): JwtPayload | null {
  const parts = token.split('.')
  if (parts.length < 2) return null
  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const json = atob(base64)
    return JSON.parse(json) as JwtPayload
  } catch {
    return null
  }
}

/** User id carried in the access token (claim "Id"/"sub"). */
export function getUserIdFromToken(token: string): string | null {
  const payload = decodePayload(token)
  return payload?.Id ?? payload?.sub ?? null
}
