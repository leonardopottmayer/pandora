# Databases Module — Product Plan

> **Status:** design approved (2026-11-07); nothing built yet. Delivery runs in steps T1–T6 (§9).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related: [Admin](../../admin/README.md) (flags and permissions) · [Files](../../files/README.md) ·
> [Assistant](../../assistant/README.md) · [Messaging architecture](../../../architecture/en/messaging.md)

---

## 1. What the module does

**Databases** lets each user build a small database of their own inside Pandora, in the spirit of
Airtable bases or Notion databases. The user creates **bases**, each with **tables**, gives each table
the **fields** they want (from a list of types Pandora offers) and keeps **records** in them. Tables can
point at each other through **relation** fields.

On top of that the user:

- filters and sorts with a visual filter builder, and later with a typed search syntax;
- saves the queries as **views**;
- imports CSV and Excel files into a new or an existing table;
- exports a view's result to CSV and Excel.

```
Base "Leituras"
 ├── Table "Livros"     fields: Título (text, primary) · Autor (relation → Autores) · Nota (integer) · Lido (boolean)
 │     views: "Todos", "Não lidos" (Lido = false, sorted by Título)
 └── Table "Autores"    fields: Nome (text, primary) · País (single select)
```

### What it is not

- **Not a SQL console.** The user never writes SQL, and no user input ever becomes SQL text: queries are
  a tree compiled to parameterized SQL (§5).
- **Not shared, yet.** Every base belongs to one user, like every other module's data. Sharing is a
  later step (§9, T6+).
- **Not a replacement for the other modules.** Finances, Agenda and Notes keep their own models. A user
  table can be *linked* to them later through events or field types, never merged into them.

---

## 2. Naming and coordinates

| Thing | Value |
|---|---|
| Module name | `Databases` (UI: "Bases" in pt-BR, "Databases" in en) |
| Backend projects | `Pottmayer.Pandora.Modules.Databases.{Abstractions,Contracts,Domain,Application,Infrastructure,Persistence,Presentation}` |
| PostgreSQL schema | `databases` |
| Table prefix | `dbsXXX_`, PK `uuid_generate_v7()` |
| Options section | `Pandora:Databases` |
| API base | `/api/v{version}/databases`, JWT |
| Frontend | `client-web/src/modules/databases`, routes `databases`, `databases/:baseId`, `databases/:baseId/tables/:tableId` |
| Migrations | `migrations/migrations/databases/` |

In code, the user's top-level container is a **`Base`** (not `Database`, which would read as the
server's database everywhere). The module keeps the name `Databases`.

---

## 3. Principles

1. **Schema is metadata, not DDL.** Creating a table or a field writes rows in `dbs002`/`dbs003`. Pandora
   never runs `CREATE TABLE` or `ALTER TABLE` for a user's table.
2. **Values are keyed by field id.** A record's `data` is a JSON object keyed by the field's id, never by
   its name, so renaming a field touches one row.
3. **Every type is a plugin.** What a field type accepts, how it filters, sorts, imports and exports
   lives in one class per type (§4). A new type is a new class, plus its editor in the frontend.
4. **One query shape.** The filter builder, the search syntax, saved views, exports and (later) the
   Assistant all produce the same query tree (§5).
5. **Owned by the user.** Every query filters by `UserId`, like every other module. Relations can only
   point at tables in the same base.
6. **Limits are configuration.** How many bases, tables, fields, records and import rows a user may have
   is set in options (§6.4), so a runaway import cannot fill the disk.

---

## 4. Field types

### 4.1 The contract

Each type implements `IFieldType`, registered in DI and resolved by its id:

| Member | What it does |
|---|---|
| `Id` | The stored type id, hyphenated (`short-text`, `single-select`) |
| `ValidateConfig(config)` | Checks the field's settings (select options, currency code, decimal places) |
| `Normalize(value, config)` | Turns an incoming JSON value into the stored form, or returns a validation error |
| `Operators` | The filter operators the type accepts (§5.2) |
| `SqlExpression(fieldId)` | The SQL expression that reads the value from `data` for filtering and sorting (`(data->>'<id>')::numeric`) |
| `Parse(text, config)` | Reads a cell from an imported file, with the culture of the import |
| `Format(value, config)` | Writes the value for CSV/Excel export |
| `ConvertFrom(otherType, value)` | Converts a value when the user changes a field's type; `null` when it cannot |

