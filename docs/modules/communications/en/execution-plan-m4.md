# Communications — Execution Plan, M4 (Rules)

> **Status:** In progress: decisions approved, steps 1–6 built. It is phase **M4** of the [product-plan](product-plan.md), whose §10 has the model; what exists today is in
> [Implementation Status](implementation-status.md).
> 🇧🇷 [Versão em português](../pt-BR/execution-plan-m4.md)

**M4 delivers:** rules kept in one screen. On Gmail they are native filters, both ways; on Titan and IMAP they run in Pandora's own engine. No AI conditions, no "apply to existing messages", no ManageSieve (later).

**Done when** (§15): existing Gmail filters show up as rules on connect; a rule created in Pandora appears as a Gmail filter and applies on arrival with Pandora stopped; a filter edited in Gmail updates its rule in place; a Titan rule moves a new message within one cycle.

**Rules act without anyone looking,** and they touch filters the user already relies on. That shapes the decisions below.

---

## What Google's documentation says

Checked on 2026-10-31; it changes the plan in three places.

| Fact | Consequence |
|---|---|
| `filters.list` accepts `gmail.modify`; `create` and `delete` accept **only** `gmail.settings.basic` | Reading filters needs no new consent. Writing does, so a mailbox without the scope shows its filters read-only and asks for consent only when needed |
| "Only one user-defined label is allowed per filter" | One "add label" per Gmail rule, checked when saving |
| A filter has no `update`, and can `forward` and match on `size`, `negatedQuery` and `excludeChats` | An edit is create + delete. A filter with what the model does not hold must not be rewritten: see below |

Also: at most 1,000 filters; `delete` is permanent; `list` costs 1 unit, `create` and `delete` 5. Trash (`addLabelIds: TRASH`) and mark as read (`removeLabelIds: UNREAD`) are listed actions.

---

## Locked decisions

Approved on 2026-10-31.

| Decision | Value | Reason |
|---|---|---|
| Filters that do not fit | **Imported read-only:** the raw filter is kept, and the screen shows it with "edit it in Gmail" | Rewriting a filter that forwards or matches on size would silently drop that. Rejected: leaving them out, which breaks "existing filters show up" |
| Editing a filter | **Create the new one, then delete the old** | The opposite order loses the filter if the create fails. Worst case here: two for a moment |
| What Pandora deletes in Gmail | **Only when the user deletes or disables that rule, with a confirmation that says so.** Removing a mailbox from Pandora deletes no filter | A filter may have taken years to get right |
| Disabling | Deletes the Gmail filter, keeps the rule (§10.3), since Gmail has no disabled filters. The confirmation says it stops working in Gmail too | |
| How the engine gets its messages (IMAP) | **A subscriber to `MailMessageReceived`**, through the outbox | Saved in the same transaction as the message, so a crash does not lose a rule's run, and a slow rule does not hold the sync. Rejected: inside the sync step, where a crash after saving loses it |
| Old mail | **A folder newly in scope must publish no arrivals.** Fixed first, in step 1 | Otherwise rules would run on old mail: moved or trashed by a rule nobody aimed at it. This is the open point from M2 |
| Several rules matching | All apply, in `position` order. Flags and labels add up; **trash beats a move, and the first move wins** | A message is in one folder. Stated so the result is predictable |
| An empty rule | **Refused:** at least one condition | A rule with no condition matches all mail, and could trash it |
| A rule that fails | Retried by the outbox; the sync goes on. The error shows on the rule | One bad rule must not stop the mailbox |
| A label that is gone | The rule goes `broken`, is highlighted and does not run | §10.1 |
| Filter sync failing | **Never fails the mailbox's cycle.** Logged and shown on the rules screen | Filters are a side job, and reading mail matters more |
| Body conditions (`has-words`) | Fetches the raw message once per message, only if a rule still needs it | Each costs a provider call |
| The edit heuristic | §10.3: one gone + one new in the same cycle is an edit, with `ponytail:` | Covers editing one filter at a time |
| Real-world tests | A `pandora-test` rule and label, on messages sent to yourself; **no existing filter is edited or deleted** | They are yours, and nothing here restores one |

---

## Steps

Each step ends green before the next one. Suggested commit points: **M4a** = step 1, **M4b** = steps 2–3, **M4c** = steps 4–6, **M4d** = steps 7–8. Commits only when asked.

### Step 1 — No arrival from a folder newly in scope ✅

> **Built (2026-10-31).** `RemoteChanges.ExistingIds`; the old engine published the false event in the new test. A message that lands in a brand-new folder in the very cycle it first appears counts as existing, so it has no arrival: rare, and it errs on the side of a rule not firing. Tests: `SyncEngineTests` +1, `ImapMailboxProviderTests` +1.

