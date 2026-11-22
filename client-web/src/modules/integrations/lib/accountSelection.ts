import type { AuthKind, ExternalAccount } from '../models'

/** What a feature needs from an account: which providers (and kinds) it accepts and which scopes. */
export interface AccountRequirement {
  providers: string[]
  authKind?: AuthKind
  requiredScopes?: string[]
  /** Accounts the feature already uses and cannot take again (e.g. one mailbox per account). */
  excludeIds?: string[]
}

/** Accounts of an accepted provider (and kind), whether or not they already have the scopes. */
export function candidateAccounts(accounts: ExternalAccount[], req: AccountRequirement): ExternalAccount[] {
  return accounts.filter(
    (a) =>
      req.providers.includes(a.provider) &&
      (req.authKind === undefined || a.authKind === req.authKind) &&
      !req.excludeIds?.includes(a.id),
  )
}

/** Whether an account grants every scope the feature needs (a missing one needs a "grant access" first). */
export function hasScopes(account: ExternalAccount, requiredScopes: string[] = []): boolean {
  return requiredScopes.every((scope) => account.scopes.includes(scope))
}

/** Candidates that can be used right away: connected and with every required scope. */
export function eligibleAccounts(accounts: ExternalAccount[], req: AccountRequirement): ExternalAccount[] {
  return candidateAccounts(accounts, req).filter(
    (a) => a.status === 'connected' && hasScopes(a, req.requiredScopes),
  )
}

/**
 * The account to preselect when nothing is chosen yet: the only eligible one. With two or more the
 * user decides — there is no default account.
 */
export function preselectedAccount(accounts: ExternalAccount[], req: AccountRequirement): string | null {
  const eligible = eligibleAccounts(accounts, req)
  return eligible.length === 1 ? eligible[0].id : null
}