### 4.2 Types in the first steps

| Id | Stored as | Notes |
|---|---|---|
| `short-text` | string | Up to 1 000 characters; the default primary field |
| `long-text` | string | Up to 100 000 characters, plain text |
| `email` | string | Validated format |
| `url` | string | `http`/`https` only |
| `integer` | number | 64-bit |
| `decimal` | string | Decimal kept as a string to avoid binary floats; config: places |
| `currency` | string | Like `decimal`; config: currency code (`BRL`) |
| `boolean` | boolean | Missing means `false` |
| `date` | string `yyyy-MM-dd` | No time zone |
| `date-time` | string, ISO 8601 UTC | Shown in the user's time zone (Identity preference) |
| `single-select` | option id | Config: options with name and color |
| `multi-select` | array of option ids | Same config |
| `relation` | — (in `dbs005`) | Config: target table, one or many; see §6.3 |

Planned later: `attachment` (through Files), `formula`, `lookup`/`rollup` (through a relation),
`autonumber`, `created-at`/`updated-at`, `user`.

### 4.3 The primary field

Each table has exactly one primary field (by default the first `short-text`). It is the record's title:
what a relation shows, what search matches first, the first column of the grid. It cannot be deleted,
only replaced by another field.

### 4.4 Changing a field

- **Rename, reorder, change config:** metadata only.
- **Change type:** the backend counts how many values `ConvertFrom` can keep and returns the count
  first; the user confirms and the values that cannot convert become empty.
- **Delete:** removes the field and its key from every record of the table in one `UPDATE`, and its
  links in `dbs005`. Views that used it drop the conditions on it.
- **Removing a select option:** the option id is removed from every record that had it.

---

## 5. Queries

### 5.1 The query tree

A query is JSON, stored as-is in a view and sent as-is by the client:

```json
{
  "filter": {
    "and": [
      { "field": "<lido-id>", "op": "eq", "value": false },
      { "or": [
        { "field": "<nota-id>", "op": "gte", "value": 4 },
        { "field": "<titulo-id>", "op": "contains", "value": "duna" }
      ] }
    ]
  },
  "sort": [ { "field": "<titulo-id>", "dir": "asc" } ],
  "search": "texto livre"
}
```

`search` matches the primary field and every text field (`ILIKE`), for the search box above the grid.

### 5.2 Operators

| Group | Operators | Types |
|---|---|---|
| Equality | `eq`, `neq` | all but `long-text` and `relation` |
| Emptiness | `empty`, `not-empty` | all |
| Text | `contains`, `not-contains`, `starts-with` | text types |
| Order | `lt`, `lte`, `gt`, `gte`, `between` | numbers, dates |
| Dates | relative values: `today`, `-7d`, `+1m`, `this-week`, `this-month` | `date`, `date-time` |
| Sets | `in`, `not-in` | `single-select` |
| Sets | `has-any`, `has-all`, `has-none` | `multi-select`, `relation` |

### 5.3 Compiling

The compiler walks the tree, asks each field's type for its `SqlExpression` and its operator's SQL, and
**always** passes values as parameters. Unknown fields, operators the type does not accept and values
that fail `Normalize` are refused with a validation error that points at the condition. A depth and a
condition limit keep a query from growing without end.

Paging is offset-based (`limit`/`offset`, 50 per page), since the sort can be on any field.

> ponytail: offset paging slows down past a few hundred thousand rows; switch to keyset paging on
> `(sort value, id)` if a table ever gets there.

### 5.4 Search syntax (T5)

A small language for users who prefer to type, parsed into the same tree:

```
Lido = false and (Nota >= 4 or Título contains "duna")
[Data de leitura] > -30d and Tags has-any ("ficção", "clássico")
Autor is-empty
```

- Field names as written in the table; names with spaces in brackets.
- `and`, `or`, `not`, parentheses; strings in double quotes; dates as `2026-11-07` or relative.
- The parser returns errors with position ("unknown field `Notaa` at column 18, did you mean `Nota`?").
- The filter builder can show the text of the current filter and the other way around.

---

## 6. Pandora: the Databases module

### 6.1 Data model

