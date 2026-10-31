# Communications — Execution Plan, M2 (Organize)

> **Status:** Done (2026-10-31); this is the history of how it was built. It was phase **M2** of the [product-plan](product-plan.md); what exists today is in
> [Implementation Status](implementation-status.md).
> 🇧🇷 [Versão em português](../pt-BR/execution-plan-m2.md)

This doc is the work list. The *why* behind the model is in the [product-plan](product-plan.md); section numbers (§) point
there.

**M2 delivers:** labels both ways (create, rename, delete) and batch actions on messages (read, star, move, archive, trash,
and add or remove labels on Gmail). **It is the first phase that changes real mailboxes.** No sending (M3), no rules (M4).

**Done when** (§15): a label created in Pandora appears in Gmail and in Titan, a folder renamed in the Titan webmail shows
renamed in Pandora, and a move done on either side is reflected on the other.

---

## What already exists (do not rebuild)

| Piece | Where | What M2 uses |
|---|---|---|
| Label sync | `SyncMailboxCommandHandler.ApplyLabelsAsync` | Every cycle re-reads the labels, so a change made in the webmail arrives by itself |
| Self-healing state | Gmail `history.list`, IMAP CONDSTORE + snapshots | A provider change, ours included, comes back as an upsert on the next cycle |
| Scope | `gmail.modify`, already granted | Covers labels, modify and trash: **no new consent** |
| Adapters | `GmailMailboxProvider`, `ImapMailboxProvider` | One more group of methods; `RunAsync` and the error mapping stay |
| Capabilities | `MailboxCapabilities.MultipleLabels` | Decides what the screens offer |
| Screens | `MailSidebar`, `MessageList`, `MessageReader` | Get a label menu, checkboxes and a toolbar |

---

## Locked decisions

