# Files Module — Product Plan

> **Status:** Plan. Nothing is built. Files starts after [Pandora Desktop](../../../architecture/en/desktop-client.md)
> phases D1 (shell) and D2 (device credentials), which it depends on.
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related: [Pandora Desktop](../../../architecture/en/desktop-client.md) ·
> [Identity](../../identity/README.md) · [Assistant](../../assistant/en/product-plan.md) ·
> [Notes](../../notes/README.md)

---

## 1. What the module does

**Files** is a catalog of everything on the user's disks: movies and series, photos, college
material, books, manuals, PDFs. It answers "what do I have, and where is it?" without the user
browsing 20 TB of folders.

- **Any number of devices** report into one catalog — a Windows PC today; Linux, macOS, a headless
  server and phones later. Each runs an **agent** (on the desktop, the `Desktop.Files` module of
  [Pandora Desktop](../../../architecture/en/desktop-client.md)).
- The user decides **exactly what is cataloged**, per device, per disk, per folder: pick folders in a
  tree, exclude a subfolder, re-include one deeper down, and layer **filters** by extension, glob,
  prefix, suffix, "contains" or regex.
- The backend keeps the **catalog**: every file and folder with its path, size, dates, kind, and
  whether it is still there. It notices new files, changed files, files that disappeared, and files
  that were **moved or renamed** (keeping what the user attached to them).
- The web lets the user **browse** and **search** from any device, without the disk being reachable.
- Later phases add **metadata** (EXIF, PDF title, video duration), **user tags**, and
  **AI-suggested classification**.

### What it is not

- **Not a sync or backup tool.** The cloud copy (iDrive today) is outside Pandora; the module does
  not know it exists. Switching backup provider changes nothing here.
- **Not a file server.** The backend never holds the files' bytes. Opening a file works only on the
  device that has it (F1 reveals it in the file explorer); serving or streaming files to other devices
  is a separate, later decision.
- **Not a file manager.** The agent **never writes to the disk** — no delete, move or rename. The
  catalog follows the disk; it never drives it.

---

## 2. Naming and coordinates

| Thing | Value |
|---|---|
| Backend projects | `Pottmayer.Pandora.Modules.Files.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}` |
| Shared with agents | `Pottmayer.Pandora.Modules.Files.Agent` — protocol DTOs + the selection/filter engine (4.3), `net10.0`, no server dependencies |
| PostgreSQL schema | `files` |
| Table prefix | `filXXX_`, PK `uuid_generate_v7()` |
| API base (user) | `/api/v{version}/files` — JWT, the usual user scope |
| API base (agent) | `/api/v{version}/files/agent` — device key with scope `files.agent` only |
| Frontend | `client-web/src/modules/files` |
| Desktop module | `client-desktop/Pottmayer.Pandora.Desktop.Files` |
| Migrations | `migrations/migrations/files/` |

---

## 3. Principles

1. **The disk is the source of truth.** The catalog is a mirror that can be rebuilt by scanning
   again. What Pandora genuinely owns is only what the user adds on top — tags, notes,
   classifications. *(F1)*
2. **The agent is read-only.** It lists, stats and reads the first and last bytes of files. It never
   modifies anything on the disk. *(F2)*
3. **Nothing leaves the catalog on its own.** A file is marked *missing* only by a **completed** scan
   of a **reachable** root; one that a config change leaves out is marked *excluded*. Either way it
   keeps its row and metadata, and leaves the catalog **only when the user approves it** in the review
   inbox (4.7). *(F3)*
4. **Identity survives a move.** A **fingerprint** (size + hash of the first and last 64 KiB) lets
   the catalog recognize a renamed or moved file and keep its id, tags and history. *(F4)*
5. **The backend decides, the agent reports.** The catalog and the configuration live in the
   backend. The agent keeps no local database: it fetches its configuration, sends what it sees and
   answers the backend's questions. *(F5)*
