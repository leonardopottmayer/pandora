# Communications — Execution Plan, M3 (Send)

> **Status:** In progress: decisions approved, steps 1–2 built. It is phase **M3** of the [product-plan](product-plan.md); what exists today is in
> [Implementation Status](implementation-status.md).
> 🇧🇷 [Versão em português](../pt-BR/execution-plan-m3.md)

This doc is the work list. Section numbers (§) point to the [product-plan](product-plan.md).

**M3 delivers:** compose, reply, reply all and forward, with attachments, from any connected mailbox. No server-side drafts, no undo, no
rules (M4).

**Done when** (§15): a reply sent from Pandora lands in the same Gmail conversation, and a message sent from Titan appears exactly once in its
Sent folder.

**Sending cannot be undone,** unlike everything M2 does. A wrong retry or a double click reaches another person's inbox. That shapes the decisions
below.

---

## What already exists (do not rebuild)

| Piece | Where | What M3 uses |
|---|---|---|
| Scope | `gmail.modify`, already granted | Google's page for `messages.send` accepts it: **no new consent**, contrary to what the earlier plans said |
| SMTP servers and password | `com001`, Integrations' `GetPasswordAsync` | The same account as IMAP |
| MIME | MimeKit, in Application (`MimeReader`) | Builds the outgoing message and reads the original for a reply |
| Raw message | `GetRawMessageAsync` | Reply and forward read the original's `Reply-To`, `References` and attachments, which the mirror does not keep |
| Labels + changes step | `MailboxChangesStep` | Brings the sent copy into the mirror right after sending |
| Markdown | `marked` + `dompurify`, in `client-web` | Turns the composed text into the HTML part |
| File upload | `IFormFile`, as in Notes and Finances | The attachments |

---

## Locked decisions

Approved on 2026-10-31.

| Decision | Value | Reason |
|---|---|---|
| Retrying a send | **Only when the provider certainly refused:** `429` and a rate-limit `403`. Never on `5xx`, a timeout or a dropped connection | Those are ambiguous: the mail may have gone. The error says so: "it may have been sent, check Sent" |
| Double send | **A send key:** the form generates a Message-ID when it opens, and the server refuses a key it has already handled (`409 AlreadySent`) | Covers a double click, a retry after a timeout and two tabs. Rejected: disabling the button only, which covers none of the last two. `ponytail:` keys in process memory for an hour; a restart forgets them |
| Sent copy on IMAP | **Detect:** after SMTP accepts, look for the Message-ID in the `sent` folder, twice with a pause; `APPEND` only if it is not there | Works on any server with no setting, and answers the open question about Titan by itself. Rejected: a per-provider flag, which someone has to know and keep right. The risk: a server that saves later than the wait gives a duplicate, which the real check on Titan looks for |
| A copy that fails | The send **succeeded**: `200` with `copySaved: false`, and the screen warns | The mail has gone. An error would invite sending it again |
| Editor | **A text area, written in Markdown**, with a preview. The message goes as HTML + plain text | No new dependency, and plain text just works. Rejected: Notes' `MarkdownEditor`, tied to pages, tags and Notes' attachments; a rich text editor, a new dependency |
| Quoted text | **Plain text**, `> ` per line for a reply, a header block for a forward | The original's HTML came from someone else, and sending it on needs its own sanitizing and its `cid:` images. `ponytail:` keep the formatting if plain text proves too poor |
| Forward's attachments | The original's files come along, each removable | What a forward is expected to do. Read from the raw message |
| Limits | **25 MB** per message including attachments, **100 recipients** | Gmail's limit as the plan has it (§9, to confirm in step 4). Our cap is below Gmail's 500 |
| Who it goes to in a reply | The server works it out from the raw message: `Reply-To`, else `From`; reply all adds `To` and `Cc`, without the mailbox's own address | The mirror has no `Reply-To`, and getting this wrong sends mail to the wrong person |
| Threading | `In-Reply-To` and `References` from the original; on Gmail also `threadId`, and the subject must match the original's (a `Re: ` prefix is fine) | Google's three conditions for joining a conversation |
| Drafts | In the browser, per mailbox and per message replied to; cleared on send | §9. Nothing is sent to the provider |
| Confirmation | None to send, as in any mail client. A warning for an empty subject or a body with no text | A confirmation on every send trains people to click through it |
| Real-world tests | **Only to your own addresses**, between your Gmail and your Titan | Nothing here reaches anyone else |

---

## Found while planning

- **Sent mail published `MailMessageReceived`.** A new message in Sent or Drafts went through the same arrival check, and only a copy with the same Message-ID in the mailbox stopped it. Reproduced by a
  test and fixed in step 1. Nothing consumed the event, but M3 would have made it routine.
- **Quotas can cost money.** Google's page (updated on 2026-10-16) plans charges "later in 2026", with 90 days' notice, for going over **80,000,000 units per project per day**.
  `messages.send` costs 100. One person is far from it: the 64,000-message backfill costs about 1.3 million. It matters for scale: that threshold is about 60 such backfills in a day.

---

## Steps

Each step ends green before the next one. Suggested commit points: **M3a** = step 1, **M3b** = steps 2–5, **M3c** = steps 6–7. Commits only when asked.

### Step 1 — No arrival for sent mail ✅

> **Built (2026-10-31).** As listed; the old engine published 2 events in the new test. One limit, marked `ponytail:` in the code: mail sent to yourself has no arrival if its sent copy comes a cycle before
> the inbox's, since the Message-ID is already there. Test: `Mail_in_sent_or_drafts_is_not_an_arrival`.

- **First, a failing test:** a new message under a label with role `sent` or `drafts` → no `MailMessageReceived`.
- `MailboxChangesStep`: an upsert whose labels are only `sent` or `drafts` is not an arrival.

