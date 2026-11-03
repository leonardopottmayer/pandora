# Pandora's look

Pandora stays open all day and mixes tables, notes and a calendar. The user picks how it looks, and the screens stay out of it.

The values live in [`src/app/theme.ts`](src/app/theme.ts), on top of antd's own algorithms. How the frontend code is organized (folders, data, tests) is in [Frontend architecture](../docs/architecture/en/frontend.md); this page is how screens look and behave.

## Three choices

| Choice | Values | Stored in |
|---|---|---|
| Mode | light, dark, system | `idt003.theme` |
| Theme | Default, Claude, Reddit | `idt003.palette` |
| Accent | the theme's own, or any color | `idt003.accent_color`, null for the theme's own |

They combine freely, so a new theme is one more block in `PALETTES`, in both modes, and nothing else. Claude's and Reddit's colors are inspired by their dark and light modes, and were not measured from them.

## The accent

The user picks one color and antd derives its hover, active and selected tones. Three things are worked out so that any color works:

- **Text on the accent** is white, or navy `#181b3d` where white falls under 3:1.
- **The accent as text**, in links and the selected menu item, moves toward white or black until it reaches 4.5:1 on every surface.
- **Green, red and yellow stay fixed.** They say success, error and warning, and don't follow the accent.

The ready-made ones are Ant Design's blue `#1677ff` and green `#2dcd88`, whose hover is `#2dbc88`.

`theme.test.ts` checks this on every combination of theme, mode and a range of accents, including white, near-black and yellow.

## Rules

- **Colors come from tokens.** No hex in a screen: `theme.useToken()` in code, `var(--ant-color-*)` in CSS. Data the user picked, like a calendar's color, is the exception.
- **On an accent background, use `onAccent()`,** not white.
- **One accent.** The primary is for the one main action of a view and for what is selected. Every other button is the default.
- **Color carries meaning.** Green, red and yellow are not decoration. Money in is green, money out is red, a late date is red, a pending item is gold. Nothing is colored only to look lively.
- **Change the theme before the screen.** If many screens need the same change, it belongs in `theme.ts`. Before overriding a component, check its tokens with `antd token <Component>`.

## Type and layout

- **Type:** the system font, at antd's sizes.
- **Numbers:** tabular (`tabular-nums`), so amounts and dates line up in columns.
- **Density:** antd's default size. Tables can go smaller when a screen asks for it, not globally.
- **Screen width:** settings and forms at `max-w-3xl`; lists and tables use the available width.
- **Settings screens** use `PageHeading`, `SettingsSection` and `SettingRow`.
- **Filters:** what is used most stays visible, the rest collapses. Filters should not push the data off the first screen.

---

# Screen patterns

The screens redesigned since November 2026 (email, Finances accounts, account detail, transactions, recurrences) all follow the patterns below. A new or redesigned screen copies the closest one instead of inventing its own:

| Kind of screen | Copy |
|---|---|
| List with figures, filters and a long table | `modules/finances/pages/transactions/TransactionsListPage.tsx` |
| List in groups (active, paused, finished) | `modules/finances/pages/recurring/RecurringTransactionsListPage.tsx` |
| Short list of things to manage | `modules/finances/pages/accounts/AccountsListPage.tsx`, `modules/communications/pages/MailAccountsPage.tsx` |
| One thing and what belongs to it | `modules/finances/pages/accounts/AccountDetailPage.tsx` |
| Three panes (folders, list, reader) | `modules/communications/pages/MailPage.tsx` |
| A form in a modal | `AccountFormModal.tsx` (short), `TransactionFormModal.tsx` (medium), `RecurringTransactionFormModal.tsx` (long, in blocks) |
| Seeing and editing one row | `modules/finances/components/TransactionDetailDrawer.tsx` |

## Building blocks

Use these before writing markup of your own:

| Component | Where | What it is |
|---|---|---|
| `PageHeading` | `components/settings` | The page title (h4) and a one-line description under it |
| `SettingsSection` | `components/settings` | A small uppercase title, an optional `extra` on its right, and a bordered panel under it |
| `SettingRow` | `components/settings` | Label and description on the left, the control on the right; stacks below `sm` |
| `StatTile` | `modules/finances/components` | One figure: a small label, the value large, an optional note |
| `MonthNavigator` | `modules/finances/components` | `‹ month ›`, a month picker between the arrows |
| `CurrencyAmount` | `modules/finances/components` | A signed, colored amount from a direction (`in`, `out`, `neutral`) |

A component that more than one module needs moves to `src/components`. Until then it stays in the module that made it.

## Page anatomy

From top to bottom, with `gap-6` between the parts:

