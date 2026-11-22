# Projects Module — Product Plan

> **Status:** design recorded (2026-11-07); nothing built yet. Delivery runs in steps P1–P7 (§11).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related: [Databases](../../databases/README.md) (field types, query tree) · [Notes](../../notes/README.md)
> (the editor) · [Channels](../../channels/README.md) · [Admin](../../admin/README.md) (flags and permissions)

---

## 1. What the module does

**Projects** is a work tracker in the spirit of Azure Boards and Jira. The user creates **projects**, and
under each one plans and follows **work items** (epics, features, stories, tasks, bugs and any custom
type) through **backlogs**, **sprints** and **boards** whose pipeline they shape freely: states per type,
and board columns that nest into groups and sub-columns.

```
Project "Pandora" (key PAN)
 ├── Members and roles            (only the creator for now, §7)
 ├── Process                      item types, fields, workflows, backlog levels — copied from a template, editable
 ├── Areas, iterations (sprints), releases
 ├── Work items                   PAN-1 Epic › PAN-4 Feature › PAN-9 Story › PAN-12 Task
 ├── Boards                       kanban per backlog level, the sprint board — column trees
 └── Saved queries, dashboards
```

### What it is not

- **Not the Agenda's tasks.** Agenda tasks are personal to-dos with reminders. Projects items belong to a
  project's process, with states, hierarchy, estimates and boards. A later step can show due dates in the
  Agenda calendar (§9).
- **Not shared, yet.** Only the project's creator uses it for now, but the model is built for members from
  day one (§7), so adding them is additive.
- **Not source control or CI.** Commits and pull requests can link to items later (§9); Projects never
  hosts code.

---

## 2. Naming and coordinates

| Thing | Value |
|---|---|
| Module name | `Projects` (UI: "Projetos" in pt-BR, "Projects" in en) |
| Backend projects | `Pottmayer.Pandora.Modules.Projects.{Abstractions,Contracts,Domain,Application,Infrastructure,Persistence,Presentation}` |
| PostgreSQL schema | `projects` |
| Table prefix | `prjXXX_`, PK `uuid_generate_v7()` |
| Options section | `Pandora:Projects` |
| API base | `/api/v{version}/projects`, JWT |
| Frontend | `client-web/src/modules/projects` (routes in §8.6) |
| Migrations | `migrations/migrations/projects/` |
| Item key | `<PROJECT KEY>-<number>` (`PAN-123`), numbers per project, never reused |

---

## 3. Principles

1. **The process is data.** Item types, fields, states, transitions, backlog levels and board columns are
   rows the user edits, not code. Templates only seed them.
2. **States have categories.** Every state belongs to one fixed category (`proposed`, `in-progress`,
   `resolved`, `completed`, `removed`). Reports, burndown, "open items" and automation read categories,
   so they work with any workflow the user invents.
3. **Columns are not states.** Board columns form a tree; leaf columns map to states, several leaves can
   share a state, and each item's place on a board is stored per board (§6).
4. **Access goes through membership.** No code ever checks "is this the project's owner". Every check
   asks `IProjectAccess` for the user's role in the project (§7).
5. **People are explicit ids.** Assignee, reporter, comment author, the author of each history entry:
   always a user id, never "the current user" implied.
6. **Everything leaves a trail.** Every change to an item is a history entry with field, old value, new
   value, who and when.
7. **Shared building blocks.** Custom fields use the same field type engine as
   [Databases](../../databases/README.md) (`IFieldType`), and queries the same query tree. Whichever module
   is built first extracts the engine to a shared project; the second one reuses it.

---

## 4. Process

### 4.1 Templates

A project is created from a template, and gets its own **copy**: changing one project never changes
another.

| Template | Backlog levels and types | States per type |
|---|---|---|
| **Basic** | Epics: Epic · Issues: Issue · Tasks: Task | To Do → Doing → Done |
| **Agile** | Epics: Epic · Features: Feature · Stories: User Story, Bug · Tasks: Task | New → Active → Resolved → Closed (+ Removed) |
| **Scrum** | Epics: Epic · Features: Feature · Backlog items: Product Backlog Item, Bug · Tasks: Task, Impediment | New → Approved → Committed → Done (+ Removed) |