### Step 2 — The port and the message ✅

> **Built (2026-10-31).** `SendAsync` on the port, with both adapters answering `ActionNotSupported` until steps 3 and 4; `MailErrors`; `MessageComposer`, with `Build` and `DraftFrom`. Beyond the list below:
> the draft gives the original's text as it is, and the screen quotes it, in the user's language. Replying to your own message continues to the same people. A forward's files leave out inline images. Tests:
> `MessageComposerTests`.

- `IMailboxProvider.SendAsync(mailbox, rawMessage, threadKey?)` → `SentMessage(CopySaved)`. Raw bytes, so MimeKit stays out of the Domain; each adapter loads or forwards them.
- `MailErrors`: `SendUncertain` (it may have gone), `AlreadySent` (409), `RecipientsInvalid`, `MessageTooLarge`.
- `Application/Mime/MessageComposer`: builds the `MimeMessage` from the input (from, to, cc, bcc, subject, text, HTML, attachments, Message-ID, `In-Reply-To`, `References`); and
  from a raw original, works out recipients, subject, quoted text and attachments for reply, reply all and forward.
- **Tests (unit):** `Reply-To` wins over `From`; reply all leaves out the mailbox's own address, in any case; `Re: ` is not doubled; `References` grows by the original's Message-ID; a header with a line break
  cannot inject another header; the forward carries the files.

### Step 3 — IMAP: SMTP and the sent copy

- `ImapMailboxProvider.SendAsync`: MailKit `SmtpClient`, `SecureSocketOptions.Auto`, the same credential. Bcc goes on the envelope, not on the wire, and stays in the sent copy.
- Before the server has accepted, any failure is a plain error. Once data has been sent and the answer is lost → `SendUncertain`. Refused recipients are a plain error.
- Sent copy as decided: search by `HEADER Message-ID` in the folder with role `sent`, twice with a pause, then `APPEND` with `\Seen`. No `sent` folder → `CopySaved: false`.
- **Tests (GreenMail):** received by the recipient; one copy in a folder set up as sent (GreenMail has no SPECIAL-USE, so the folder finding needs a seam, or stays for the real check); a wrong password →
  `MailboxAuthFailed`. A copy already there is not added again.

### Step 4 — Gmail send

- `messages.send` with the multipart upload, carrying `threadId` as metadata and the raw message as `message/rfc822`. **Confirm against Google's upload guide** before relying on it, along with the size limit,
  which the method's page does not state.
- `SendAsync` in the adapter gets a parameter for what may be retried: here only `429` and a rate-limit `403`. Everything else ambiguous → `SendUncertain`. Gmail keeps its own sent copy: `CopySaved: true`.
- **Tests (canned answers):** the URL, the parts and `threadId`; a `500` makes **exactly one** request and is `SendUncertain`; a `429` is retried; a timeout is `SendUncertain`.

### Step 5 — Application and API

- `SendMessageCommand`: checks the mailbox is the caller's, the addresses, the limits and the send key; builds; sends **without the action gate**, since a slow SMTP must not hold actions back; then runs the labels +
  changes step, whose failure is only logged.
- `GetComposeDraftQuery`: for a reply, reply all or forward, the prefilled form.
- API:
  - `POST messages/send`, multipart: the fields, `sendKey`, `replyToId?` or `forwardOfId?`, `files`. Answers `{ copySaved }`. `[RequestSizeLimit]` at the limit.
  - `GET messages/{id}/compose?mode=reply|reply-all|forward`.
  - `GET messages/{id}/attachments/{attachmentId}` already serves what the forward shows.
- **Tests (fake provider):** another user's mailbox or original → 404 and no send; the same key twice → `409` and **one** send; concurrent sends with one key → one send; bad address → 422 and no send; too large → 422; a failed
  copy is a `200`; `SendUncertain` keeps the key, so a retry is refused.

### Step 6 — Web

- **`ComposeDrawer`:** from (which mailbox), to, cc, bcc, subject, Markdown with a preview, attachments; a new send key each time it opens. `marked` + `DOMPurify` for the HTML part.
- "New message" on `MailPage`; "Reply", "Reply all" and "Forward" in `MessageReader`.
- **Failures:** the form stays with its content. `SendUncertain` shows its own message and does not offer to send again as if nothing happened. `copySaved: false` is a warning.
- **Draft:** `localStorage`, in `try/catch`, cleared on send.
- i18n `mail.compose.*` in `en.json` and `pt-BR.json`.
- **Tests:** a double click sends once; a failure keeps the content; `SendUncertain` is shown as such; the HTML part has no script; the draft comes back after closing and is gone after sending.

### Step 7 — Real-world check and docs

**Only to your own addresses.**

- **Gmail to Titan and Titan to Gmail:** received once, with text, HTML and an attachment; it appears in Sent.
- **Titan:** **exactly one** copy in Sent. Look at the webmail, and at it again after a few minutes, for a server that saves late.
- **Reply on Gmail:** it joins the same conversation, on both sides. **Reply on Titan:** the Gmail side threads it.
- **Forward with an attachment**, and removing one.
- **Reply to a message with a `Reply-To`** goes to that address.
- **Bcc** does not show to the recipient.
- **Double click** on send: one message.
- **Docs:** `overview`, `architecture`, `api-reference`, `implementation-status`, en + pt-BR; the product plan keeps only M4; a **Built** note on each step here.

---

## Known limits

- **A restart forgets the send keys,** so a retry right after one can send twice.
- **The copy check can be fooled** by a server that saves later than the wait.
- **Quoted text loses the formatting** of the original.
- **Inline images** in the composed message are not supported; they go as attachments.
- **No undo send, no scheduled send, no signature per mailbox** (§15, later).
