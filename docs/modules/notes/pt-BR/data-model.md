# Modelo de dados

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Pages e hierarquia](pages-and-hierarchy.md)

Schema PostgreSQL **`notes`**. Convenções em todas as tabelas: PK `uuid DEFAULT uuid_generate_v7()`,
`TIMESTAMPTZ` para timestamps, constraints nomeadas (`pk_nteXXX`, `uq_nteXXX_*`, `fk_nteXXX_*`),
enums guardados como `VARCHAR`. As raízes do usuário têm `user_id NOT NULL` e índice nele; as tabelas
derivadas chegam ao dono pela page delas.

Colunas de auditoria (`created_by/created_at/updated_by/updated_at`) existem só nas tabelas que são
*editadas* — `nte001_page` e `nte005_tag`. As derivadas (`nte004`, `nte006`) e as write-once
(`nte002`, `nte003`) carregam só `created_at`, porque nada ali atualiza uma linha.

As migrations ficam em `migrations/migrations/notes/`.

## Catálogo de tabelas

| # | Tabela | Conteúdo |
|---|---|---|
| nte001 | `page` | Pages (árvore + corpo + vetor de busca) |
| nte002 | `attachment` | Metadados dos arquivos subidos |
| nte003 | `file_blob` | Os bytes (backend `DatabaseFileStorage`) |
| nte004 | `page_link` | Arestas do grafo wiki, derivadas do corpo |
| nte005 | `tag` | Rótulos do usuário, nascidos do corpo |
| nte006 | `page_tag` | Arestas page ↔ tag, derivadas do corpo |

> A numeração é ordem de criação, não a ordem em que as tabelas se referenciam: `nte003` veio antes
> de `nte002` porque o blob store precisava existir antes dos metadados que apontam para ele.

---

## nte001_page

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | dono |
| `parent_id` | uuid NULL → nte001 | a árvore da sidebar; `NULL` é raiz |
| `title` | varchar(255) NOT NULL | |
| `slug` | varchar(100) NOT NULL | derivado do título na criação, depois congelado |
| `content_markdown` | text NOT NULL DEFAULT `''` | a fonte da verdade |
| `icon` | varchar(50) NULL | um único grafema de emoji |
| `order_index` | int NOT NULL DEFAULT 0 | posição entre as irmãs |
| `is_favorite` | boolean NOT NULL DEFAULT false | |
| `archived_at` | timestamptz NULL | arquivada = fora da árvore padrão, ainda editável |
| `deleted_at` | timestamptz NULL | soft delete |
| `search_vector` | tsvector GENERATED ALWAYS … STORED | `to_tsvector('simple', title || ' ' || content_markdown)` |
| auditoria | `created_by/at`, `updated_by/at` | |

Constraints e índices:

- `pk_nte001`, `fk_nte001_parent (parent_id → nte001.id)` — sem cascade de propósito: o comando de
  delete soft-deleta a subárvore inteira por conta própria (ver
  [D5](architecture.md#3-decisões-de-design)).
- `uq_nte001_user_slug (user_id, slug) WHERE deleted_at IS NULL` — índice único **parcial**, então
  uma page soft-deletada libera o slug para reuso.
- `ix_nte001_user_id`, `ix_nte001_parent_id`.
- `ix_nte001_search_vector` — **GIN** sobre a coluna gerada.

O vetor usa a configuração `simple` (só lower-case, sem stemming, sem adivinhar idioma), porque o
caderno mistura português e inglês e escolher uma língua pioraria a outra. Ver [Busca](search.md).

## nte002_attachment

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | também o id na URL de download |
| `page_id` | uuid NULL | **referência solta, sem FK** — a page pode ser soft-deletada e o anexo continuar ali |
| `file_name` | varchar(255) NOT NULL | nome original, usado como nome do arquivo no download |
| `content_type` | varchar(255) NOT NULL | MIME como enviado; vazio cai em `application/octet-stream` |
| `size_bytes` | bigint NOT NULL | |
| `storage_backend` | varchar(50) NOT NULL | qual `IFileStorage` tem os bytes — hoje sempre `Database` |
| `storage_key` | varchar(1024) NOT NULL | chave opaca dentro daquele backend — hoje o id da linha `nte003` |
| `created_at` | timestamptz NOT NULL | write-once, então sem `updated_*` |

Índice `ix_nte002_page_id`. O par `storage_backend` + `storage_key` é o que permite um backend S3
futuro entrar sem migration: a leitura continua auto-descritiva e as linhas antigas seguem
funcionando.

## nte003_file_blob

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | o valor guardado como `storage_key` de um anexo |
| `file_name` | varchar(255) NOT NULL | |
| `content_type` | varchar(255) NOT NULL | |
| `size_bytes` | bigint NOT NULL | |
| `content` | bytea NOT NULL | os bytes |
| `created_at` | timestamptz NOT NULL | |

O blob store genérico atrás do `DatabaseFileStorage`. Ele não sabe nada sobre pages nem usuários — é
endereçado só por uma linha de anexo.

## nte004_page_link

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `source_page_id` | uuid NOT NULL → nte001 | a page cujo corpo contém a referência |
| `target_page_id` | uuid NOT NULL → nte001 | a page para a qual ela resolve |
| `kind` | varchar(20) NOT NULL | `wikilink` \| `embed` |
| `created_at` | timestamptz NOT NULL | arestas são criadas e removidas, nunca editadas |

- `uq_nte004_edge (source_page_id, target_page_id, kind)` — um alvo linkado cinco vezes na mesma page
  é um fato só. Uma page que linka **e** embeda outra produz duas linhas.
- `ix_nte004_target_page_id` — "quem aponta pra mim?" é a leitura quente (backlinks).
- As duas FKs apontam para pages reais, e as linhas sobrevivem ao soft delete de qualquer um dos
  lados: uma aresta cujo alvo foi soft-deletado é uma aresta *quebrada*, filtrada na leitura em vez de
  apagada. As arestas que saem de uma page deletada são removidas de vez.

## nte005_tag

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `slug` | varchar(50) NOT NULL | a identidade — `#Café` e `#cafe` compartilham `cafe` |
| `name` | varchar(50) NOT NULL | como foi escrita primeiro; só exibição |
| `color` | varchar(20) NULL | hex (`#rgb`/`#rrggbb`/`#rrggbbaa`), validado no handler |
| auditoria | `created_by/at`, `updated_by/at` | `updated_*` existe porque a cor é editável |

`uq_nte005_user_slug (user_id, slug)`. Não há índice único em `name`: duas grafias colapsam numa
linha só, e a primeira grafia vence.

A linha nunca é criada por tela de CRUD — ela aparece porque o markdown de alguma page a mencionou, e
é apagada quando a última page para de mencioná-la, **a menos que tenha cor**. Ver [Tags](tags.md).

## nte006_page_tag

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `page_id` | uuid NOT NULL → nte001 | |
| `tag_id` | uuid NOT NULL → nte005 | |
| `created_at` | timestamptz NOT NULL | derivada, nunca editada |

- `uq_nte006_page_tag (page_id, tag_id)` — uma tag escrita cinco vezes na mesma page é um fato só.
- `ix_nte006_tag_id` — "quais pages carregam essa tag?" é a leitura do filtro.
- `fk_nte006_tag` precisou ser **declarada ao EF como relação**, mesmo sem propriedade de navegação.
  Sem ela, a varredura de órfãs — que apaga a tag e as arestas dela na mesma transação — mandava o
  `DELETE` da tag primeiro e batia na constraint. Foi um teste de integração que pegou isso.

---

## Mapa de relacionamentos

```
nte001_page ──parent_id──┐ (auto-referência, árvore, sem cascade)
     │                   └──> nte001_page
     │
     ├──< nte004_page_link (source_page_id, target_page_id)   o grafo wiki, ciclos permitidos
     ├──< nte006_page_tag (page_id) >── nte005_tag (user_id)  rótulos escritos no corpo
     └──… nte002_attachment (page_id, solto, sem FK) ──storage_key──> nte003_file_blob
```