1. **Heading row:** `PageHeading` on the left; on the right, at most one primary button (the action the screen exists for) and, if needed, default buttons beside it.
   ```tsx
   <div className="flex shrink-0 flex-wrap items-start justify-between gap-3">
     <PageHeading title={t('nav.transactions')} description={t('finances.transactions.subtitle')} />
     <Space>
       <Button icon={<SwapOutlined />}>{t('finances.transactions.transfer')}</Button>
       <Button type="primary" icon={<PlusOutlined />}>{t('finances.transactions.new')}</Button>
     </Space>
   </div>
   ```
2. **Figures**, when the screen has numbers worth knowing before the rows: a row of `StatTile`s (`flex flex-wrap gap-3`). A figure answers a question the user has ("how much went out this month", "what do the recurrences commit me to"); a count of rows is not a figure. With nothing to show, the tiles stay with `—`, so the table doesn't jump.
3. **Filters**, in one wrapping row: the period, a search and the one or two selects used most. The rest goes behind a "More filters" button with a badge counting the active ones, in a panel that opens **over** the table (`absolute`, `colorBgElevated`, `boxShadowSecondary`), so nothing above it changes height. A "Clear" link appears only when a filter is on.
4. **The data**, in one panel (below).

## Panels

The data sits in a bordered panel, never on the bare page:

```tsx
<div
  className="overflow-hidden rounded-xl"
  style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}
>
```

`SettingsSection` draws the same panel with a title above it. One panel per screen is the default: groups of the same thing (active / paused / finished) go inside one panel as group headings, not as separate panels with titles between them.

## Holding the screen's height

From `lg` up, a list screen holds the window's height: the heading, figures and filters stay put, and only the table scrolls, with its header held. Below `lg` the page scrolls as usual, because the filters wrap and would leave the table no room.

```tsx
// The layout's header (64) and padding (48) are 112px.
<div className="flex flex-col gap-6 lg:h-[calc(100dvh-112px)]">
  <div className="shrink-0">{/* heading */}</div>
  <div className="shrink-0">{/* figures, filters */}</div>
  <div
    className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl"
    style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}
  >
    <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
      <Table
        tableLayout="fixed"
        sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
        pagination={false}
        /* ... */
      />
    </div>
    {/* footer: "N transactions", text-xs, secondary, border-top */}
  </div>
</div>
```

- `scrollEl` is state (`useState<HTMLDivElement | null>`), not a ref, so the table re-renders once the element exists.
- Everything above the panel is `shrink-0`; the panel is `min-h-0 flex-1`. Without `min-h-0` the panel grows instead of scrolling.
- Nothing that scrolls may sit outside the panel. A group title above the table that scrolls away with the page is wrong; put it inside as a sticky group heading.
- Use `100dvh`, never `100vh`: on a phone `100vh` counts the browser's bars and leaves a sliver of scroll.
- No pagination on these screens. The data for the period is loaded in full (see "Fetching everything" in the frontend architecture) and a count sits in the footer.

## Tables and rows

- **Few columns.** A row is the main text, a secondary line under it, and the number that matters, right-aligned. Details join the secondary line with ` · ` (`category · account · 3/12`) instead of getting their own columns.
- `tableLayout="fixed"` with fixed widths for the narrow columns (date 110, amount 124, menu 48), so the main column truncates instead of widening the table past a phone's width. Text that can be long gets `truncate` inside a `min-w-0` container.
- Columns that matter less hide on small screens (`responsive: ['md']`), and their value moves into the secondary line there (`<span className="md:hidden">`).
- **The row opens the item** (`onRow={() => ({ onClick, className: 'cursor-pointer' })}`): the detail drawer, the edit modal or the detail page.
- **Status tags only when they say something.** No green "Active" on every row. A tag appears for what is unusual: pending in gold, a void row struck through and secondary, late in red.
- An empty table says what is missing in a sentence (`locale={{ emptyText: t('...empty') }}`).

### The row menu

Actions on a row go in one `⋯` menu at the end of the row, not as buttons:

```tsx
// The row opens the item; its menu must not.
<span onClick={(e) => e.stopPropagation()}>
  <Dropdown
    trigger={['click']}
    placement="bottomRight"
    menu={{ items: [
      { key: 'edit', label: t('...edit'), onClick: ... },
      { type: 'divider' },
      { key: 'delete', danger: true, label: t('...delete'), onClick: () => confirmDelete(item) },
    ] }}
  >
    <Button type="text" size="small" icon={<EllipsisOutlined />} aria-label={t('...moreActions', { name })} />
  </Dropdown>
</span>
```

- The button's `aria-label` names the row ("More actions for Netflix"), so tests and screen readers can tell the rows apart.
- Destructive items come last, after a divider, with `danger`, and ask first through `modal.confirm` (from `App.useApp()`), with the item's name as the content and the confirm button `danger`.
- An action that only applies in some states shows only then (Post on a pending transaction, Resume on a paused recurrence).

### Grouped lists

When a list has states the user thinks of separately, group the rows in one panel under sticky headings, in the order of what matters now (active by next date, then paused, then finished folded):