- **First, a failing test:** an IMAP folder with messages appears after the backfill → its messages are saved, no `MailMessageReceived`. A real new message in it on a later cycle → one event.
- The adapter marks what comes from a folder the cursor does not know; the engine publishes nothing for those.

### Step 2 — The model ✅

> **Built (2026-10-31).** Migration `20261031120000-create-table-com006-mail-rule`, `MailRule`, `RuleExecution`, `RuleStatus`, `RuleMatcher` (with `RulePlan`), `IMailRuleRepository`. Decided here, beyond the text below: a condition with an empty value is refused (it would match all mail; `has-attachment` takes none); "add label" is refused off Gmail, where a folder is where a message lives; a move beats an archive; `has-words` looks at subject and text, as a substring, not whole words; the engine never matches `provider-query`, which only Gmail can run. `provider_filter_id` is null while a rule is disabled, since Gmail has no disabled filters. Tests: `MailRuleTests`, and a round trip in `PersistenceTests`.

- Migration `com006_mail_rule`: mailbox, name, `position`, `conditions` and `actions` (jsonb), `execution` (`provider-filter`/`pandora`), `provider_filter_id`, `provider_filter_raw` (jsonb, for what does not fit), `is_enabled`, `status` (`ok`/`broken`/`read-only`), `last_error`.
- `MailRule`: conditions `from`, `to`, `subject`, `has-words`, `has-attachment`, `provider-query`; actions add label, move, archive, mark as read, star, trash. Validation: at least one condition and one action; on Gmail, one user label at most; `provider-query` on Gmail only.
- `RuleMatcher`: pure, over a message and, only when needed, its text. Conditions as Gmail treats them, so a rule behaves alike on both: `from` and `to` as substrings, case-insensitive; `to` covers To and Cc.
- `RulePlan`: from the rules that match, one set of actions, as decided.
- **Tests (unit):** each condition; AND; the merge; empty rules refused; a label beyond the one allowed refused.

### Step 3 — The engine (IMAP) ✅

> **Built (2026-10-31).** `RunMailRulesHandler`, and `MailboxChangesStep` marks rules `broken`. What the check of the outbox found: delivery is at least once, a handler that throws is retried up to 8 times and then dead-lettered, and a crash after a handler finishes repeats it. So the handler skips what is already done, by the row: a message already in its folder is not moved again. Decided here: only `ProviderFailed` and `MailboxAuthFailed` throw for a retry; any other failure (no archive folder, say) would only repeat, so it stays on the rule. An error goes on all the rules that matched, since the plan merges them, and the next success clears it. A failed run is not repeated once the retries are spent. Renaming a folder in IMAP is one gone and one new, so its rules go `broken` and need the folder picked again. Tests: `MailRulesEngineTests`.

- `RunMailRulesHandler : IIntegrationEventHandler<MailMessageReceived>`: mailbox with `pandora` rules only; loads them; matches; applies through the same path as `ApplyMessageAction`, under the action gate.
- **Check how the outbox retries a handler that throws, and whether it can repeat one that succeeded.** Applying a rule twice must be harmless: a moved message is found by its row, and read and star are idempotent.
- A rule whose label is gone → `broken` when the labels step removes it.
- **Tests:** a new message is moved; old mail is not; a rule fails → the sync goes on and the error is on the rule; `broken` does not run; a rule delivered twice acts once.

### Step 4 — Gmail filters in the adapter ✅

> **Built (2026-10-31).** `ListFiltersAsync`, `CreateFilterAsync` and `DeleteFilterAsync` on the port (IMAP answers `ActionNotSupported`), `GmailFilterMapper`, `MailErrors.MissingFilterScope`. Decided here: on Gmail a rule takes one condition of each kind, since a filter has one field for each, and `has-words` and `provider-query` share `query`; a `query` with a `:` reads back as `provider-query`, otherwise as `has-words`, which Gmail treats alike. A user label is an id starting with `Label_`; any other system label (`IMPORTANT`, `SPAM`, a category) makes the filter read-only. Deleting a filter already gone counts as done. The scope is checked on the token, and Gmail's own 403 gives the same error, since the scopes on record can be out of date. Not checked against real Gmail until step 8: all of this comes from the documentation, including the `Label_` prefix. Tests: `GmailMailboxProviderTests` and `MailRuleTests`.

- `ListFiltersAsync`, `CreateFilterAsync`, `DeleteFilterAsync` on the port, only where `ServerFilters`.
- `GmailFilterMapper`: rule ⇄ filter. Move = add label + remove `INBOX`; archive = remove `INBOX`; read = remove `UNREAD`; star = add `STARRED`; trash = add `TRASH`. A filter with `forward`, `size`, `negatedQuery` or anything else it cannot express → read-only.
- A create or delete without `gmail.settings.basic` → `MissingFilterScope`, which does **not** stop the mailbox (not `MissingScopes`).
- **Tests (canned answers):** each way, both directions; a round trip changes nothing; a filter that forwards is read-only; the scope error.