Templates ship in code as seed definitions; they also create the default boards and their columns (§6).

### 4.2 Item types

Each type has: name, icon, color, the backlog level it belongs to, the types allowed as its parent, its
fields and form layout, its workflow, an optional description template (for example a bug with
"Steps / Expected / Actual") and an enabled flag. Disabling a type hides it from creation without touching
existing items.

### 4.3 Backlog levels

Levels order the hierarchy: **portfolio** levels (Epics, Features — as many as the user wants), one
**requirement** level (Stories, PBIs, Bugs) and one **task** level. The sprint board works on the task
level under the requirement level; each other level gets a backlog and a kanban board.

### 4.4 Workflows

Per type:

- **States:** name, color, category (§3), order. Each category from `proposed` to `completed` needs at
  least one state; `removed` is optional.
- **Transitions:** optional. With none, any state can go to any state; with some, only those are allowed.
- **Reasons:** an optional list per transition ("Closed → Duplicate", "Active → Reopened").

Removing a state asks for a target state; the items in it move there, with a history entry.

### 4.5 Fields

**System fields** are columns on the item (§8.1): title, description, acceptance criteria, state, reason,
priority, severity, assignee, reporter, area, iteration, release, parent, tags, start date, due date,
target date, story points, effort, original/remaining/completed work, steps to reproduce, environment,
checklist. A type can hide the ones it does not use.

**Custom fields** are defined once per project and added to the types that use them, each with required
and default value. Their values live in the item's `custom jsonb`, keyed by field id. Types come from the
shared field engine: text, long text, number, decimal, date, date-time, boolean, single and multi select,
URL, user (any project member).

Lists the user edits per project: priorities, severities, tags.

### 4.6 Rich text

Description, acceptance criteria, steps to reproduce and comments use the [Notes](../../notes/README.md)
editor and its storage format. Acceptance criteria can also be a checklist.

---

## 5. Planning

### 5.1 Backlogs

- One backlog per level, as a list or as a tree (the level with its children).
- Ordered by a **fractional rank** (a string between its neighbours, LexoRank-style): dragging an item
  updates one row. The same rank orders cards within a board column.
- Inline quick create, multi-select with bulk actions (§8.4), filters, and "show completed" off by default.

### 5.2 Iterations and sprints

- Iterations form a tree (`2026 › Q4 › Sprint 23`); leaf iterations with start and end dates are sprints.
- A sprint has a goal and a state (`planned`, `active`, `closed`).
- **Capacity:** hours per day and days off per member, plus project days off. The sprint backlog shows
  planned work (remaining work, or points) against capacity.
- **Closing a sprint:** the items not in a `completed` category move to the next sprint or back to the
  backlog, the user's choice; the move is in each item's history.

### 5.3 Areas and releases

Areas form a tree (`Backend › Identity`). Releases have a name, a target date and a state; items link to
one release.

---

## 6. Boards

### 6.1 The column tree

A board's columns are a tree of any depth (the screen supports three levels for now):

```
| Backlog | Dev                  | Rollout                                          | Done |
|         | Doing    | Code rev. | Production release | Post-release monitoring    |      |
```

- **Group nodes** are headers only. They hold child columns and can have a WIP limit that counts all
  cards below them.
- **Leaf columns** hold cards. Each has a name, color, optional WIP limit and an optional
  "definition of done" shown on hover.
- Each leaf maps to **one state per item type** shown on the board ("Production release" = User Story in
  *Resolved*, Bug in *Resolved*). Several leaves may map to the same state.

### 6.2 Where a card sits

The item's leaf on a board is stored per board (`prj020`):

- **Moving a card** to a leaf sets the item's state to the one mapped for its type; if the state is the
  same (two leaves sharing it), only the board position changes. Both are history entries.
