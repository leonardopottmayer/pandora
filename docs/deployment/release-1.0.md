# Release 1.0

1.0 means **Pandora running in production with real data**. This checklist covers the release, in order,
and the manual script for checking against real accounts what the tests only cover with fakes. Tick each
item as you go.

The pipeline: the CI only builds images on a push to `main` (`:latest`, `:<VERSION>`, `:sha-…`).
Staging runs `:latest`, and prod runs a pinned tag. So the order is: `dev` → `main` → staging checked →
`VERSION` 1.0.0 → prod.

---

## 1. Code (done before the release)

- [x] Dates in the user's zone everywhere (KI-001, KI-003), Finances jobs per user.
- [x] Assistant: an unanswered confirmation reads as `expired` in the history; conversations are kept
      on purpose (see the module's implementation-status).
- [x] KI-002 closed (not needed yet); Gmail testing mode accepted.
- [x] Daily backup and restore check written (repo **homelab**, `docs/05-backups.md`).

## 2. Staging

- [ ] Merge `dev` into `main` and push; the CI goes green and publishes `:latest`.
- [ ] On the homelab: `docker login ghcr.io` (if not done yet) and the local `migris` config (see
      [homelab-deploy.md §1.5](homelab-deploy.md)).
- [ ] **Recreate the staging database from scratch.** Before 1.0, migrations were edited in place, so an
      old database does not match them: drop and recreate the `pandora` database in `staging-db`
      ([deployment.md, step 2](deployment.md#2-provision-pandoras-database--role-once-per-tier)), then
      `migris apply staging`.
- [ ] `docker compose --env-file .env.staging pull && docker compose --env-file .env.staging up -d`.
- [ ] Run the script in §3 against `http://<homelab>:8731`.

## 3. Manual script (staging, real accounts)

Write down what fails. The rest stays as is.

**Identity**
- [ ] Sign up, activation e-mail in Mailhog (`:8732`), sign in.
- [ ] Turn on MFA (TOTP), sign out, sign in with the code. Password reset through the e-mail.
- [ ] Preferences: zone, language, theme. Change the zone and see the Agenda times move.

**Integrations (I5)**
- [ ] Connect three Google accounts and two Gemini keys, each with a name.
- [ ] The Assistant uses the key picked in its settings.
- [ ] Disconnecting a key in use shows "Used by: Assistant · chat" and still disconnects; the Assistant
      then asks for an account.

**Channels**
- [ ] Link Telegram, send the test message.
- [ ] Quiet hours: turn them on around the current time and see a notification held or dropped as set.

**Agenda**
- [ ] Create a task with only a date late at night (after 21:00): it stays on the chosen day.
- [ ] A browser in another zone (DevTools → Sensors → Location, or the system's zone): the forms show
      and save the account zone's times.
- [ ] Reminder and task alert arriving on Telegram at the right time.
- [ ] Google Calendar: link a calendar; an event created/edited/deleted on each side shows on the other.
      An all-day event from Google stays on its date.
- [ ] Google Tasks: link lists (subtasks and completed ones show up); create/edit/complete/delete on each
      side; a due time and priority set in Pandora survive a title edit in Google; completing a recurring
      task creates the next one in Google; editing the same task on both sides between two cycles keeps
      the newer one and logs a conflict.

**Assistant (web and Telegram)**
- [ ] "lembra de pagar a luz amanhã às 9h", "o que tem pra hoje", "cria a tarefa X toda segunda".
- [ ] Deleting something asks for confirmation; leave it unanswered for 10 minutes: the history shows it
      as expired.
- [ ] Voice message on Telegram becomes a command.

**Finances**
- [ ] Account, card, a card purchase and an installment purchase; the statement closes on the closing day
      (the job runs hourly).
- [ ] Import a real bank file; approve the suggestions in the inbox.
- [ ] A recurring transaction creates its occurrence on the day.
- [ ] Attachment via Telegram lands in the queue.

**Notes**
- [ ] AI actions (summarize, translate, ask your notes) with the real Gemini key.
- [ ] `/nota` on Telegram creates the capture.
- [ ] Agenda and Finances blocks show real data.

**Communications**
- [ ] Connect Gmail and Titan; sync, read, move, send with an attachment, a rule.
- [ ] Note the connection date: about 7 days later, Gmail asks to reconnect (testing mode, expected).

**Files and Desktop**
- [ ] F1's "done when" check on the homelab ([Files product-plan](../modules/files/en/product-plan.md)).

**Admin**
- [ ] A role with a permission denied blocks the action; a feature flag hides its feature.

## 4. Production

- [ ] Generate the `.env.prod` secrets and save them in Bitwarden.
- [ ] `prod-db` up, `pandora` database and role created, `migris apply prod`.
- [ ] Set `VERSION` to `1.0.0` on `main`, push, and wait for the CI to publish `:1.0.0`.
- [ ] Pin `:1.0.0` in `.env.prod`, `pull` and `up -d`.
- [ ] Schedule `homelab-db-backup`, run it once and run `Test-Restore.ps1` (homelab
      `docs/05-backups.md`). Confirm iDrive is watching the folder.
- [ ] Connect the real accounts (Google, Gemini, Telegram, mailboxes).
- [ ] `git tag v1.0.0` on the released commit and push the tag.

## 5. After 1.0

- [ ] The "edit the original `CREATE TABLE`" rule ends: from here on, schema changes are new `ALTER`
      migrations. Update [coding-standards](../architecture/en/coding-standards.md) (en/pt-BR).
- [ ] Every new version: bump `VERSION`, push to `main`, pin the new tag in prod, `migris apply prod`
      before the `up -d` when there are migrations.