| Table | What it holds | Main columns |
|---|---|---|
| `dbs001_base` | A user's base | `id`, `user_id`, `name`, `description`, `icon`, `color`, `created_at`, `updated_at` |
| `dbs002_table` | A table of a base | `id`, `base_id`, `user_id`, `name`, `description`, `position`, timestamps |
| `dbs003_field` | A table's field | `id`, `table_id`, `name`, `type`, `config jsonb`, `is_primary`, `is_required`, `position` |
| `dbs004_record` | A row | `id`, `table_id`, `user_id`, `data jsonb`, `created_at`, `updated_at` |
| `dbs005_record_link` | A relation value | `field_id`, `from_record_id`, `to_record_id`, `position`; PK on the first three |
| `dbs006_view` | A saved query | `id`, `table_id`, `name`, `type` (`grid`), `query jsonb`, `layout jsonb` (columns, widths, hidden), `position` |
| `dbs007_import` | An import job | `id`, `user_id`, `base_id`, `table_id`, `status`, `file_name`, `format`, `mapping jsonb`, `rows_total`, `rows_imported`, `errors jsonb`, timestamps |

Indexes: `dbs004 (table_id, created_at)`, GIN on `dbs004.data` (`jsonb_path_ops`), `dbs005 (to_record_id)`
for reverse lookups. Expression indexes on single fields are added only when a real table needs one.

Deleting a base or a table removes everything under it (cascade). Deletes are hard; there is no trash
in the first steps.

### 6.2 Why JSONB

| Option | Verdict |
|---|---|
| **One `data jsonb` per record (chosen)** | Schema changes are metadata, any number of tables per user, one set of migrations. Indexable with GIN and expression indexes. |
| A physical table per user table | Real columns and constraints, but DDL at runtime, outside migris, with schema drift and risky backups. |
| EAV (one row per value) | Flexible, but every query becomes N joins and slow. |

### 6.3 Relations

A `relation` field stores nothing in `data`; its values are rows in `dbs005_record_link`.

- Config: `targetTableId` (same base only) and `multiple` (one or many).
- **Reverse side:** creating a relation also creates the matching field on the target table (Airtable
  style), so "Autores" shows the books that point at each author. Deleting either side deletes both.
- Deleting a record deletes its links on both sides.
- In the grid, a relation shows the target records' primary field; the editor is a record picker with
  search.

### 6.4 Options

```json
"Pandora": {
  "Databases": {
    "Limits": {
      "MaxBases": 50,
      "MaxTablesPerBase": 50,
      "MaxFieldsPerTable": 100,
      "MaxRecordsPerTable": 50000,
      "MaxImportRows": 50000,
      "MaxImportFileMegabytes": 20,
      "MaxExportRows": 100000
    }
  }
}
```

### 6.5 Import (T4)

A wizard in three steps, backed by `dbs007_import`:

1. **Upload** a CSV or XLSX file (and for XLSX, pick the sheet). The file is parsed in streaming.
2. **Map.** The backend returns the columns, the first rows and a type guessed for each column. The user
   picks "new table" (each column becomes a field, the type can be changed) or "existing table" (each
   column goes to a field or is skipped), plus the culture of the file (decimal comma, date format).
3. **Run.** Small files run in the request; large ones as a background job. The result says how many
   rows went in and lists the rejected ones with the reason, downloadable as CSV.

Import formats are `IRecordImporter` implementations chosen by format, so JSON or another format is one
more class.

### 6.6 Export (T4)

The current view (its filter, sort and visible columns) is exported in streaming to CSV (UTF-8 with BOM,
so Excel opens accents correctly) or XLSX. Relations export the target records' primary field, selects
export option names. Like import, formats are `IRecordExporter` implementations.

Spreadsheet library: **MiniExcel** (streaming read and write, Apache 2.0). ClosedXML holds the whole
file in memory; EPPlus has a commercial license.

### 6.7 Flags and permissions

Declared in `Databases.Abstractions` as constants (`DatabasesFeatures`, `DatabasesPermissions`), like
every module:

| Kind | Key | Guards |
|---|---|---|
| Feature | `databases` | The whole module |
| Feature | `databases.import` | Import |
| Feature | `databases.export` | Export |
| Permission | `databases.bases.{read,create,update,delete}` | Bases, tables, fields and views (the schema) |
| Permission | `databases.records.{read,create,update,delete}` | Records and their links |
| Permission | `databases.imports.run` | Running an import |
| Permission | `databases.exports.run` | Running an export |

The default `user` role gets all of them.

### 6.8 API (outline)