- **State changed elsewhere** (the form, a bulk edit, the Assistant, an automation): on every board, the
  card goes to the first leaf, in column order, mapped to the new state.
- **Validation on save:** every state of every type on the board must map to at least one leaf, so no card
  can be lost.
- Transitions that the workflow forbids make the target column refuse the drop.

### 6.3 Lanes, cards and styles

- **Swimlanes:** a list per board with a default lane; a card's lane is stored with its position. Lanes
  can also come from a field (priority, assignee) instead of being manual.
- **Card settings:** which fields show (id, assignee, tags, points, remaining work, children progress,
  due date) per type.
- **Style rules:** condition (the query tree, §8.3) → card color or tag color. "Severity = Critical → red".

### 6.4 Kinds of board

| Kind | Shows | Created |
|---|---|---|
| `kanban` | The items of one backlog level, optionally narrowed by a filter | One per level with the project, plus any number of extra boards |
| `sprint` | The task-level children of the selected sprint's items, one row per parent | One per project; the same column engine, with its own tree |

Both kinds share the engine of §6.1–§6.3, so the sprint board is as customizable as the kanban.

---

## 7. Access, one user now, members later

- **`prj002_member`** exists from the first step. Creating a project adds the creator as `admin`; for now
  that is the only row.
- **Roles:** `admin` (process, boards, members, everything), `member` (items, comments, sprints), `reader`
  (read only). Only `admin` is used at first.
- **One checkpoint:** `IProjectAccess.RequireAsync(projectId, role)` in every handler. Lists filter by
  "projects I am a member of", never by a creator column.
- **On top of Admin:** the global permissions (§8.5) say what a user may do in the module at all; the
  project role says what they may do in that project.
- **Concurrency from day one:** items carry a version; a stale update gets 409 and the client reloads.
  Cheap with one user, needed with many.
- **Later, additive:** a members screen and invitations, the `member` and `reader` roles in the UI,
  @mentions, watchers, notifications to other people.

---

## 8. Pandora: the Projects module

### 8.1 Data model

| Table | What it holds | Main columns |
|---|---|---|
| `prj001_project` | A project | `id`, `key` (unique), `name`, `description`, `template`, `item_sequence`, `created_by`, `archived_at`, timestamps |
| `prj002_member` | Membership | `project_id`, `user_id`, `role`, `added_at`; PK on the first two |
| `prj003_backlog_level` | A backlog level | `id`, `project_id`, `name`, `kind` (`portfolio`/`requirement`/`task`), `position` |
| `prj004_item_type` | An item type | `id`, `project_id`, `backlog_level_id`, `name`, `icon`, `color`, `allowed_parent_type_ids uuid[]`, `form_layout jsonb`, `description_template`, `hidden_system_fields text[]`, `is_enabled`, `position` |
| `prj005_state` | A workflow state | `id`, `item_type_id`, `name`, `color`, `category`, `position` |
| `prj006_transition` | An allowed transition | `from_state_id`, `to_state_id`, `reasons text[]` |
| `prj007_field` | A custom field | `id`, `project_id`, `name`, `type`, `config jsonb` |
| `prj008_item_type_field` | A field on a type | `item_type_id`, `field_id`, `is_required`, `default_value jsonb` |
| `prj009_area` | An area | `id`, `project_id`, `parent_id`, `name`, `position` |
| `prj010_iteration` | An iteration or sprint | `id`, `project_id`, `parent_id`, `name`, `start_date`, `end_date`, `goal`, `state`, `position` |
| `prj011_release` | A release | `id`, `project_id`, `name`, `target_date`, `state`, `description` |
| `prj012_item` | A work item | `id`, `project_id`, `number`, `item_type_id`, `parent_id`, `title`, `description`, `acceptance_criteria`, `steps_to_reproduce`, `environment`, `state_id`, `reason`, `priority`, `severity`, `assignee_id`, `reporter_id`, `area_id`, `iteration_id`, `release_id`, `rank`, `story_points`, `effort`, `original_work`, `remaining_work`, `completed_work`, `start_date`, `due_date`, `target_date`, `tags text[]`, `checklist jsonb`, `custom jsonb`, `version`, `created_by`, `state_changed_at`, `closed_at`, timestamps |
| `prj013_item_link` | A link between items | `from_item_id`, `to_item_id`, `kind` (`related`, `blocks`, `duplicates`, `predecessor`), `created_by`, `created_at` |
| `prj014_comment` | A comment | `id`, `item_id`, `author_id`, `body`, `created_at`, `edited_at`, `deleted_at` |
| `prj015_item_history` | A revision | `id`, `item_id`, `changed_by`, `changed_at`, `changes jsonb` (`[{ field, old, new }]`) |
| `prj016_board` | A board | `id`, `project_id`, `kind`, `name`, `backlog_level_id`, `filter jsonb`, `card_settings jsonb`, `style_rules jsonb`, `lane_field`, `position` |
| `prj017_board_column` | A column node | `id`, `board_id`, `parent_id`, `name`, `color`, `wip_limit`, `definition_of_done`, `position` |
| `prj018_board_column_state` | A leaf's state per type | `column_id`, `item_type_id`, `state_id`; PK on the first two |
| `prj019_board_lane` | A manual swimlane | `id`, `board_id`, `name`, `is_default`, `position` |
| `prj020_board_item` | A card's place | `board_id`, `item_id`, `column_id`, `lane_id`; PK on the first two |
| `prj021_capacity` | Sprint capacity | `iteration_id`, `user_id`, `hours_per_day`, `days_off date[]` |
| `prj022_saved_query` | A saved query | `id`, `project_id`, `owner_id`, `name`, `query jsonb`, `is_shared` |

