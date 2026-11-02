# Modelo de dados

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Pages e hierarquia](pages-and-hierarchy.md)

Schema PostgreSQL **`notes`**. Convenções em todas as tabelas: PK `uuid DEFAULT uuid_generate_v7()`,
`TIMESTAMPTZ` para timestamps, constraints nomeadas (`pk_nteXXX`, `uq_nteXXX_*`, `fk_nteXXX_*`),
enums guardados como `VARCHAR`. As raízes do usuário têm `user_id NOT NULL` e índice nele; as tabelas
derivadas chegam ao dono pela page delas.

Colunas de auditoria (`created_by/created_at/updated_by/updated_at`) existem só nas tabelas que são
*editadas* — pages, tags, espaços, itens de mídia e pastas. As derivadas (`nte004`, `nte006`) e as
write-once (`nte003`, `nte009`) carregam só `created_at`, porque nada ali atualiza uma linha.

As migrations ficam em `migrations/migrations/notes/`.

## Catálogo de tabelas

| # | Tabela | Conteúdo |
|---|---|---|
| nte001 | `page` | Pages (árvore + corpo + vetor de busca) |
| nte002 | `media` | Itens da biblioteca de mídia: imagens e arquivos |
| nte003 | `file_blob` | Os bytes (backend `DatabaseFileStorage`) |
| nte004 | `page_link` | Arestas do grafo wiki, derivadas do corpo |
| nte005 | `tag` | Rótulos do usuário, nascidos do corpo |
| nte006 | `page_tag` | Arestas page ↔ tag, derivadas do corpo |
| nte007 | `space` | Espaços que agrupam as pages |
| nte008 | `media_folder` | Pastas virtuais da biblioteca |
| nte009 | `media_rendition` | Versões derivadas de um item de mídia (miniaturas) |
| nte010 | `page_media` | Page → item de mídia, derivada do corpo |
| nte011 | `page_version` | Snapshots completos e imutáveis; ver [Histórico de versões](version-history.md) |

> Os números nomeiam as tabelas; as migrations rodam na ordem das dependências (espaço antes da
> page, blob store e pastas antes da mídia).

---

## nte001_page

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | dono |
| `space_id` | uuid NOT NULL → nte007 | o espaço da page; uma filha sempre divide o da mãe |
| `parent_id` | uuid NULL → nte001 | a árvore da sidebar; `NULL` é raiz |
| `title` | varchar(255) NOT NULL | |
| `slug` | varchar(100) NOT NULL | derivado do título na criação, depois congelado |
| `content_markdown` | text NOT NULL DEFAULT `''` | a fonte da verdade |
| `icon` | varchar(50) NULL | um único grafema de emoji |
| `order_index` | int NOT NULL DEFAULT 0 | posição entre as irmãs |
| `is_favorite` | boolean NOT NULL DEFAULT false | |
| `archived_at` | timestamptz NULL | arquivada = fora da árvore padrão, ainda editável |
| `deleted_at` | timestamptz NULL | soft delete |
| `history_number` | bigint NOT NULL | Número do último snapshot; token de concorrência otimista |
| `search_vector` | tsvector GENERATED ALWAYS … STORED | `to_tsvector('simple', title || ' ' || content_markdown)` |
| auditoria | `created_by/at`, `updated_by/at` | |

Constraints e índices:

