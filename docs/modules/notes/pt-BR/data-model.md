# Modelo de dados

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Páginas e hierarquia](pages-and-hierarchy.md)

Schema PostgreSQL **`notes`**. Convenções: PK `uuid DEFAULT uuid_generate_v7()`, `timestamptz` em
todo lugar, tabelas `nteXXX_<nome>`. Migrations em `migrations/migrations/notes/`; antes da 1.0 uma
coluna nova entra no `CREATE TABLE` original da tabela.

---

## Catálogo

| Tabela | Nome | Guarda |
|---|---|---|
| nte001 | `page` | A página e seu documento de blocos |
| nte002 | `media` | Um item da biblioteca |
| nte003 | `file_blob` | Bytes de itens e miniaturas (backend de armazenamento em banco) |
| nte004 | `page_link` | Arestas derivadas entre páginas |
| nte005 | `tag` | Uma tag do usuário |
| nte006 | `page_tag` | Derivada: que páginas têm que tags |
| nte007 | `space` | Um espaço |
| nte008 | `media_folder` | Uma pasta da biblioteca |
| nte009 | `media_rendition` | A miniatura de um item |
| nte010 | `page_media` | Derivada: que páginas usam que itens |
| nte011 | `page_version` | Snapshots imutáveis de páginas |
| nte012 | `folder` | Uma pasta de páginas |
| nte013 | `page_comment` | Comentários e respostas |
| nte014 | `block_stamp` | Quando cada bloco foi escrito e editado |
| nte015 | `note_capture` | Recibos das capturas do Telegram |
| nte016 | `notes_preferences` | As preferências do Notes do usuário |
| nte017 | `daily_note` | A página de cada data |

---

## nte001_page