Unique `prj012 (project_id, number)`. The next number comes from `prj001.item_sequence` in an
`UPDATE ... RETURNING`, so two creates never get the same one. Removed items stay (category `removed`);
hard deletion is an admin action.

Later tables: attachments, comment reactions, watchers, dashboards and widgets, worklogs, automation
rules, item templates.

### 8.2 Custom fields

The item's `custom jsonb` is keyed by field id, like a Databases record's `data`. Renaming a field touches
one row; deleting one removes its key from the project's items. Validation, filter operators, SQL
expressions, import and export come from each field's `IFieldType`.

### 8.3 Queries

- Filters, sorts and columns are a query tree (the Databases shape), over system fields, custom fields and
  a few computed ones (`@me`, `@current-sprint`, `@today`, "has children", "parent is").
- **Saved queries:** personal or shared in the project; results as a flat list or a tree.
- **Text syntax** (later): `type = Bug and state in category in-progress and assignee = @me`.

### 8.4 Bulk edit

From a backlog, a board or a query, several items at once: state, assignee, iteration, area, release,
priority, tags, parent, a custom field. Each item gets its own history entry.

### 8.5 Flags and permissions

Declared in `Projects.Abstractions` as constants (`ProjectsFeatures`, `ProjectsPermissions`):

| Kind | Key | Guards |
|---|---|---|
| Feature | `projects` | The whole module |
| Permission | `projects.projects.{read,create,update,delete}` | Projects and their settings (process, boards, areas, iterations) |
| Permission | `projects.items.{read,create,update,delete}` | Items, links, comments |

The default `user` role gets all of them. Inside a project, the role of §7 decides.

### 8.6 API and frontend (outline)