```tsx
<div
  className="sticky top-0 z-10 border-x-0 border-b border-t border-solid px-4 py-1.5 text-xs font-semibold uppercase tracking-wider first:border-t-0"
  style={{
    color: token.colorTextSecondary,
    borderColor: token.colorBorderSecondary,
    // Opaque, so the rows going under it do not show through; tinted like a table header.
    background: token.colorBgContainer,
    boxShadow: `inset 0 0 0 9999px ${token.colorFillAlter}`,
  }}
>
  {t('...activeGroup')} · {active.length}
</div>
```

`colorFillAlter` is translucent, so it can't be the background alone; the inset shadow tints an opaque `colorBgContainer`. A group that is rarely needed (finished) folds behind a button in its own heading, with `aria-expanded`.

## Detail

- **One row, seen and edited in place:** a `Drawer` from the right (`TransactionDetailDrawer`). The fields that can change are editable right there; the state actions (post, void, unvoid) and Save sit in the footer. It takes the item's id and `onClose`, and every screen that lists the item opens the same drawer.
- **A thing with its own content** (an account and its statement): its own page, with the heading carrying the thing's name and its actions, figures under it, then the content. The account's statement is grouped by day under sticky day headings, a month at a time with `MonthNavigator`.

## Forms

- **Modal width:** 520 (antd's default) for a handful of fields, 640 for a medium form, 720 for one in blocks. A body that can be long scrolls inside: `styles={{ body: { maxHeight: 'calc(100dvh - 260px)', overflowY: 'auto', paddingRight: 8 } }}`.
- **Two fields a row** from `sm` up, for fields that belong together: `<div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">`. A field that needs the room (description, notes) takes the full width.
- **Order:** what the user always fills first, from the top. The required fields come before the optional ones.
- **Optional fields fold away.** What most entries leave empty goes under a native `<details>` at the end, its summary naming what is inside ("Payee and notes", "More options"). Open it when editing an item that already has a value there (`open={!!item?.description || undefined}`).
  ```tsx
  <details>
    <summary className="mb-3 w-fit cursor-pointer select-none text-sm">{t('...moreOptions')}</summary>
    {/* optional Form.Items */}
  </details>
  ```
- **A long form goes in blocks** with a small uppercase title over a hairline (`FormGroup` in `RecurringTransactionFormModal.tsx`: "What", "When", "Automation").
- **Sensible defaults:** the date starts on today, the account on the one the screen was opened from (`defaultAccountId`), the kind on the most common one. A required field never starts empty when there is an obvious value.
- **Values the form can work out, it does:** an amount shows its account's or card's currency as `prefix`; choosing a bank fills the institution if it is still empty; a name fills the description until the user types their own (compare with a ref of the last value it copied, not with `isFieldTouched`, which `setFieldValue` can set).
- **One choice, one control.** Mutually exclusive settings are one radio, not two fields that can contradict each other ("ends: never / on a date / after N times"), and only the chosen one is sent. "Every [2] [months]" is one field made of an `InputNumber` and a `Select` in a `Space.Compact`.
- **Explain under the field, not in a tooltip,** when the user needs it to decide (`extra` on `Form.Item`, used for switches). A tooltip is for a detail most people won't need.
- **Show the consequence.** A schedule previews its next dates under the fields; a currency field shows the currency.
- **Validation messages** say what to do ("Enter the description."), and are in both languages.
- The footer is Cancel and Save (`okText={t('common.save')}`), Save with `loading` while the request runs.

## Feedback

- After a change: `message.success(t('...created'))`. On failure: `message.error(toErrorMessage(err, t('...saveError')))`, which shows the backend's localized message when there is one.
- Loading a list: the table's own `loading`. Loading a page: antd `Skeleton` or `Spin` in the place of the content, not over the whole screen.
- Deleting, voiding and anything else that loses or hides data confirms first. A change the user can see and undo on the spot (pause, resume, post) does not.

## Small screens

- Test every screen at 375px wide. Nothing scrolls sideways.
- `Grid.useBreakpoint()` decides layout changes JS has to make: the email screen shows one pane at a time below `lg` (`narrow = !screens.lg`, with a back button in the reader) and moves the folders into a `Drawer` below `xl`.
- Filters take the full width below `sm` (`w-full sm:w-56`).

## Words

- Text in the UI is short, plain and in sentence case. Say what the thing is or does ("Next 30 days", "Payee and notes"), not how it is built.
- Every string is a key in both `en.json` and `pt-BR.json`, under the module's namespace (`finances.transactions.*`). Plurals use i18next's `_one`/`_other` keys with `count`.
- Dates are `DD/MM/YYYY`; relative dates where they help ("today", "tomorrow", "in 3 days", "2 days late").

## Checking a screen

Before calling a screen done:

- `npx antd lint <files> --format json` reports nothing.
- It works in light and dark, in each theme, and with a yellow accent.
- At 375px nothing scrolls sideways, and at `lg` only the table scrolls.
- One primary button per view; no hex in the code.
