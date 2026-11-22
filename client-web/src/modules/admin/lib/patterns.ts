import type { CatalogModule } from '../models'

/** Same rule as the backend: `*`, a prefix wildcard ending in `.*`, or the exact key. */
export function matches(pattern: string, key: string): boolean {
  if (pattern === '*') return true
  if (pattern.endsWith('.*')) return key.startsWith(pattern.slice(0, -1))
  return pattern === key
}

export function allKeys(catalog: CatalogModule[]): string[] {
  return catalog.flatMap((m) => m.resources.flatMap((r) => r.actions.map((a) => a.key)))
}

/** The declared keys a role's patterns cover. */
export function coveredKeys(patterns: string[], catalog: CatalogModule[]): Set<string> {
  return new Set(allKeys(catalog).filter((key) => patterns.some((p) => matches(p, key))))
}

/**
 * The shortest patterns for a set of keys: a whole module becomes `module.*`, a whole resource
 * `module.resource.*`, the rest stays as keys. Wildcards also cover what later releases add.
 */
export function toPatterns(keys: ReadonlySet<string>, catalog: CatalogModule[]): string[] {
  const patterns: string[] = []
  for (const mod of catalog) {
    const moduleKeys = mod.resources.flatMap((r) => r.actions.map((a) => a.key))
    if (moduleKeys.length > 0 && moduleKeys.every((k) => keys.has(k))) {
      patterns.push(`${mod.key}.*`)
      continue
    }
    for (const resource of mod.resources) {
      const resourceKeys = resource.actions.map((a) => a.key)
      if (resourceKeys.every((k) => keys.has(k))) patterns.push(`${mod.key}.${resource.key}.*`)
      else patterns.push(...resourceKeys.filter((k) => keys.has(k)))
    }
  }
  return patterns
}

/** Keys in the set whose dependency (the resource's `read`) is not in it. */
export function missingDependencies(keys: ReadonlySet<string>, catalog: CatalogModule[]): string[] {
  return catalog.flatMap((m) =>
    m.resources.flatMap((r) =>
      r.actions.filter((a) => keys.has(a.key) && a.requires.some((dep) => !keys.has(dep))).map((a) => a.key),
    ),
  )
}