| Area | API (under `/projects`) | Screen |
|---|---|---|
| Projects | `GET/POST /`, `GET/PUT/DELETE /{key}` | `projects` — list; `projects/:key` — summary |
| Process | `/{key}/process/types`, `/states`, `/transitions`, `/fields`, `/levels` | `projects/:key/settings/process` — types, fields, workflow editor |
| Items | `POST /{key}/items`, `GET/PATCH/DELETE /items/{itemKey}`, `/items/{itemKey}/comments`, `/links`, `/history` | `projects/:key/items/:itemKey` — full form; a drawer from anywhere |
| Backlogs | `GET /{key}/backlogs/{levelId}`, `POST /items/{itemKey}/rank` | `projects/:key/backlog/:level` |
| Sprints | `/{key}/iterations`, `/iterations/{id}/capacity`, `POST /iterations/{id}/close` | `projects/:key/sprints/:iterationId` — backlog, board, capacity |
| Boards | `/{key}/boards`, `/boards/{id}/columns`, `/lanes`, `POST /boards/{id}/move` | `projects/:key/boards/:boardId`, board settings |
| Queries | `POST /{key}/query`, `/{key}/queries` | `projects/:key/queries` |
| Bulk | `POST /{key}/items/bulk` | From lists and boards |

---

## 9. Extension points and later features

| Point | What it enables |
|---|---|
| Field engine (`IFieldType`) | New custom field types, shared with Databases |
| Query tree | Text syntax, dashboard widgets, style rules, automation conditions, board filters |
| State categories | Reports and automations that work on any workflow |
| Board kinds | More layouts on the same column engine |
| Integration events | `WorkItemCreated/Changed` through the outbox, once a consumer exists |

Planned later, each with its own plan:

- **Reports:** sprint burndown/burnup, velocity, cumulative flow, lead and cycle time, bugs opened vs
  closed. **Dashboards** per project with widgets. **Roadmap:** a timeline of epics and features with
  dependencies and releases.
- **Collaboration:** members and invitations, @mentions, reactions, watchers, notifications through
  Channels (assigned, mentioned, watched item changed, sprint starts/ends), attachments.
- **Productivity:** item templates, keyboard shortcuts, worklogs, automation rules ("when all children are
  completed, resolve the parent").
- **Integrations:** the Assistant ("cria um bug no PAN: login falha com MFA"), due dates and sprints in
  the Agenda calendar, a project wiki and item ↔ page links in Notes, GitHub (a `PAN-123` in a commit or
  PR links it; a merged PR can move the state), CSV/Excel import and export, Jira and Azure CSV import.

---

## 10. Decisions

| # | Decision | Chosen | Over |
|---|---|---|---|
| 1 | Users | One user now, the membership model from day one | Members now; owner-only model refactored later |
| 2 | Process | A template copied into each project, editable there | Global inherited processes; fixed types |
| 3 | Custom fields | System fields as columns, custom ones in `jsonb` by field id, field engine shared with Databases | Everything in `jsonb` |
| 4 | Order | Fractional string rank | Integer positions |
| 5 | Rich text | The Notes editor | A markdown textarea |
| 6 | Columns and states | A column tree; leaves map to states; place stored per board | Column = state; columns independent of states |
| 7 | Name | `Projects` | `Work`; `Boards` |

---

## 11. Delivery

| Step | Delivers | Done when |
|---|---|---|
| **P1** | Projects, key and numbering, membership with the creator, templates, items with system fields, hierarchy, comments, history | A project from the Agile template has an epic › feature › story › task tree with comments and a history of every change |
| **P2** | Workflows (states, categories, transitions, reasons), the process editor (types, custom fields, backlog levels, form layout) | A custom type "Spike" with its own states and two custom fields is created and used |
| **P3** | The board engine (column tree, state mapping, WIP, lanes, cards, styles), kanban per level and extra boards | The "Rollout › Production release / Post-release monitoring" board works, both sharing *Resolved* |
| **P4** | Backlogs with rank, iterations and sprints, capacity, the sprint board with its own columns, closing a sprint | A sprint is planned against capacity, run on its board and closed, moving what was left |
| **P5** | Queries and saved queries, bulk edit, links | "My open bugs in the current sprint" is saved and bulk-moved to the next sprint |
| **P6** | Reports, dashboards, roadmap | The sprint's burndown and the velocity of the last sprints show on a dashboard |
| **P7+** | Members and the rest of §9 | One at a time, each with its own plan |
