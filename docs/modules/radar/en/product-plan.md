# Radar Module — Product Plan

> **Status:** design recorded (2026-11-07); nothing built yet. Delivery runs in steps R1–R5 (§9).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related: [Communications](../../communications/README.md) (newsletters) · [Channels](../../channels/README.md)
> (the digest) · [Assistant](../../assistant/README.md) (the AI client) · [Admin](../../admin/README.md)
> (flags and permissions)

---

## 1. What the module does

**Radar** keeps the user up to date on technology without manual curation. It follows the sources the
user picks (YouTube channels, Medium, Reddit, Hacker News, blogs, GitHub releases, newsletters that
arrive by email), asks an AI model how well each new item matches the user's interests, and delivers the
best ones as a **digest** on Telegram or email and in a feed screen. The user's 👍/👎 on items teaches it
what matters.

```
sources ──► fetch (poll) ──► new items ──► AI score + summary ──► feed screen
   ▲                                              │
   │                                              └──► digest (daily/weekly) ──► Channels ──► Telegram / email
   └──────────────── 👍 / 👎 feedback ◄──────────────────────────────────────────────┘
```

### What it is not

- **Not a full RSS reader.** Items are kept for scoring and the digest, not as an archive: they are
  purged after a while unless saved (§6.4).
- **Not a mail client.** Newsletters stay in [Communications](../../communications/README.md); Radar
  only reads the ones under a label the user picks.
- **Not custom scrapers.** Sources are typed connectors Pandora ships (§4). A user-defined source
  (arbitrary page, selector, API) is a later idea.

---

## 2. Naming and coordinates

| Thing | Value |
|---|---|
| Module name | `Radar` (UI: "Radar") |
| Backend projects | `Pottmayer.Pandora.Modules.Radar.{Abstractions,Contracts,Domain,Application,Infrastructure,Persistence,Presentation}` |
| PostgreSQL schema | `radar` |
| Table prefix | `rdrXXX_`, PK `uuid_generate_v7()` |
| Options section | `Pandora:Radar` |
| API base | `/api/v{version}/radar`, JWT |
| Frontend | `client-web/src/modules/radar`, routes `radar` (feed), `radar/sources`, `radar/settings` |
| Migrations | `migrations/migrations/radar/` |
| Channels category | `radar.digest` |

---

## 3. Principles

1. **Sources are typed connectors.** Each kind of source is one class behind `ISourceConnector` (§4).
   Most are RSS/Atom underneath, so a new one is usually a URL template over the RSS connector.
2. **Cheap before smart.** Duplicates, blocked words and old items are dropped before the AI sees them.
   The AI scores items in batches, under a daily cap, so cost stays predictable.
3. **The AI explains itself.** Every score comes with a one-line reason and a short summary in the
   user's language, so the user can tell why an item was picked.
4. **Feedback is the profile's second half.** The written interest profile says what the user wants; the
   recent 👍/👎 show it. Both go into the scoring prompt.
5. **Delivery belongs to Channels.** Radar asks Channels to notify the user (`NotifyUserRequested`); it
   never talks to Telegram or SMTP itself.
6. **Owned by the user.** Sources, items and feedback are per user, like every other module.

---

## 4. Sources

### 4.1 The contract

`ISourceConnector`, registered in DI and resolved by the source's `kind`:

| Member | What it does |
|---|---|
| `Kind` | The stored kind, hyphenated (`youtube-channel`) |
| `ResolveAsync(input)` | Turns what the user pasted (a URL, a handle, a subreddit) into the source's config and a display name, or a validation error |
| `FetchAsync(source, state)` | Returns the items published since the last fetch, and the new fetch state (ETag, `Last-Modified`, last item id) |

A fetched item carries: external id, canonical URL, title, author, published at, summary or description
(plain text, trimmed), and optional extra data per kind (video duration, points, comments count).

### 4.2 Connectors in the first steps

| Kind | User gives | Underneath | Notes |
|---|---|---|---|
| `rss` | Feed URL, or a site URL with a `<link rel="alternate">` | RSS/Atom | Covers blogs and anything with a feed |
| `youtube-channel` | Channel URL or `@handle` | `youtube.com/feeds/videos.xml?channel_id=` | The channel id is read from the channel page once, at resolve time |
| `medium` | Profile, publication or tag | `medium.com/feed/...` | |
| `reddit` | Subreddit, optional minimum score | `reddit.com/r/<name>/top.rss?t=day` | |
| `github-releases` | `owner/repo` | `github.com/<owner>/<repo>/releases.atom` | |
| `hacker-news` | Minimum points | HN's public API (top stories) | Not RSS; its own connector |
| `newsletter` | A Communications label | `MailMessageReceived` + a body read through Communications | Pushed by the event, not polled (§6.5) |

Planned later: YouTube through the Data API (via Integrations) if the page lookup stops working,
podcasts, X/Bluesky, dev.to, user-defined sources.