| Decision | Value | Reason |
|---|---|---|
| Move in the webmail | **Stitch by Message-ID:** a message that vanished and reappeared in the same apply updates the same row | Keeps the row id and the cached body, and removes the false event ([found while planning](#the-bug-step-1-starts-with)). Rejected: keep "removed + new", which loses the cache on every move |
| Rename in the webmail | **Same stitching**, with no rename detection. The folder's headers are read again | One path. Detecting a rename by UIDVALIDITY is a heuristic that depends on the server; add it only if a large folder makes the re-read hurt |
| Rename in Pandora | **Same path too:** `RENAME` at the provider, then the labels + changes step at once | A folder's path is inside each `provider_message_id` and in the opaque cursor, so a direct update would touch both |
| Sync vs. actions | **Two gates per mailbox.** The cycle gate stops two cycles at once. The action gate covers an action, or the cycle's labels + changes step. The backfill runs outside it | A click never waits for a backfill, which can hold the cycle for 5 minutes. A stale batch that overwrites an action is corrected on the next cycle, since the action is a change at the provider. Worst case: the screen is wrong for one cycle. Rejected: one gate (up to 5 minutes of waiting); a shorter budget (the backfill can take twice as long) |
| Write order | **Provider first**, local row after it accepts (§3, C2) | A failure changes nothing locally and shows on screen |
| Permanent deletion | **Does not exist in M2.** "Delete" means trash: no port method, no endpoint | Nothing M2 does can lose mail for good |
| Deleting a folder (IMAP) | **Only when empty**, and never a system label. `409 LabelNotEmpty` otherwise | Deleting an IMAP folder deletes what is inside it |
| Batch size | Up to **100 ids per request, one mailbox per request** | A selection across mailboxes is grouped by the client; it needs no partial-failure format |
| Moving and labels across mailboxes | Offered only when the selection is in one mailbox | Labels belong to a mailbox. Read, star, archive and trash work across mailboxes |
| Opening a message | **Marks it as read**, from the client, through the same action | What a mail client is expected to do. M1 did not because it only read. A failure here is silent and does not block reading |
| Trash on IMAP | Move to the folder with role `trash`; none → `422 NoTrashFolder`. Same for archive | Pandora never invents a folder |
| Real-world tests | The real accounts, on **disposable material only**: a `pandora-test` label and folder, messages sent to yourself | The real environment with nothing new to set up |

---

## The bug step 1 starts with

Found by reading the code while planning, then **reproduced by a test and fixed in step 1**. In `ApplyMessagesAsync` the snapshot's removals run before
`ExistsWithRfc822IdAsync`. So a message moved in the webmail is deleted from the old folder and then looks new, and publishes a
false `MailMessageReceived`. The existing test `A_message_id_the_mailbox_already_has_is_not_an_arrival` only covers a copy, where
the original stays. Nothing consumes the event today, so no harm has been done yet.

`ApplyLabelsAsync` has a related problem: it deletes the messages of a vanished folder **before** the changes are read, which
leaves nothing to stitch to on a rename.

---

## Steps

Each step ends green (build + its tests) before the next one. Suggested commit points: **M2a** = step 1, **M2b** = steps 2–4,
**M2c** = steps 5–6. Commits only when asked.

### Step 1 — Sync engine: stitching and gates ✅

> **Built (2026-10-30).** `MailboxGates` (singleton), `MailboxChangesStep` (labels + changes, under the action gate; it takes the
> gate itself, so step 4 calls it outside its own hold), `MailMessage.Rebind`, and `MailboxWrites.UpdateMailboxAsync` shared by the
> step and the handler. The old engine failed the new tests, including one false `MailMessageReceived` on a move. Tests:
> `SyncEngineTests` +6 (move, rename, a folder gone for good, two copies moved together, no Message-ID, the two gates).

No new feature, and it changes no mailbox.

- **First, a failing test:** a real move through `FakeMailProvider` (`SnapshotLabel` + the same Message-ID under a new id and label)
  → no `MailMessageReceived`, the same row id, the cached body still there.
- `MailMessage.Rebind(providerMessageId)`.
- `ApplyMessagesAsync` in a new order:
  1. Work out what is vanishing (`removedIds` + each snapshot's missing rows left without a label).
  2. For each upsert with an unknown provider id and a Message-ID, look for a vanishing row with the same Message-ID, one to one:
     `Rebind` + `ApplyRemote`, and take it off the delete list.
  3. Delete the rest in bulk.
  4. Upsert as today; the arrival check stays.
- `ApplyLabelsAsync` splits in two: add and update before the changes; **remove vanished labels after** they are applied. The
  adapter already sends an empty snapshot for a folder out of scope, and the label must still exist for it to count. With no cursor
  yet (no changes to read), it removes at once, as today.
- `MailboxGates` replaces the handler's static dictionary: `Cycle(id)` and `Action(id)`. The cycle takes `Cycle` for its whole length
  and `Action` for labels + changes only.
- Extract the labels + changes step into something a command can call (step 4 runs it after a label operation).
- **Tests:** the move above; a renamed folder (old path gone, new path with the same Message-IDs) keeps its rows and bodies and fires
  no event; a copy stays two rows; a message with no Message-ID is still delete + create; an action is not blocked while a slow fake
  backfill holds `Cycle`.
- `ponytail:` a move between folders that straddles the walk (from a folder not read yet to one already read) spans two cycles, so it
  is not stitched and can still fire a false event. A window of seconds, on the IMAP walk only.

### Step 2 — Port and IMAP writes ✅

> **Built (2026-10-30).** Changes from the list below:
> - **`TrashAsync` returns the ids** like `MoveAsync`, since on IMAP it is a move.
> - **Subscriptions are ignored (changed during the real check, 2026-10-31).** This step first subscribed each new folder, as the list below
>   says. On Titan, which has no subscriptions, that flipped M1's rule ("subscribed folders, or all when none is"): Trash, Sent and the
>   others dropped out of Pandora, though nothing changed at the provider. Now every selectable folder is in scope, which replaces that
>   M1 decision. Test: `Every_folder_stays_in_scope_whatever_is_subscribed`.
> - **A folder with subfolders** is also `LabelNotEmpty`, and the check uses `STATUS`.
> - **The Gmail adapter answers `ActionNotSupported`** to every write until step 3. Nothing calls the writes yet.
> - **Not covered by GreenMail, so for step 6:** it has no SPECIAL-USE, so no folder there has the `trash` role (only the refusal is
>   tested); and it drops the connection on deleting a folder that once held a message, so the test deletes a never-used one. MailKit's
>   behavior without `MOVE` was not exercised either: GreenMail announces `MOVE` and `UIDPLUS`.
> - Tests: `ImapMailboxProviderTests` +4.

- `IMailboxProvider` gains:
  - `CreateLabelAsync(mailbox, name)` → `RemoteLabel`
  - `RenameLabelAsync(mailbox, providerLabelId, newName)`
  - `DeleteLabelAsync(mailbox, providerLabelId)`
  - `SetFlagsAsync(mailbox, ids, isUnread?, isStarred?)`
  - `ChangeLabelsAsync(mailbox, ids, add, remove)`: only where `MultipleLabels`
  - `MoveAsync(mailbox, ids, targetProviderLabelId)` → old id → new id, or null when the provider does not say
  - `TrashAsync(mailbox, ids)`
- `ImapMailboxProvider`:
  - Group the ids by folder; open it `ReadWrite`. A UIDVALIDITY that differs from the id → `SyncCursorExpired`, no write.
  - Flags: `AddFlags`/`RemoveFlags` for `\Seen` and `\Flagged`.
  - Move: `MoveToAsync`, reading the new UID. Without `UIDPLUS` the id is null, and the next cycle stitches the row. Check what MailKit
    does on a server without `MOVE`, and record whether Titan has `UIDPLUS` and `MOVE`.
  - Create: `CreateAsync`, with no subscription (see the note above).
  - Rename: `RenameAsync`. Delete: refuse a folder with messages (`LabelNotEmpty`), checked at the server, not just locally.
  - Trash: resolve the `trash` role in the adapter, and move.
- **Tests (GreenMail):** read and star seen from another client; a move leaves the source and reaches the target; a created folder is
  listed; a rename; deleting a non-empty folder is refused and the messages are still there; `INBOX` cannot be deleted.

### Step 3 — Gmail writes ✅

> **Built (2026-10-30).** Calls as listed below, confirmed against Google's documentation; `gmail.modify` accepts all of them, and
> `batchModify` takes up to 1,000 ids. A label is created with its name only. A message already gone counts as trashed.
>
> **Correction on quotas.** The numbers we held from memory were wrong. Per Google's page: **6,000 units per user per minute** and
> 1,200,000 per project per minute; `messages.get` costs **20** (60 with `raw` or `full`), `messages.trash` 20, `batchModify` 50,
> `labels.create` and `labels.delete` 5, `labels.list` 1, `history.list` 2. So a user gets at most 5 `messages.get` per second, whatever
> the parallelism, which fits the throttling seen on the first backfill. For M2, a batch of 100 trashed messages costs 2,000 units,
> a third of a user's minute, and is fine.
>
> Tests: `GmailMailboxProviderTests` +9, on canned answers. The real API is step 6.

- `labels.create`, `labels.patch`, `labels.delete` (user labels only).
- `messages.batchModify` for flags (`UNREAD`, `STARRED`) and labels. Move = add the target, remove `INBOX` or the current exclusive one
  (§4.3); the id does not change.
- `messages.trash`, one per message, `ParallelGets` at a time. `messages.delete` and `batchDelete` are **never called**.
- Confirm the limits against Google's documentation before relying on them; the quota numbers we hold are from memory.
- The retry in `GetAsync` becomes one send path shared with `POST`/`PATCH`/`DELETE`. These writes are idempotent, so retrying after a
  throttle is safe.
- **Tests:** on canned answers, the URL and body of each call; a `403` for scopes → `MissingScopes`; a throttled write is retried.

### Step 4 — Application and API ✅

> **Built (2026-10-30).** `ApplyMessageAction`, `CreateLabel`, `RenameLabel`, `DeleteLabel`, with `LabelOperation` shared by the three.
> Changes from the list below:
> - **Label endpoints return the mailbox's labels**, as they are after the labels + changes step. If that step fails, the operation
>   still succeeded at the provider: it is logged and the next cycle catches up.
> - **The step runs after the action gate is released**, since it takes the gate itself.
> - **`TooManyMessages` became `MessageCountInvalid`**, which also covers an empty list. `LabelNotFound` (404) is new.
> - **A label name** is trimmed and at most 100 characters; beyond that, the provider decides.
> - **Move targets:** never Sent or Drafts; on Gmail, of the system labels, only the inbox.
> - **Local trash and move** are what the next cycle will report anyway; a provider that gives no new id leaves the row to it.
> - Tests: `MailActionsApiTests` (14), including the stranger's ids with no provider call, and a failure with no local change.

- Commands under the action gate: `CreateLabel`, `RenameLabel`, `DeleteLabel`, `ApplyMessageAction`.
- **Label operations** call the provider, then run the labels + changes step, so renames take the one path.
- **Message actions:** provider first, then the local row (flags, labels, `Rebind` when a move returned a new id). A null id leaves the
  row to the next cycle.
- `MailErrors`: `LabelNotEmpty` (409), `SystemLabel` (422), `LabelNameInvalid` (422), `NoTrashFolder` and `NoArchiveFolder` (422),
  `ActionNotSupported` (422, labels on IMAP), `TooManyMessages` (422).
- A system label cannot be renamed, deleted or used as a move target where the provider forbids it (`SENT`, `DRAFT`, `CATEGORY_*`).
- API, `/api/v1/communications`:
  - `POST mailboxes/{id}/labels` `{ name }`, `PATCH labels/{id}` `{ name }`, `DELETE labels/{id}`
  - `POST messages/actions`:
    ```json
    { "mailboxId": "…", "messageIds": ["…"], "action": "move", "labelId": "…" }
    ```
    `action`: `mark-read`, `mark-unread`, `star`, `unstar`, `move`, `archive`, `trash`, `add-label`, `remove-label`. Returns the updated
    summaries.
- **Tests** (fake provider): another user's ids → 404; a message of another mailbox in the list → 404 and no provider call; a provider
  failure → the error and **no local change**; each action's local result; more than 100 ids → 422; a system label refused; a null id
  from a move leaves the row until the next cycle, which stitches it.

### Step 5 — Web ✅

> **Built (2026-10-30).** `MailActions` is one component for both the list's selection and the open message. Beyond the list below:
> - **Only what applies is offered:** "mark as read" with something unread, and so on. One label menu toggles: a label all the selected
>   messages have is removed, otherwise it is added.
> - **Trash asks first**, since Pandora has no undo.
> - **A renamed or deleted label that was open** sends the list back to its mailbox, since an IMAP rename gives the label a new row.
> - **A selection over 100** becomes several requests.
> - Tests: `MailPage.test.tsx` +5. The sidebar's label menu has no test of its own; the real check covers it.

- **`MessageList`:** a checkbox per row, "select the loaded page".
- **Toolbar** with a selection: read, unread, star, archive, trash, move to, label (Gmail only). It reads `mailbox.provider` for what to
  offer; moving and labels hide when the selection spans mailboxes. The client splits a selection into one request per mailbox.
- **`MailSidebar`:** a menu on user labels (rename, delete) and "new label" per mailbox, in a small modal. A non-empty folder shows the
  server's message.
- **`MessageReader`:** the same actions for the open message. Opening an unread message sends `mark-read`, without blocking the reader.
- Invalidate messages and labels after an action, with no optimistic update: the row changes when the provider has accepted it. A
  failure is a `message.error`, and the selection stays.
- i18n `mail.*` in `en.json` and `pt-BR.json`.
- **Tests:** a selection in two mailboxes makes two requests and hides "move"; a failed action leaves the row as it was; opening an unread
  message sends `mark-read` once.

### Step 6 — Real-world check and docs ✅

> **Done (2026-10-31).** Results are in [Implementation Status](implementation-status.md#real-world-check-of-m2-2026-10-31). `UIDPLUS` and `MOVE` on Titan were
> not recorded.


Only on disposable material: a `pandora-test` label and folder, and messages sent to yourself.

- **Gmail:** create `pandora-test` in Pandora → it appears in Gmail. Label, move and star → they show in Gmail. Trash → it is in Gmail's
  trash, and can be restored. A change in Gmail → it shows in Pandora.
- **Titan:** the same, with a folder. Move in the webmail → same row, cached body kept, no `MailMessageReceived` in the outbox. Rename in
  the webmail → renamed in Pandora, bodies kept. Delete a folder with a message → refused. Empty that folder and delete it → it goes.
  Trash finds the folder with the `trash` role.
- **Timing:** an action during a Gmail or Titan cycle does not wait for it.
- **Titan:** record `UIDPLUS` and `MOVE`.
- **Docs:** `overview`, `architecture` (gates, stitching), `data-model`, `api-reference`, `implementation-status`, en + pt-BR; the product
  plan keeps only M3–M4, and C8 and §13 describe the two gates; a **Built** note on each step here.

---

## Known limits

- **A folder rename reads its headers again.** Quick on a small folder; a very large one would need rename detection.
- **A move straddling the IMAP walk** spans two cycles (see step 1).
- **A folder that newly comes into scope with messages** arrives as new mail, with one false `MailMessageReceived` per message. Nothing
  consumes the event yet; it must be fixed before Radar does.
- **Only the loaded page can be selected.** "Everything under this label" is later.
- **No undo.** Trash can be restored at the provider.
- **After a Gmail action the screen is right at once**, but a stale backfill batch can put the old state back for up to one cycle.