| Coluna | Tipo | Notas |
|---|---|---|
| `id`, `user_id` | uuid | |
| `space_id` | uuid NOT NULL | FK `nte007` |
| `parent_id` | uuid NULL | FK `nte001`; preenchido numa subpágina |
| `folder_id` | uuid NULL | FK `nte012`; só numa página de topo |
| `title` | varchar(255) | |
| `slug` | varchar(100) | Fixo na criação; único por usuário entre páginas vivas |
| `content` | jsonb | O [documento de blocos](blocks.md); padrão `{"version": 1, "blocks": []}` |
| `content_text` | text | Texto de busca derivado dos blocos |
| `search_vector` | tsvector, gerada | `to_tsvector('simple', title || ' ' || content_text)` |
| `icon` | varchar(50) NULL | Um emoji |
| `order_index` | int | Posição entre os irmãos |
| `is_favorite`, `is_template` | boolean | |
| `archived_at`, `deleted_at` | timestamptz NULL | Excluída = na lixeira |
| `appearance` | jsonb | `{ cover?, width, font, smallText }`; ver [Páginas §8](pages-and-hierarchy.md#8-configurações-da-página) |
| `is_locked` | boolean | |
| `verified_at`, `verified_until` | timestamptz NULL | |
| `viewed_at` | timestamptz NULL | Última abertura, para as páginas recentes |
| `public_token` | varchar(64) NULL | Único quando preenchido |
| `history_number` | bigint | Número da última versão |
| `created_*`, `updated_*` | | Colunas de auditoria |

**Índices:** `uq_nte001_user_slug (user_id, slug) WHERE deleted_at IS NULL`,
`uq_nte001_public_token WHERE public_token IS NOT NULL`, GIN em `search_vector`, mais `user_id`,
`parent_id`, `folder_id`, `space_id`.

Aparência, trava, verificação, `viewed_at`, `public_token` e `is_template` **não** fazem parte de uma
versão.

## nte007_space

`user_id`, `name` (100), `icon`, `color` (`#rrggbb`), `description` (500), `is_default`,
`archived_at`, `deleted_at`, colunas de auditoria. **Índice:** `uq_nte007_user_default (user_id) WHERE
is_default AND deleted_at IS NULL`.

## nte012_folder

`user_id`, `space_id` (FK `nte007`), `parent_id` (FK `nte012`), `name` (255), `icon`, `description`,
`order_index`, `deleted_at`, colunas de auditoria. Índices em `user_id` e `space_id`.

## nte004_page_link

`source_page_id`, `target_page_id` (ambas FK `nte001`), `kind` (`wikilink` para uma menção, `embed`
para um bloco de link de página ou uma cópia sincronizada), `created_at`. **Índices:**
`uq_nte004_edge (source, target, kind)`, `ix_nte004_target_page_id` para os backlinks.

## nte005_tag / nte006_page_tag

- `nte005_tag`: `user_id`, `slug` (50), `name` (50, a primeira grafia), `color` NULL, colunas de
  auditoria. **Índice:** `uq_nte005_user_slug`.
- `nte006_page_tag`: `page_id`, `tag_id`, `created_at`. **Índice:** `uq_nte006_page_tag`, mais
  `tag_id`.

Uma linha de tag nasce quando um bloco de tags a traz e é apagada quando nenhuma página a carrega, a
menos que tenha cor. Ver [Tags](tags.md).

## Mídia: nte002, nte003, nte008, nte009, nte010

| Tabela | Colunas |
|---|---|
| `nte002_media` | `user_id`, `folder_id` (FK `nte008`), `kind` (`image`/`file`/`video`), `name`, `original_file_name`, `content_type`, `size_bytes`, `width`, `height`, `storage_backend`, `storage_key`, `deleted_at`, colunas de auditoria |
| `nte003_file_blob` | `file_name`, `content_type`, `size_bytes`, `content` (bytea), `created_at` |
| `nte008_media_folder` | `user_id`, `parent_id` (FK para ela mesma), `name`, colunas de auditoria |
| `nte009_media_rendition` | `media_id` (FK `nte002`), `purpose` (`thumbnail`), `width`, `height`, `content_type`, `size_bytes`, `storage_backend`, `storage_key`; único `(media_id, purpose)` |
| `nte010_page_media` | `page_id`, `media_id`; único `(page_id, media_id)`, índice em `media_id` |

Ver [Biblioteca de mídia](media-library.md).

## nte011_page_version

`page_id`, `user_id`, `number` (único por página), `created_at`, `source` (`initial`, `edit`,
`assistant`, `restore`), `restored_from` NULL, `snapshot` (jsonb com chaves em PascalCase: `Title`,
`Content`, `Icon`, `SpaceId`, `ParentId`, `FolderId`, `OrderIndex`, `IsFavorite`, `ArchivedAt`,
`DeletedAt`). Ver [Histórico de versões](version-history.md).

## nte013_page_comment

`page_id`, `user_id`, `block_id` (64), `thread_id` NULL (uma resposta aponta para o primeiro comentário
da conversa), `quote` (500) NULL, `body`, `resolved_at`, `created_at`, `edited_at`. Índice em
`page_id`.

## nte014_block_stamp

`page_id`, `block_id` (64), `fingerprint` (32, um MD5 do JSON do próprio bloco sem os filhos),
`created_at`, `updated_at`, `updated_source` (`edit`, `assistant`, `restore`). Único
`(page_id, block_id)`. Ver [Comentários](comments.md#4-datas-dos-blocos).

## nte015_note_capture

`id` (o id do evento de entrada), `user_id`, `page_id`, `captured_at`. Índice `(user_id, page_id)`. Não
guarda texto, e sobrevive à exclusão definitiva da página.

## nte016_notes_preferences

`user_id` (PK), `daily_template_id` NULL, `daily_folder_id` NULL, colunas de auditoria.

## nte017_daily_note

`user_id`, `date`, `page_id`; único `(user_id, date)`.

## Exclusão

Excluir uma página de vez remove seus links (nos dois sentidos), tags, usos de mídia, versões,
comentários e datas dos blocos. Nada usa `ON DELETE CASCADE`: os commands fazem isso, então a ordem é
explícita.
