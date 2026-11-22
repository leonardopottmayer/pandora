# AI

[← Back to index](../README.md) · Related: [Search](search.md), [Assistant](../../assistant/README.md)

Notes calls a model only through the [Assistant](../../assistant/README.md): `ITextCompletion` in
`Assistant.Abstractions` uses the user's Assistant profile, model and Integrations key. Every AI action
needs the Notes permission for the page, `assistant.commands.run`, both modules enabled and an enabled
Assistant profile. Nothing is generated on its own: the user asks, sees a preview, and accepts or
discards.

---

## 1. Text actions

- **Summarize, improve, translate** from the selection bar; **Continue with AI** from the `/` menu,
  using the last 8,000 characters before the cursor. A selection is limited to 8,000 characters.
- `POST /notes/ai/transform { action, text, language?, context? }`.
- A preview shows the result before anything changes: replace the selection, insert below, or discard.
  Accepting needs the document to still match the preview. The result enters as plain text.
- **Attribution:** the page PUT accepts `source: edit | assistant`. The first autosave after accepting
  marks the changed blocks and the version as `assistant`; later manual saves are `edit` again.
- Provider failures, timeouts, empty answers and answers over 16,000 characters become friendly errors
  without provider details. Closing the preview cancels the request.

## 2. Page summary

"Summarize page with AI" in the page menu flushes pending edits, then
`POST /notes/pages/{id}/ai/summary` summarizes the page's **saved** derived text (tables, properties and
chips included). Empty text or more than 32,000 characters is refused without calling the provider.
Accepting inserts a "Summary" info callout at the top. Only this page's text is sent; linked pages are
not read.

## 3. Ask your notes

A tab beside the page search takes a question of up to 1,000 characters, in the current space or all
of them (`POST /notes/ai/ask`).

1. `NoteQuestionSearch` drops common Portuguese and English question words and builds an **OR**
   `tsquery` over the existing search vector. Ordinary search is unchanged.
2. At most **five** pages (archived included, deleted excluded) contribute passages of up to 4,000
   characters, centred on a matching word.
3. The titles and passages go to the model, which must answer `{ answer, pageIds }`.
4. `NoteAnswerParser` refuses malformed answers, answers without citations, and citations outside the
   pages sent. No hits, or an explicit "not enough information", shows a localized message.

The answer is plain text with buttons to open the cited pages and see their passages. The lexical
search has no synonyms or semantics; the Assistant's `ask_notes` tool widens it with words the model
adds (§6). Nothing is written.

## 4. What leaves Pandora

| Action | Sent to the provider |
|---|---|
| Text actions | The selection, or the 8,000 characters before the cursor |
| Page summary | That page's derived text |
| Ask your notes | The question and passages of up to five pages |
| AI block | The block's prompt only |

## 5. AI block

`/AI block` inserts an `ai` block storing `{ prompt, output, generatedAt }`.

- `POST /notes/ai/generate { prompt }` (1–8,000 characters) sends only the prompt, with no page
  context, and returns plain text with the server's UTC time.
- The previous output stays while a new one is previewed; accepting replaces it as an
  Assistant-origin edit, and discarding leaves it alone. Accepting checks that the block is unchanged.
- `output` is a plain-text inline list, so search, exports, readable text and version diffs include it.
- Read-only and public views show the stored prompt and result, without generating.

## 6. Assistant tools

Notes contributes five tools to the [Assistant](../../assistant/README.md) (`Application/Assistant`):

| Tool | Does |
|---|---|
| `create_note` | Creates a page from text (one block per line; `#`, `-`, `1.` line starts become headings and lists), optionally in a named space |
| `search_notes` | Lists matching pages, numbered, so "open 2" works next |
| `read_note` | Reads a page back as text |
| `append_to_note` | Appends text to a page |
| `ask_notes` | Answers a question from the notes (§3), with `search_terms` the model adds to widen the search |

## 7. Telegram capture

`/nota <text>` on the linked Assistant bot appends one literal paragraph to a dedicated **Capture**
page, created in the default space when needed.

- No model call: it needs no AI profile or key, only the Assistant and Notes permissions.
- Each paragraph starts with the receipt date and time in the account's zone. Text stays literal:
  newlines are kept, and Markdown-looking lines do not become headings or commands.
- `nte015_note_capture` keeps the inbound event id, written in the same transaction as the page, so a
  redelivered event appends nothing even after the page or the paragraph is removed.
- Renaming, moving or archiving the Capture page keeps it in use; a deleted one is replaced. A page with
  the same title is never adopted. A locked Capture page refuses the capture.
