# Frontend architecture

[Documentation index](../../README.md) · [Português](../pt-BR/frontend.md)

`client-web` is the browser app for every module: React 19, TypeScript, Vite, Ant Design 6, Tailwind,
TanStack Query, react-router and i18next. The Pandora Desktop shell wraps the same build
([desktop client](desktop-client.md)). How screens look and behave is in
[Pandora's look](../../../client-web/DESIGN.md); this page is how the code is organized.

## Folders

```
client-web/src/
  app/            providers, router, theme (theme.ts holds every color decision)
  components/     UI shared by more than one module (settings/: PageHeading, SettingsSection, SettingRow)
  i18n/           i18next setup and locales/en.json, locales/pt-BR.json
  layouts/        AppLayout (header, sidebar) and navigation.tsx (the sidebar's entries)
  lib/            app-wide helpers; lib/api has the HTTP client and the Tars envelope
  modules/<name>/ one folder per backend module
  pages/          screens that belong to no module (Home, NotFound)
  test/           test setup, msw server, renderWithProviders
```

Inside a module, one folder per kind of thing:

| Folder | Holds | Example |
|---|---|---|
| `models.ts` | Mirrors of the backend DTOs and requests, and the enum value lists | `TransactionDto`, `ACCOUNT_TYPES` |
| `services/` | One file per backend resource, plain async functions over `apiClient` | `transactions.service.ts` |
| `hooks/` | TanStack Query hooks over the services, and `queryKeys.ts` | `useTransactions.ts` |
| `lib/` | Pure logic: formatting, enum metadata, calculations, with tests beside them | `format.ts`, `recurrence.ts` |
| `components/` | UI reused by several screens of the module | `TransactionDetailDrawer.tsx` |
| `pages/<area>/` | Route screens and the modals that only they open | `pages/transactions/TransactionsListPage.tsx` |

A component used by one screen stays next to it in `pages/<area>/`. It moves to the module's
`components/` when a second screen needs it, and to `src/components/` when a second module does.

## Talking to the backend

- **`apiClient`** (`lib/api/client.ts`) is the one axios instance. It adds the bearer token and
  `Accept-Language` (so backend errors come back in the user's language), unwraps the Tars success
  envelope so `data` is the payload, refreshes the token once on a 401 for all requests waiting on it,
  and turns an error envelope into an `ApiResponseError`.
- **Services** take and return the `models.ts` types and know the route; nothing else does.
  ```ts
  const BASE = '/api/v1.0/finances/accounts'

  export async function getAccount(id: string): Promise<AccountDto> {
    const { data } = await apiClient.get<AccountDto>(`${BASE}/${id}`)
    return data
  }
  ```
- **Errors** reach the user through `toErrorMessage(err, fallback)`: the backend's localized message
  when there is one, the screen's fallback text otherwise.
- **Fetching everything.** List endpoints cap `take` (Finances: 200; a larger value falls back to the
  default of 50, it does not error). A screen that shows a whole period loads every page with
  `fetchAllPages` (`modules/finances/services/transactions.service.ts`), which asks for pages of 200
  until one comes back short. Never pass a large `take` and assume it was honored.

## Server state

- Server data lives in TanStack Query, never copied into component state. A screen derives what it
  shows (filters, totals, groups) from the query's data with `useMemo`.
- **Query keys** come from the module's factory (`hooks/queryKeys.ts`, e.g. `financeKeys`), built from
  the general to the specific so a whole area can be invalidated at once:
  ```ts
  accounts: () => [...financeKeys.all, 'accounts'] as const,
  account: (id: string) => [...financeKeys.accounts(), 'detail', id] as const,
  ```
  A hook never writes a key by hand.
- **Mutations** invalidate, in `onSuccess`, every key whose data they change, including the ones in
  other areas (a transaction changes the transactions, the accounts' balances, the cards and the
  statements). Hook tests check them with `vi.spyOn(client, 'invalidateQueries')` on the client from
  `createHookWrapper`.
- Several queries of the same kind (one balance per account) use `useQueries` on the same keys as the
  single-item hook, so they share its cache.

## Routing and navigation

- Routes are in `app/router.tsx`. Every protected screen is lazy, so it downloads when first opened:
  `{ path: 'finances/accounts', lazy: page(() => import('@/modules/finances/pages/accounts/AccountsListPage'), 'AccountsListPage') }`.
  Screens are named exports.
- Routes are in English and are what the browser shows (`/finances/recurring`).
- The sidebar's entries are in `layouts/navigation.tsx`. A new screen needs both. Each entry declares the
  `feature` and `permission` it needs: the menu hides it and its route shows the 404/403 page when the
  user lacks them (`layouts/navigationAccess.ts`). Buttons use `usePermission` or `<Can>` from
  `modules/identity`.

## Language

- Every string the user sees is a key in **both** `en.json` and `pt-BR.json`, under the module's
  namespace (`finances.transactions.new`). Shared words are under `common.*`.
- Plurals: `key_one` / `key_other`, called with `count`. Interpolation: `{{name}}`.
- English is the default and the fallback; the user's choice is stored on the device and in their
  preferences.
- Code, file names, routes and keys are in English. Comments may be in Portuguese.

## Tests

Vitest with jsdom, Testing Library and msw. Run `npm test` (or `npx vitest run <path>`).

- **Render** screens and components with `renderWithProviders` (`test/utils.tsx`): Query client
  without retries, router, antd `App`, i18n and a fixed preferences context. Hooks use
  `createHookWrapper`.
- **Language:** tests switch to English in `beforeAll` (`await i18n.changeLanguage('en')`) and find
  elements by their English text and role.
- **HTTP:** the msw server starts empty and fails on any request without a handler. Each test registers
  the handlers it needs with `server.use(...)`; build them with `FINANCES_BASE` and friends from
  `test/constants.ts`. Within one `server.use(...)` call the **first** matching handler wins, so put a
  test's specific handler before a generic one for the same route.
- **What to test:** pure logic in `lib/` gets unit tests (`recurrence.test.ts`); a service gets a test
  of its URL and body; a hook, of its key and invalidations; a screen, of the behavior a user would
  notice (a required field, a default value, what a menu does, what is sent).
- **Screen width:** antd reads breakpoints through `matchMedia`, which the setup answers `false` to
  every query, i.e. a phone. A test of the desktop layout mocks it to match `min-width` queries
  (`asDesktop()` in `MailPage.test.tsx`), and a phone test leaves it alone.

jsdom quirks already met:

- antd hides the native input of radio buttons with `pointer-events: none`: click the label's text
  (`user.click(screen.getByText('Card'))`), or set up `userEvent.setup({ pointerEventsCheck: 0 })`.
- Dropdown menu items have the role `menuitem`.
- A `tooltip` on `Form.Item` adds to the field's accessible name; find the field with a regex
  (`{ name: /Max occurrences/ }`).
- `form.isFieldTouched` can be true after `setFieldValue`; don't use it to tell whether the user typed.
- "Not implemented: getComputedStyle with pseudo-elements" in the output is jsdom noise, not a failure.

## Checks before calling a change done

From `client-web/`:

```bash
npx vitest run src/modules/<module>
```

```bash
npx tsc -b --noEmit
```

```bash
npx eslint src/modules/<module>
```

```bash
npx antd lint <changed .tsx files> --format json
```

Run the whole suite (`npm test`) when the change touches anything shared (`src/components`, `lib`,
`app`, `layouts`, `test`). `npm run build` also typechecks and bundles.
