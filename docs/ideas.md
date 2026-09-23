# Ideas Backlog

Candidate features and extensions for Pandora, collected on 2026-09-22. None of this is scheduled;
items graduate into a module's `product-plan.md` when picked up. Items already on a module roadmap
(Google Calendar/Tasks sync, Integrations I3, Assistant A3–A5) are deliberately left out.

**Effort:** S small · M medium · L large. **Status:** idea · planned · in progress · done.

---

## Suggested order

1. **Finances → Channels events** — little code, a notification used every week.
2. **Boleto line + receipt photo → Finances inbox** — makes Telegram the main way to feed Finances.
3. **Balance forecast** — the report that most drives decisions, built from data that already exists.

---

## 1. Wiring existing modules together (highest leverage)

The plumbing (events, Channels, Assistant, the Finances inbox) exists; these connect it.

| Idea | What | Reuses | Effort | Status |
|---|---|---|---|---|
| **Finances → Channels** | Finances' `Contracts` project is empty. Publish `StatementDueSoon`, `StatementOverdue`, `ImportCompleted` + Channels templates: "Nubank statement due in 3 days, R$ 2,340". | Outbox, Channels templates | S | idea |
| **Boleto line → bill to pay** | The *linha digitável* encodes amount and due-date factor — a parser, no AI. Paste on Telegram/web → `PendingTransaction` in the inbox, shown on the Agenda day view. | Finances inbox, Channels C4 inbound | S | idea |
| **Receipt photo → Finances inbox** | Photo via Telegram → Gemini (multimodal) extracts amount/date/merchant → `PendingTransaction`. The inbox is the confirmation step. | Channels C4, `Ai.Chat` Gemini, inbox | M | idea |
| **Finances due dates in the Agenda day view** | Read-only; already listed under Agenda's "Beyond". No sync. | Agenda read GETs | S | idea |
| **Read-only Assistant tools** | "How much did I spend on iFood in September?", "What do I have tomorrow?". No confirmation needed — simpler than the write tools. | Assistant command catalog | M | idea |

## 2. Extending existing modules

### Finances

| Idea | What | Effort | Status |
|---|---|---|---|
| **Subscription detection** | Scan history for same payee + similar amount + ~30-day interval → suggest a recurrence template (`fin010`). Also flag price increases. | M | idea |
| **Categorization from history** | Before the `fin015` rule engine: same normalized description → category of the last approval. Covers most cases; add the rule engine when this falls short. | S | idea |
| **Balance forecast** | Current balance + recurrences + installments + open statements → 60-day projection. "Checking goes negative on the 18th." | M | idea |
| **Installment plans from imports** | Already in the schema (`Origin.Import`), unused. Closes the aggregate's "phase 10". | M | idea |

### Agenda

| Idea | What | Effort | Status |
|---|---|---|---|
| **Weekly review** | Every Sunday via Channels: overdue tasks, the week's events, pending Finances inbox items, notes created. GTD ritual without opening the app. | M | idea |
| **Habits** | Recurrence + check-in via Telegram buttons ("Worked out today? ✅ ❌") + streaks. | M | idea |

### Notes

| Idea | What | Effort | Status |
|---|---|---|---|
| **Daily note tied to Agenda** | One page per day, created on demand, embedding that day's events and tasks. A journal. | S | idea |
| **Cross-module wikilinks** | `[[agenda:event/…]]`, `[[finances:tx/…]]` with backlinks — meeting note shows on the event, purchase note on the transaction. | M | idea |
| **Read later / clipper** | Link sent to Telegram → Notes page with extracted content, tagged `#read`. | S | idea |
| **Spaced repetition** | Mark a note "study" → Channels resurfaces it at 1/3/7/21 days. | M | idea |

### Communications (once Gmail lands)

| Idea | What | Effort | Status |
|---|---|---|---|
| **Triage that routes** | Statement/OFX email → Finances import; invite → Agenda; tracking code → task that closes on delivery. | L | idea |

## 3. New modules

| Module | What | Effort | Status |
|---|---|---|---|
| **People (personal CRM)** | Birthdays, last contact, "reach out every 30 days", `@person` in Notes, "who owes me" in Finances (splitting bills). | M | idea |
| **Documents & assets** | ID card, passport, warranties, contracts, car. The value is **expiry dates** → Agenda alerts ("license expires in 60 days", "car service due"). Reuses Notes attachments. | M | idea |
| **Health** | Medication reminders with stock ("runs out in 5 days"), appointments, lab results (PDF + extracted values over time), weight. | M | idea |
| **Shopping / NFC-e** | Read the NFC-e QR → line items → per-product price history, shopping list via Telegram. Caveat: every state SEFAZ portal differs, hence L. | L | idea |
| **Net worth / investments** | Consolidated positions, dividends, net worth including Finances accounts. Tracking only — no advice, no execution. | L | idea |

## 4. Cross-cutting

| Idea | What | Effort | Status |
|---|---|---|---|
| **Global search** | Each module registers a search provider; Ctrl+K in the client. Could merge with the Assistant command bar (no match → interpret as a command). | M | idea |
| **Day timeline** | "What happened on 2026-08-12": transactions, events, notes, messages. Cheap once global search exists. | S | idea |
| **Export everything** | Markdown/JSON dump of every module. Data ownership + a readable backup. | S | idea |
| **Generic "when X then Y" automations** | **Not yet.** Build 3–4 of the section 1 wirings by hand first; generalize only once the same shape repeats. | — | deferred |