- `pk_nte001`, `fk_nte001_parent (parent_id → nte001.id)` — sem cascade de propósito: o comando de
  delete soft-deleta a subárvore inteira por conta própria (ver
  [D5](architecture.md#3-decisões-de-design)).
- `uq_nte001_user_slug (user_id, slug) WHERE deleted_at IS NULL` — índice único **parcial**, então
  uma page soft-deletada libera o slug para reuso.
- `ix_nte001_user_id`, `ix_nte001_parent_id`, `ix_nte001_space_id`.
- `fk_nte001_space (space_id → nte007.id)`.
- `ix_nte001_search_vector` — **GIN** sobre a coluna gerada.

O vetor usa a configuração `simple` (só lower-case, sem stemming, sem adivinhar idioma), porque o
caderno mistura português e inglês e escolher uma língua pioraria a outra. Ver [Busca](search.md).

## nte002_media

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | também o id na URL que a nota embeda |
| `user_id` | uuid NOT NULL | quem enviou; qualquer outro usuário recebe 404 |
| `folder_id` | uuid NULL → nte008 | `NULL` é "Não organizadas" |
| `kind` | varchar(20) NOT NULL | `image` \| `file` \| `video` (`chk_nte002_kind`); vídeo ainda não é aceito |
| `name` | varchar(255) NOT NULL | o que a biblioteca mostra; renomeável |
| `original_file_name` | varchar(255) NOT NULL | o nome do arquivo baixado |
| `content_type` | varchar(255) NOT NULL | MIME como enviado; vazio cai em `application/octet-stream` |
| `size_bytes` | bigint NOT NULL | |
| `width`, `height` | int NULL | pixels, já de pé; NULL para arquivo ou imagem que o servidor não lê |
| `storage_backend` | varchar(50) NOT NULL | qual `IFileStorage` tem os bytes — hoje `Database` |
| `storage_key` | varchar(1024) NOT NULL | chave opaca dentro daquele backend — hoje o id de uma linha `nte003` |
| `deleted_at` | timestamptz NULL | soft delete; os bytes continuam servidos |
| auditoria | `created_by/at`, `updated_by/at` | |

Índices `ix_nte002_user_id`, `ix_nte002_folder_id`; `fk_nte002_folder`. O par `storage_backend` +
`storage_key` é o que permite um backend de bucket entrar sem migration. Ver [Biblioteca de mídia](media-library.md).

## nte003_file_blob

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | o valor guardado como `storage_key` de um item ou de uma versão |
| `file_name` | varchar(255) NOT NULL | |
| `content_type` | varchar(255) NOT NULL | |
| `size_bytes` | bigint NOT NULL | |
| `content` | bytea NOT NULL | os bytes |
| `created_at` | timestamptz NOT NULL | |

O blob store genérico atrás do `DatabaseFileStorage`. Ele não sabe nada sobre pages, mídia ou
usuários — é endereçado só pelas linhas que apontam para ele.

## nte008_media_folder

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `parent_id` | uuid NULL → nte008 | a árvore; `NULL` é o topo |
| `name` | varchar(100) NOT NULL | |
| auditoria | `created_by/at`, `updated_by/at` | |

`fk_nte008_parent`, `ix_nte008_user_id`. Excluir uma pasta sobe o que ela tem antes, então a linha
sai de vez.

## nte009_media_rendition

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `media_id` | uuid NOT NULL → nte002 | |
| `purpose` | varchar(20) NOT NULL | `thumbnail` (`chk_nte009_purpose`); a capa de vídeo seria a próxima |
| `width`, `height` | int NOT NULL | |
| `content_type` | varchar(255) NOT NULL | `image/webp` nas miniaturas |
| `size_bytes` | bigint NOT NULL | |
| `storage_backend`, `storage_key` | como na nte002 | bytes próprios no blob store |
| `created_at` | timestamptz NOT NULL | gravada uma vez no upload |

`uq_nte009_media_purpose (media_id, purpose)` — uma de cada por item.

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

## nte007_space

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | dono |
| `name` | varchar(100) NOT NULL | |
| `icon` | varchar(50) NULL | um único grafema de emoji |
| `color` | varchar(20) NULL | hex `#rrggbb`, validado no handler |
| `description` | varchar(500) NULL | |
| `is_default` | boolean NOT NULL DEFAULT false | para onde vai uma page criada sem espaço |
| `archived_at` | timestamptz NULL | fora da lista; as pages continuam alcançáveis |
| `deleted_at` | timestamptz NULL | soft delete, só de espaço vazio |
| auditoria | `created_by/at`, `updated_by/at` | |

- `uq_nte007_user_default (user_id) WHERE is_default AND deleted_at IS NULL` — um padrão por usuário.
- `ix_nte007_user_id`. Ver [Espaços](spaces.md).

## nte010_page_media

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `page_id` | uuid NOT NULL → nte001 | |
| `media_id` | uuid NOT NULL → nte002 | |
| `created_at` | timestamptz NOT NULL | derivada, nunca editada |

- `uq_nte010_page_media (page_id, media_id)` — um item mostrado cinco vezes numa page é um fato só.
- `ix_nte010_media_id` — "quais pages usam este item?" é a leitura da biblioteca.
- Reescrita a partir do markdown a cada save; uma page excluída leva as linhas dela. Ver [Biblioteca de mídia](media-library.md#10-quais-notas-usam-cada-item).

---

## Mapa de relacionamentos

```
nte007_space ──< nte001_page (space_id)                toda page em exatamente um espaço
nte001_page ──parent_id──┐ (auto-referência, árvore, sem cascade)
     │                   └──> nte001_page
     │
     ├──< nte004_page_link (source_page_id, target_page_id)   o grafo wiki, ciclos permitidos
     ├──< nte006_page_tag (page_id) >── nte005_tag (user_id)  rótulos escritos no corpo
     └──< nte010_page_media (page_id) >── nte002_media     mídia que o corpo mostra

nte008_media_folder ──< nte002_media (folder_id) ──< nte009_media_rendition (media_id)
                         nte002 / nte009 ──storage_key──> nte003_file_blob
```
