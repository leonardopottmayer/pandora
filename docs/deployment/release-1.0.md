# Release 1.0

1.0 means **Pandora running in production with real data**. This checklist covers the release, in order,
and the manual script for checking against real accounts what the tests only cover with fakes. Tick each
item as you go.

The pipeline ([releasing](releasing.md)): every push to `dev` builds the `:dev` images, which staging
runs. Merging `dev` into `main` is the release: the bot writes `1.0.0` (Pandora has no earlier tag),
tags `v1.0.0`, and the tag builds `:1.0.0`, which prod pins. So the order is: repository set up →
staging on `:dev` checked → `dev` → `main` → prod.

For now the homelab runs on the everyday PC (homelab repo, `docs/06-move-host.md`): staging's database
listens on `5435` there, not `5433`.

---

## 1. Code (done before the release)

- [x] Dates in the user's zone everywhere (KI-001, KI-003), Finances jobs per user.
- [x] Assistant: an unanswered confirmation reads as `expired` in the history; conversations are kept
      on purpose (see the module's implementation-status).
- [x] KI-002 closed (not needed yet); Gmail testing mode accepted.
- [x] Daily backup and restore check written (repo **homelab**, `docs/05-backups.md`).

## 2. Repository (devkit stage 3)

- [ ] CI, release and Dependabot files reach `dev` through a pull request.
- [ ] Merge and security settings, rulesets on `main` and `dev`
      ([releasing §1](releasing.md#1-merge-security-and-ruleset-settings)).
- [ ] `NUGET_GITHUB_TOKEN` also as a Dependabot secret.
- [ ] Release bot installed on the repository, its variable and secret set, bypass on `main`
      ([releasing §3](releasing.md#3-the-release-bot)).

## 2b. Staging

- [ ] Tier up (homelab repo, guides 01 to 03): networks `staging` and `prod`, `staging-postgres`, `prod-postgres`,
      pgAdmin.
- [ ] `docker login ghcr.io` and the local `migris` config (see [deployment](deployment.md)).
- [ ] Pandora's role and database in `staging-postgres`, then `migris init staging` and
      `migris apply staging -y` ([deployment, steps 2 to 5](deployment.md#creating-an-environment-step-by-step)).
- [ ] `.env.staging` up to `.env.example`: `DATA_PROTECTION_KEY` (required, new secret), images `:dev`,
      the Google client, and a staging pair of Telegram bots with `TELEGRAM_BOT_USERNAME`. Save it in
      Bitwarden.
- [ ] `docker compose --env-file .env.staging pull && docker compose --env-file .env.staging up -d`.
- [ ] Run the script in §3 against `http://<host>:8731`. Fixes go to `dev`; staging pulls `:dev` again.

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

## 4. Release

- [ ] `gh pr create --base main --head dev`; the release preview comment says **v1.0.0**.
- [ ] Merge it with a merge commit. The bot commits `chore(release): 1.0.0`, tags `v1.0.0`, publishes
      the GitHub Release and opens the `main` → `dev` pull request, which merges itself.
- [ ] The tag's `build-images` run publishes `:1.0.0` and `:latest`.

## 4b. Production

- [ ] Brevo account (SMTP) for prod's e-mails.
- [ ] Generate the `.env.prod` secrets, images pinned to `:1.0.0`, SMTP pointing at Brevo,
      `COMPOSE_PROFILES` empty. Save it in Bitwarden.
- [ ] Pandora's role and database in `prod-postgres`, `migris init prod`, `migris apply prod -y`.
- [ ] `pull` and `up -d`.
- [ ] Schedule `homelab-db-backup`, run it once and run `Test-Restore.ps1` (homelab
      `docs/05-backups.md`). Confirm iDrive is watching the folder.
- [ ] Connect the real accounts (Google, Gemini, Telegram, mailboxes).

## 5. After 1.0

- [ ] The "edit the original `CREATE TABLE`" rule ends: from here on, schema changes are new `ALTER`
      migrations. Update [coding-standards](../architecture/en/coding-standards.md) (en/pt-BR).
- [ ] Every new version: release through the `dev` → `main` pull request ([releasing](releasing.md)), pin
      the new tag in prod, `migris apply prod` before the `up -d` when there are migrations.