| Method | Route | What |
|---|---|---|
| `GET/POST` | `/databases/bases` | List, create bases |
| `GET/PUT/DELETE` | `/databases/bases/{baseId}` | A base, with its tables |
| `POST` | `/databases/bases/{baseId}/tables` | Create a table (with a primary field) |
| `GET/PUT/DELETE` | `/databases/tables/{tableId}` | A table, with its fields and views |
| `POST` | `/databases/tables/{tableId}/fields` | Add a field |
| `PUT/DELETE` | `/databases/fields/{fieldId}` | Change, delete a field |
| `POST` | `/databases/fields/{fieldId}/type-change-preview` | How many values survive a type change |
| `POST` | `/databases/tables/{tableId}/records/query` | Run a query tree, paged |
| `POST` | `/databases/tables/{tableId}/records` | Create a record |
| `PATCH/DELETE` | `/databases/records/{recordId}` | Change some fields, delete |
| `POST` | `/databases/tables/{tableId}/records/delete` | Delete many |
| `GET/POST` | `/databases/tables/{tableId}/views` | List, create views |
| `PUT/DELETE` | `/databases/views/{viewId}` | Change, delete a view |
| `POST` | `/databases/tables/{tableId}/export` | Export a query tree (format in the body) |
| `POST` | `/databases/imports` · `/imports/{id}/mapping` · `/imports/{id}/run` · `GET /imports/{id}` | The import wizard |
| `POST` | `/databases/query/parse` | Text syntax → query tree (T5) |

### 6.9 Frontend

- **Bases** page: cards with icon and color, create and rename.
- **Base** page: tables as tabs, the active table's grid below.
- **Grid:** columns per field, inline editing with the type's editor, add row at the bottom, multi-select
  rows for bulk delete, column menu (rename, change type, hide, sort, delete), field drawer for config.
- **Toolbar:** view picker, filter builder, sort, search box, hidden fields, import, export.
- Each field type has its cell renderer and editor in one place (`fieldTypes/<type>.tsx`), mirroring the
  backend's registry.

---

## 7. Extension points

| Point | How it extends | Example |
|---|---|---|
| `IFieldType` | A new class (+ cell renderer/editor) | `formula`, `attachment`, `rating` |
| Query tree | New operators on a type; new producers of the tree | The Assistant turning "unread books" into a filter |
| `IRecordImporter` / `IRecordExporter` | A new format class | JSON, ODS |
| View `type` | A new layout reading the same query | `kanban` (by a select), `calendar` (by a date), `gallery` |
| Integration events | `RecordCreated/Updated/Deleted` through the outbox, published once a consumer exists | An Agenda reminder from a date field |
| Assistant tools | Tools over the Application commands | "add a book Duna by Herbert, nota 5" |

Nothing in the last two rows is built until a step needs it.

---

## 8. Decisions

| # | Decision | Chosen | Over |
|---|---|---|---|
| 1 | Where values live | `data jsonb` keyed by field id | Physical tables per user table; EAV |
| 2 | Relations | Link table `dbs005` with reverse field | Id arrays in `data` |
| 3 | Search | Query tree + own small syntax (T5), filter builder first (T2) | Builder only; raw SQL |
| 4 | Import | Three-step wizard with mapping and rejected-row report | Direct import without mapping |
| 5 | Spreadsheet library | MiniExcel | ClosedXML; EPPlus |
| 6 | Module name | `Databases` (top-level entity `Base`) | `Tables`; `Collections` |
| 7 | Access | Per user, flags and permissions in the Admin catalog, limits in options | Sharing from day one |

---

## 9. Delivery

| Step | Delivers | Done when |
|---|---|---|
| **T1** | Bases, tables, fields (types of §4.2 except `relation`), records CRUD, the grid with inline editing and paging, flags and permissions | A user builds "Livros" with five field types, edits rows in the grid and changes a field's type |
| **T2** | The query tree, filter builder, sort, search box, saved views | A view "Não lidos" filters and sorts and survives a reload |
| **T3** | `relation` with reverse field, record picker, `has-any/has-all/has-none` | "Livros → Autores" shows both sides and filters by author |
| **T4** | Export CSV/XLSX of a view; import CSV/XLSX with the wizard | A 5 000-row spreadsheet imports into a new table and exports back with the same values |
| **T5** | Text search syntax, with errors by position | The examples of §5.4 parse and match the builder's results |
| **T6+** | Formulas and rollups, kanban and calendar views, attachments, Assistant tools, sharing | One at a time, each with its own plan |