6. **The user decides what is cataloged.** Devices, roots, folder selection and filters are all the
   user's, at every level, and nothing is cataloged that was not selected. Defaults are ordinary,
   editable settings. *(F6)*
7. **Platform-neutral protocol.** The backend never assumes Windows. An agent is anything that speaks
   the agent protocol; paths are normalized (4.8) so a Linux agent and a Windows agent produce
   comparable catalogs. *(F7)*
8. **Opt-in twice.** The account switch (`fil004_preferences.is_enabled`) and the device switch both
   start off — see [the two switches](../../../architecture/en/desktop-client.md#44-the-two-switches). *(F8)*
9. **Built for millions of files.** The size of the collection is unknown but large. Every list is
   paginated or scoped to one folder, nothing loads a whole tree, and the first scan reports the real
   numbers. *(F9)*

---

## 4. Architecture

```
 devices (any number, any platform)                 server
┌──────────────────────────────┐            ┌──────────────────────────────────┐
│ Pandora Desktop (or headless)│            │ Files module                     │
│  Desktop.Files               │  X-Api-Key │  agent endpoints  /files/agent/* │
│   config pull                │ ─────────► │   config, scans, batches         │
│   walker + selection/filters │            │   diff against the catalog       │
│   fingerprinter (on request) │ ◄───────── │   "fingerprint these"            │
│   bridge: files.*            │            │                                  │
└──────────────┬───────────────┘            │  user endpoints   /files/*       │
               │ bridge                     │   roots, selection, filters,     │
┌──────────────▼───────────────┐   JWT      │   browse, search, review, prefs  │
│ client-web (inside the app   │ ─────────► │                                  │
│ or in any browser)           │            │  fil001–fil006                   │
└──────────────────────────────┘            └──────────────────────────────────┘
```

### 4.1 Vocabulary

| Term | Meaning |
|---|---|
| **Device** | A paired agent install (Identity, phase D2), with its platform. One user, many devices. |
| **Root** | A top-level location on a device — a whole disk (`E:\`, `/mnt/hd`) or a folder. Identified by `(device, local path)`. |
| **Selection** | Per root, which folders are in: a set of include/exclude marks on paths, the deepest mark wins (4.3). |
| **Filter** | A name rule layered on the selection: include-only or exclude, by extension, glob, prefix, suffix, contains or regex, scoped to the user, a device, a root or a folder (4.3). |
| **Entry** | One file or folder under a root, by its path relative to the root. |
| **Scan** | One full walk of one root, from start to *completed* (applied), *aborted* (discarded) or *held* (waiting for the user, see 4.4). |
| **Fingerprint** | `size + SHA-256(first 64 KiB ‖ last 64 KiB)`. Cheap enough for 20 TB, strong enough to recognize the same file at another path. Not a full-content hash. |
| **Missing** | An entry a completed scan did not see. Kept until the user reviews it. |
| **Excluded** | An entry the current selection or filters leave out, after a config change. Kept until the user reviews it. |
| **Review inbox** | Where missing and excluded entries wait for the user's decision, shown as a folder tree (4.7). |

### 4.2 Devices and roots

A user has any number of devices, each with any number of roots. All of it is **configuration stored
on the server**, edited in `client-web`, and pulled by the agent (`GET /files/agent/config`) at the
start of every scan. Keeping it on the server is what lets a headless agent (no screen) and a phone
be configured the same way as the desktop, and lets the user see and edit every device's setup from
anywhere.

- **Adding a root.** On the device itself, the page offers the native folder picker
  (`files.pickFolder`). Anywhere else, the path is typed; the agent validates it on its next scan and
  aborts with `root-unavailable` if it does not exist, which the page shows.
- **Per-root settings:** name, scan schedule (daily at a time, or manual only), include hidden/system
  files (off by default), case sensitivity (defaults by platform, see 4.8).
- **Device view:** each device with its platform, last time it was seen, its roots, their last
  completed scan and entry counts.
- **Removing a root** stops scanning it; its entries become *excluded* and go to the review inbox.

### 4.3 Selection and filters

The customization has two layers. **Selection** answers "which folders?"; **filters** answer "which
names, inside them?".

**Selection — a checkbox tree.** Per root, a set of marks `(path, include | exclude)`, where `/` is
the root itself. A folder takes the mark of its **deepest marked ancestor** (or its own mark); with no
mark at all, it is included. That is the same model as the folder tree of a backup tool, and it
covers every shape without ordering rules:

| Want | Root | Marks |
|---|---|---|
| Disk 1: folders A and B, not C | `D:\` | `/` exclude · `/A` include · `/B` include |
| Disk 2: A, B, and only `Sub` inside C | `E:\` | `/C` exclude · `/C/Sub` include |
| A whole disk except the system folders | `C:\` | `/Windows` exclude · `/Program Files` exclude |

The walker **prunes**: it does not descend into an excluded folder unless some mark below it includes
something, so an excluded branch costs nothing to scan. On the device, the tree is drawn from the live
disk through the bridge (`files.listFolders`); elsewhere, from what the catalog already knows plus
typed paths.

**Filters — name rules on top of the selection.** Each filter has:

| Field | Values |
|---|---|
| action | `include` (only matching files are kept) · `exclude` (matching items are dropped) |
| applies to | `file` · `folder` (folder filters only exclude, and prune: `node_modules`, `.git`) |
| matcher | `extension` (`mkv, mp4, avi`) · `glob` (`*.part`, `**/backup/**`) · `starts-with` · `ends-with` · `contains` · `regex` |
| scope | the whole user · one device · one root · one folder of a root (inherited by everything below) |
| case sensitive | yes / no / the root's default |
| enabled | on / off, without deleting it |

A file is cataloged when (1) its folder is selected, (2) if any `include` file filter is in scope,
it matches at least one of them, and (3) it matches **no** `exclude` filter in scope. **Exclude always
wins.** There is no rule order to reason about.

**Defaults are filters.** A new user gets a seeded set of exclude filters at user scope — `Thumbs.db`,
`desktop.ini`, `.DS_Store`, `$RECYCLE.BIN`, `System Volume Information`, `*.tmp`, `*.part` — marked
as built-in but otherwise ordinary: editable, disableable, deletable.

**Preview before saving.** Editing a filter shows what it would match **now**, evaluated by the
backend against the catalog — a count and a sample — so a regex can be tried without waiting for a
scan. Regexes are validated on save and run with a timeout (a pathological pattern cannot hang a
scan).

**Changes apply on the next scan.** The agent fetches the config at each scan start; "Scan now"
right after an edit applies it immediately. Entries the new config leaves out become *excluded* (not
deleted — F3); entries it brings in appear as new.

**One engine.** Selection and filter evaluation live in `Files.Agent`, used by both the backend (for
the preview) and the .NET agents, so the preview and the scan can never disagree. Agents in other
languages (a phone) implement the same rules against a set of test vectors kept with the docs.

### 4.4 Scan protocol

```
0. GET  /files/agent/config                → this device's roots, selection and filters

1. POST /files/agent/scans                {rootId}                       → {scanId}
      409 if that root already has a running scan.

2. POST /files/agent/scans/{id}/batches   {entries: [{path, kind, size, modifiedAt}]}   (≤ 1000)
      → {needsFingerprint: [path, ...]}
      The backend matches each path against the catalog for that root:
        same size + modifiedAt  → seen, nothing to do
        new or changed          → asks for a fingerprint

3. POST /files/agent/scans/{id}/batches   {entries: [{path, ..., fingerprint}]}
      The answers to step 2, as more batches.

4. POST /files/agent/scans/{id}/complete  {entriesSeen}
   or  POST /files/agent/scans/{id}/abort {reason}      (e.g. root-unavailable)
```

The agent only sends what the selection and filters let through. On **complete**, inside one
transaction:

1. **Moves.** Entries of this root not seen in this scan are candidates for *missing*. A candidate
   whose fingerprint matches **exactly one** entry created since this root's previous completed scan
   (same user, any root, any device — so a move between two roots counts too) was moved: the old row
   takes the new path and root, keeping its id and metadata, and the newer row is dropped. Ambiguous
   matches (duplicates) are not merged — they stay *new + missing*, which is safe.
2. **Missing or excluded.** Each remaining candidate is checked against the config the scan ran with:
   if the config leaves it out, it becomes `excluded`; otherwise `missing`. Both get `missing_since =
   now` and go to the review inbox. An entry seen again goes back to `present`.
3. **Safety brake.** If the scan would mark more than **20 %** of the root missing (a disk that came up
   empty, a wrong drive letter), the scan goes to `held` instead of being applied. The web shows it;
   the user **confirms** (apply) or **discards** it. Exclusions caused by a config change do not count
   towards the brake — the user asked for them.

On **abort**, or if the agent stops sending batches for longer than a timeout, the scan is discarded:
nothing is marked missing. Entries created by the scan stay (they are real files that were seen).

Folders are entries with `kind = directory`. They carry no fingerprint; a moved folder shows up as
new folders plus moved files, which is enough because folders carry no user metadata in F1.

**Cost of the first scan.** Every file needs a fingerprint the first time — about 128 KiB read per
file. With millions of files on spinning disks, that is many hours, once. Later scans only
fingerprint what is new or changed.

### 4.5 Scheduling

Each root has its own schedule — daily at a chosen time (the default), or manual only — plus **"Scan
now"** from the page (`files.scanNow`), with progress pushed to the page (`files.scanProgress`).
Real-time watching is out of F1: a daily scan matches how this collection changes.

### 4.6 Browsing and search

- **Browse:** `GET /files/roots/{id}/entries?parentPath=` — the children of one folder, paginated,
  folders first.
- **Search:** `GET /files/search?q=&deviceId=&rootId=&category=&minSize=&maxSize=&modifiedFrom=&modifiedTo=&status=`.
  Name matching uses a **trigram index** (`pg_trgm`, new to Pandora) on `name`, because file names
  are matched by fragments (`breaking bad s02`, `calculo_2`), not by words.
- **Category** is derived from the extension by the domain (`FileCategory.FromExtension`): `video`,
  `audio`, `image`, `document`, `ebook`, `archive`, `code`, `other`. Stored for filtering.
- **Reveal:** on the device that owns the root, a result offers *Show in Explorer/Finder*
  (`files.reveal`). Elsewhere, the path is shown and copyable.

### 4.7 Review inbox

Missing and excluded entries wait in a **review inbox**, the same idea as the
[Finances inbox](../../finances/en/recurrences-and-inbox.md): the system proposes, the user approves.

- **Shown as a tree.** The inbox is the folder tree of each root, pruned to the branches that hold
  entries to review, each folder with the count under it and the reason (*missing* / *excluded*).
  Folders load lazily on expand, so a root with millions of entries never loads at once. A whole
  subtree gone reads as one branch, not as thousands of lines.
- **Two decisions, on an entry or on a whole folder** (which applies to everything under review below
  it):
  - **Forget** — the entry leaves the catalog, with what the user attached to it. This is the only
    path by which an entry is ever removed.
  - **Keep** — it stays in the catalog as missing/excluded and leaves the inbox (e.g. a file on an
    external disk that is unplugged on purpose). It can still be found by search with the status
    filter.
- An entry that reappears, is matched as a move, or is brought back by a config change leaves the
  inbox by itself.

### 4.8 Paths across platforms

- `relative_path` is stored with `/` separators and in Unicode **NFC** (macOS file systems hand out
  NFD; without normalizing, `Ação` from a Mac and from Windows would be two different names).
- `local_path` of a root is stored as the device gives it (`E:\`, `/mnt/hd`, `/Volumes/Fotos`).
- **Case sensitivity** is a per-root setting, defaulting by platform: insensitive on Windows and
  macOS, sensitive on Linux. It drives path matching, selection marks and filters' default.

---

## 5. Data model (draft)

Enum values use hyphens; columns are snake_case. No FK leaves the `files` schema — `user_id` and
`device_id` reference Identity only logically.

**`fil001_root`** — `id`, `user_id`, `device_id`, `name`, `local_path`, `kind` (`folder`; later
`media-library` for phones), `case_sensitive`, `include_hidden`, `scan_time` (null = manual only),
`status` (`active` | `removed`), `last_completed_scan_at`, `entry_count`, `created_at/by`,
`updated_at/by`. Unique `(device_id, local_path)`.

**`fil002_entry`** — `id`, `user_id`, `root_id` → fil001, `kind` (`file` | `directory`),
`relative_path`, `parent_path`, `name`, `extension`, `category`, `size_bytes`, `modified_at`,
`fingerprint` (null for folders and until computed), `status` (`present` | `missing` | `excluded`),
`missing_since`, `kept_at` (the *Keep* decision — out of the inbox), `first_seen_at`,
`last_seen_scan_id`.
Unique `(root_id, relative_path)`; index `(root_id, parent_path)` for browsing; index
`(user_id, fingerprint)` for move detection; GIN trigram on `name`.

**`fil003_scan`** — `id`, `root_id` → fil001, `status` (`running` | `completed` | `aborted` |
`held`), `started_at`, `last_batch_at`, `finished_at`, counters (`seen`, `created`, `changed`,
`moved`, `missing`, `excluded`), `error`.

**`fil004_preferences`** — `user_id` (PK), `is_enabled` (default `false`), `created_at`,
`updated_at`. The account switch.

**`fil005_selection_mark`** — `id`, `root_id` → fil001 (cascade), `path` (`/` = the root), `mode`
(`include` | `exclude`). Unique `(root_id, path)`.

**`fil006_filter`** — `id`, `user_id`, `device_id` (null), `root_id` → fil001 (null), `scope_path`
(null), `name`, `action` (`include` | `exclude`), `applies_to` (`file` | `folder`), `matcher`
(`extension` | `glob` | `starts-with` | `ends-with` | `contains` | `regex`), `pattern`,
`case_sensitive` (null = the root's), `is_enabled`, `is_builtin`, `created_at/by`, `updated_at/by`.
Scope is the most specific non-null of `scope_path` (needs `root_id`) → `root_id` → `device_id` → user.

Later phases add metadata (`fil002.metadata jsonb`), tags and classifications (`fil007`+).

---

## 6. API surface (F1)

| Method | Path | Who | Purpose |
|---|---|---|---|
| GET / PUT | `/files/preferences` | user | the account switch |
| GET | `/files/devices` | user | devices with their platform, last seen, roots |
| GET · POST | `/files/roots` | user | list / add a root `{deviceId, name, localPath, ...}` |
| PATCH · DELETE | `/files/roots/{id}` | user | root settings / remove (entries → inbox) |
| GET · PUT | `/files/roots/{id}/selection` | user | the selection marks (replaced as a set) |
| GET · POST · PATCH · DELETE | `/files/filters` | user | filters at any scope |
| POST | `/files/filters/preview` | user | what a draft filter would match in the catalog |
| GET | `/files/roots/{id}/entries?parentPath=` | user | browse one folder |
| GET | `/files/search` | user | search (4.6) |
| GET | `/files/entries/{id}` | user | one entry |
| GET | `/files/scans?rootId=` | user | scan history |
| POST | `/files/scans/{id}/confirm` · `/discard` | user | resolve a held scan |
| GET | `/files/review/tree?rootId=&parentPath=` | user | inbox: the pruned tree, one level at a time, with counts |
| POST | `/files/review/forget` · `/files/review/keep` | user | decide on entries and/or folders `{entryIds, folders: [{rootId, path}]}` |
| GET | `/files/agent/config` | device | this device's roots, selection and filters |
| POST | `/files/agent/scans` · `/{id}/batches` · `/{id}/complete` · `/{id}/abort` | device | scan protocol (4.4) |

Agent endpoints only accept the device scheme with scope `files.agent`, and act on roots of the
key's own device.

---

## 7. Roadmap

Prerequisites: [Desktop D1 and D2](../../../architecture/en/desktop-client.md#6-roadmap).

### Phase F1 — Catalog *(the MVP)*

- Backend: the 7-project scaffold plus `Files.Agent`; `fil001`–`fil006`; `pg_trgm`; agent and user
  endpoints; the scan protocol with move detection, excluded entries and the safety brake; seeded
  default filters; filter preview; a job expiring scans with no batches.
- Desktop (Windows): `Desktop.Files` — device switch, pairing (D2), config pull, walker with
  selection pruning and filters, fingerprinter, per-root schedule + scan now with progress,
  `files.pickFolder` / `files.listFolders` / `files.reveal`.
- Web: Files settings (account switch), devices and roots, the selection tree, filters with preview,
  folder browser, search, held-scan confirmation, review inbox as a tree.
- **Done when:** on the homelab you select disk 2 with only `C/Sub` inside `C`, add an `extension`
  include filter for videos, the scan catalogs exactly that; from the notebook's browser you find a
  file by a fragment of its name; you rename it on disk, rescan, and it is the same entry (same id).

### Agents on other platforms

Follows the [desktop roadmap](../../../architecture/en/desktop-client.md#6-roadmap): a headless
agent (Linux server/NAS, Windows service), Linux/macOS desktop shells, and phones. The module needs no
change for a new desktop platform — that is what F7 buys. Phones add a root `kind` (`media-library`)
and their own agent; see the desktop doc for why they come last.

### Phase F2 — Metadata

The agent extracts what the bytes say — EXIF (date, camera, location), PDF title and page count,
video/audio duration and resolution — and sends it with the batch. Stored as `metadata jsonb`; search
filters grow from it.

### Phase F3 — Tags

User tags on entries and folders (a tag on a folder applies to what is under it when filtering).
Survive moves by F4.

### Phase F4 — AI classification

Suggested category/tags from name, path and metadata through the existing `Tars.Ai` (Gemini, key from
Integrations). Suggestions are accepted by the user, never applied silently. Reading file contents
(e.g. a PDF's text) is a separate opt-in.

### Phase F5 — Across modules

- Assistant tool `search_files` ("find the fridge manual").
- Notes: link to an entry from a page.

### Ideas — not scheduled

- **Duplicates view.** The `(user_id, fingerprint)` index already finds identical files across
  roots and devices; a page listing them (with sizes, to see what reclaiming would free) is cheap to
  add. Not needed for F1.
- **More filter matchers:** size range, modified date range.
- **Real-time watching** of a root, for collections that change by the minute.

### Not planned

Serving or streaming files to other devices; awareness of the cloud backup (iDrive or any other
provider); writing to the disk. Each would be its own decision, revisited when it earns its place.

---

## 8. Open questions

1. **Size of the collection.** Unknown, but very large ("many" files on 20 TB). The design assumes
   millions; batch size and the trigram index get tuned once the first scan reports real numbers.
2. **Network folders as roots.** F1 assumes disks plugged into the device. A folder that lives on
   another machine but is mounted on the device (`Z:\` mapped to a NAS) would work with the same
   agent, but it drops offline far more often than a local disk — the safety brake would fire more.
   The cleaner answer is usually a headless agent on the machine that owns the disk.