### 4.3 Fetching

- A background job polls each active source on its interval (default 60 minutes, minimum 15), with
  `If-None-Match`/`If-Modified-Since` so unchanged feeds cost one 304.
- Items are deduplicated per user by a hash of the canonical URL (tracking parameters removed), so the
  same article from Reddit and from HN is scored once.
- Items older than the source's first fetch are skipped, so following a new source does not flood the
  feed with its archive.
- A source that fails 5 times in a row is paused and flagged in the sources screen with the last error.

---

## 5. Scoring

### 5.1 Inputs

- **Interest profile:** free text written by the user ("backend .NET, arquitetura, PostgreSQL, IA aplicada;
  não me interessa frontend nem cripto"), plus optional **must-have** and **blocked** keywords.
- **Recent feedback:** the titles of the last 20 👍 and 20 👎 items, as examples.
- **The items:** title, source, author and summary, in batches of up to 20 per call.

### 5.2 Output per item

| Field | What |
|---|---|
| `score` | 0–100, how well the item matches the profile |
| `summary` | Two or three sentences in the user's language |
| `reason` | One line: why this score |
| `tags` | Up to 5 topics, for filters in the feed |

The model answers in structured JSON. An item the model fails to score keeps `score = null` and is
retried once on the next run.

### 5.3 The AI client and cost

- Radar uses the same Tars AI chat client the Assistant uses (`IAiChatCompletionClientFactory`, Gemini),
  with its own model setting in `Pandora:Radar:Ai`.
- Before the AI: blocked keywords drop an item (`score = 0`, reason "blocked keyword"); must-have
  keywords mark it for the digest regardless of score.
- Caps in options: items scored per user per day, and items per batch. Past the cap, items wait for the
  next day.

### 5.4 Feedback

The user marks an item 👍 or 👎 in the feed or from the Telegram digest's buttons. Feedback changes no
score already given; it enters the next prompts (§5.1). Later steps can try embeddings and similarity to
👍 items (§7).

---

## 6. Pandora: the Radar module

### 6.1 Data model

| Table | What it holds | Main columns |
|---|---|---|
| `rdr001_source` | A followed source | `id`, `user_id`, `kind`, `name`, `config jsonb`, `interval_minutes`, `status` (`active`/`paused`/`failing`), `fetch_state jsonb`, `last_fetched_at`, `last_error`, `failure_count`, timestamps |
| `rdr002_item` | A fetched item | `id`, `user_id`, `source_id`, `external_id`, `url`, `url_hash`, `title`, `author`, `summary_source`, `published_at`, `extra jsonb`, `score`, `ai_summary`, `ai_reason`, `tags text[]`, `scored_at`, `feedback` (`up`/`down`/null), `saved`, `read_at`, `digested_at`, timestamps |
| `rdr003_preferences` | The user's Radar settings | `user_id` (PK), `interest_profile`, `must_have text[]`, `blocked text[]`, `digest_frequency` (`daily`/`weekly`/`off`), `digest_time`, `digest_weekday`, `digest_min_score`, `digest_max_items`, timestamps |
| `rdr004_digest` | A sent digest | `id`, `user_id`, `sent_at`, `item_ids uuid[]`, `correlation_id` |

Unique `rdr002 (user_id, url_hash)`. Index `rdr002 (user_id, published_at desc)` for the feed and
`rdr002 (user_id, scored_at) where score is null` for the scoring queue.

### 6.2 Jobs

| Job | Runs | Does |
|---|---|---|
| Fetch | Every minute, picks due sources | `FetchAsync`, dedupe, insert new items |
| Score | Every few minutes | Batches unscored items per user, under the daily cap |
| Digest | Every few minutes | For users whose digest time has come (in their time zone), picks unsent items with `score >= digest_min_score` (and must-have ones), newest first, up to `digest_max_items`, and asks Channels to send |
| Purge | Daily | Deletes items older than `RetentionDays` that are not saved and have no feedback |

### 6.3 Digest

- Sent through Channels with `NotifyUserRequested`, category `radar.digest`, template `radar-digest`;
  the user's channel preferences decide Telegram, email or both. Quiet hours apply.
- Each item: number, title (link), source, the AI summary. On Telegram, one row of buttons per item
  (`👍 3` / `👎 3`) whose callback comes back to Radar as feedback.
- Nothing above the threshold: no message (no "nothing today" noise).

### 6.4 Options

```json
"Pandora": {
  "Radar": {
    "Ai": { "Model": "gemini-2.5-flash" },
    "MaxScoredItemsPerDay": 300,
    "BatchSize": 20,
    "MaxSourcesPerUser": 100,
    "MinIntervalMinutes": 15,
    "RetentionDays": 90,
    "FailuresBeforePause": 5
  }
}
```

### 6.5 Newsletters from Communications

- The user picks a Communications label (for example `newsletters`). A `newsletter` source subscribes to
  `MailMessageReceived` and keeps messages carrying that label.
- The event has no body, so Communications needs a small port in `Communications.Abstractions`
  (`IMailMessageReader.GetTextAsync(userId, messageId)`) returning the plain text of a message. Radar
  splits a newsletter into its links when it lists several (one item per link with its paragraph), or
  keeps it as one item otherwise.
- Building the port is part of R4, in Communications.

### 6.6 Flags and permissions

Declared in `Radar.Abstractions` as constants (`RadarFeatures`, `RadarPermissions`):

| Kind | Key | Guards |
|---|---|---|
| Feature | `radar` | The whole module |
| Feature | `radar.ai` | Scoring (off: items arrive unscored, the digest sends the newest) |
| Feature | `radar.digest` | The digest |
| Permission | `radar.sources.{read,create,update,delete}` | Sources |
| Permission | `radar.items.{read,update}` | The feed; feedback, saved and read |
| Permission | `radar.preferences.{read,update}` | Profile and digest settings |

The default `user` role gets all of them.

### 6.7 API (outline)

| Method | Route | What |
|---|---|---|
| `POST` | `/radar/sources/resolve` | What the user pasted → kind, config and name, before saving |
| `GET/POST` | `/radar/sources` | List, follow a source |
| `PUT/DELETE` | `/radar/sources/{id}` | Change (name, interval, pause), unfollow |
| `POST` | `/radar/sources/{id}/fetch` | Fetch now |
| `GET` | `/radar/items` | The feed: filters by source, tag, minimum score, unread, saved; paged |
| `PATCH` | `/radar/items/{id}` | Feedback, saved, read |
| `GET/PUT` | `/radar/preferences` | Profile, keywords, digest settings |
| `POST` | `/radar/digest/preview` | The digest that would go out now, without sending |

### 6.8 Frontend

- **Feed:** cards sorted by score or date, with source icon, title, AI summary, reason on hover, tags,
  👍/👎, save, open. Filters at the top; "show low scores" off by default.
- **Sources:** a table with kind, name, status, last fetch and error; "follow" opens a dialog that
  resolves the pasted URL first and shows what was found.
- **Settings:** interest profile (textarea), must-have and blocked keywords, digest frequency, time,
  threshold and size, and "preview digest".

---

## 7. Extension points

| Point | How it extends | Example |
|---|---|---|
| `ISourceConnector` | A new class, often a URL template over `rss` | Podcasts, dev.to, Bluesky, a user-defined source |
| Scoring | Another scorer behind the same output (§5.2) | Embeddings and similarity to 👍 items, cheaper than a prompt |
| Item enrichment | A step between fetch and scoring | Full article text, YouTube transcripts |
| Digest | Another template or channel through Channels | A weekly "best of", a per-topic digest |
| Assistant tools | Tools over the Application queries | "o que saiu de novo sobre .NET essa semana?" |
| Integration event | `RadarItemSaved`, once a consumer exists | Saving an item as a page in Notes |

---

## 8. Decisions

| # | Decision | Chosen | Over |
|---|---|---|---|
| 1 | Sources | Typed connectors Pandora ships, most over RSS | User-defined sources now |
| 2 | Order | After Communications' email (done in M1–M4); newsletters through its event | Radar reading mailboxes itself |
| 3 | Relevance | AI prompt with profile + recent feedback, batched, under a daily cap | Keyword rules only; embeddings first |
| 4 | Content scored | Feed title and summary | Fetching every full article (later, §7) |
| 5 | Items | Per user, deduplicated by canonical URL | Shared items across users |
| 6 | Delivery | Digest through Channels on a schedule, plus the feed screen | One notification per item |
| 7 | Retention | 90 days unless saved or rated | Keeping everything |
| 8 | Name | `Radar` | — |

> ponytail: items are per user, so two users following the same feed fetch it twice; share fetched
> items across users only if Pandora ever has many users on the same sources.

---

## 9. Delivery

| Step | Delivers | Done when |
|---|---|---|
| **R1** | Sources (`rss`, `youtube-channel`, `medium`, `reddit`, `github-releases`), resolve, fetch job, dedupe, the feed and sources screens, flags and permissions | Following the five example YouTube channels and a blog fills the feed with only new items |
| **R2** | Scoring with the interest profile, keywords, feedback in the feed, the daily cap | Items show score, summary and reason; 👎 items stop showing up near the top after a few days |
| **R3** | The digest through Channels, Telegram buttons as feedback, preview | A daily digest arrives on Telegram at the chosen time and its buttons record feedback |
| **R4** | `newsletter` through Communications (the reader port), `hacker-news` | A newsletter under the label becomes items in the next digest |
| **R5+** | Full article text, transcripts, embeddings, more connectors, Assistant tools, user-defined sources | One at a time, each with its own plan |