### Step 5 — Filters both ways ✅

> **Built (2026-10-31).** `SyncFiltersCommand`, sent by the cycle after the changes, so a filter finds its labels. Connecting needs nothing of its own: on the first cycle every filter is new. Decided here: the error goes on a new `com001.filter_sync_error`, since the rules screen must show it even with no rules, and a throw shows only the fixed text; a filter pointing at a label not known here is imported read-only, not dropped; the name comes from all of a filter's criteria, so a read-only one gets a useful name too, and the adapter builds it, since it knows the format. New rules go after the last one. **For step 6:** EF 10 cannot save a `Remove` of an entity with a JSON complex collection, so deleting a rule is `DeleteByIdsAsync`; and the rule writes must share a gate with this diff, or a sync between Gmail's answer and the rule's save imports the filter a second time. Tests: `FilterSyncTests`.

- `SyncFiltersCommand`, once per cycle, outside the action gate, failures logged: `list`, diff by `provider_filter_id`, §10.3's cases.
- Connecting a Gmail mailbox imports through the same diff.
- **Tests:** a new filter → a rule; one gone → the rule goes (a disabled rule is skipped); one gone + one new → same rule, same name and position; two gone + one new → delete + import; `list` failing leaves the rules and the cycle alone.

### Step 6 — API ✅

> **Built (2026-11-01).** The routes, `RuleOperation`, `GetRulesQuery`, and `MailboxGates.Rules`, shared with the filter diff; the mailbox's DTO carries `filterSyncError`. Every write answers with the mailbox's rules as they are now. Decided here: enabling and disabling go through `PUT rules/{id}`, with `isEnabled`; a change of name alone does not touch Gmail, since it is Pandora's own; a read-only rule cannot be renamed or disabled, since a disabled filter could not be made again as it was, but it can be deleted, which deletes the filter, or the next cycle would import it again; reordering takes the ones listed, then any other, so a filter imported while the screen was open is no error. On Gmail a rule can only point at a user label, since a system one would read back as read-only. If an edit's delete fails, the old filter comes back as a rule of its own on the next cycle, which is the truth: it is still running. The filter endpoints are tested with the scripted provider under `titan` with the filters capability, since a Gmail mailbox needs OAuth; real Gmail is step 8. Tests: `MailRulesApiTests`, `MailRuleTests` +1.

- `GET/POST mailboxes/{id}/rules`, `PUT/DELETE rules/{id}`, `PUT mailboxes/{id}/rules/order`.
- Saving on Gmail goes to Gmail first (C2). An edit is create, save, delete; if the delete fails, the rule is right and the old filter is reported.
- **Tests:** another user's → 404; read-only cannot be edited; a Gmail failure changes nothing; an edit whose delete fails keeps the new one and says so; more than 1,000 filters is Gmail's refusal, shown.

### Step 7 — Web

- `mail/rules`: rules per mailbox, drag to reorder, enable toggle, a badge for where each runs, `broken` and read-only highlighted; the condition and action form.
- Deleting or disabling a Gmail rule says it changes Gmail.
- Writing without the scope: a "grant access" that goes through consent, without losing the form.
- i18n `mail.rules.*`.
- **Tests:** the form's validation; the confirmation; read-only has no edit; the consent prompt.

### Step 8 — Real-world check and docs

Your part in Google Cloud first: declare `gmail.settings.basic` on the consent screen, then reconnect the Gmail account.

- **Import:** your filters show up, nothing changes in Gmail, and one with a forward (if any) is read-only.
- **Gmail:** a `pandora-test` rule becomes a filter; with the **backend stopped**, a message sent to yourself gets the label. Edit it in Gmail → the same rule updates. Disable and delete it in Pandora → it goes from Gmail.
- **Titan:** a `pandora-test` rule moves a new message within one cycle; a message from before does not move; delete the folder → `broken`.
- **Docs:** topic docs, en + pt-BR; the product plan has only "later"; a **Built** note on each step.

---

## Known limits

- **Rules apply to mail that arrives after they are saved** (§10.1).
- **Titan's webmail filters are not seen,** and it has no ManageSieve: rules there run only while Pandora does.
- **Delay on IMAP:** up to one cycle, 2 minutes.
- **The edit heuristic** can read two edits in one cycle as delete + import, losing the name.
- **A `has-words` rule on a busy mailbox** costs one provider call per message that reaches it.
