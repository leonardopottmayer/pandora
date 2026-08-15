# Pages e hierarquia

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Editor](editor.md), [Referência de API](api-reference.md)

---

## 1. O que é uma page

Uma **page** é um documento markdown do usuário e o aggregate root do módulo (`nte001_page`). Todo o
resto do Notes ou deriva do corpo dela (arestas, tags, vetor de busca) ou pendura nela (anexos).

Uma page carrega um `title`, um `icon` emoji opcional, o corpo `content_markdown`, o lugar na árvore
(`parent_id` + `order_index`) e três estados: favorita, arquivada, deletada.

## 2. A árvore

A hierarquia da sidebar é `parent_id` e nada mais — `NULL` significa page raiz. As irmãs são ordenadas
por `order_index`. O frontend aninha a lista plana de `GET /notes/pages` com `lib/buildTree.ts`; o
backend nunca devolve um payload aninhado.

**Ciclos são rejeitados.** `Page.Move` guarda só o caso trivial "o pai sou eu"; a invariante de
verdade precisa da árvore inteira, então mora em `PageHierarchy.WouldCreateCycle`, que sobe a partir
do pai pretendido sobre um mapa do pai de cada page e acusa ciclo se o passeio chegar na page que está
sendo movida. Um link de pai quebrado ou estrangeiro encerra o passeio sem ciclo.

Essa é a postura oposta à do grafo wiki, que espera ciclos — ver
[Wikilinks e backlinks](wikilinks-and-backlinks.md).

## 3. Slugs

O slug é derivado do título pelo `Slugger` — minúsculas, acentos removidos, não-alfanuméricos
colapsados em hífens únicos, teto de 80 caracteres, caindo em `untitled` quando não sobra nada
utilizável. O `CreatePage` então acrescenta `-2`, `-3`, … até estar livre para aquele usuário.

Duas regras saem do schema:

- **O slug é congelado na criação.** Renomear uma page não a re-slugga, então um `[[link]]` escrito
  contra o slug antigo continua resolvendo.
- **Uma page soft-deletada libera o slug**, porque `uq_nte001_user_slug` é um índice parcial sobre
  `deleted_at IS NULL`.

## 4. Estados

| Estado | Armazenamento | Significado |
|---|---|---|
| **Favorita** | `is_favorite` boolean | Só um marcador para a sidebar. |
| **Arquivada** | `archived_at` timestamptz | Fora da árvore padrão, **ainda editável**, ainda no grafo e nos resultados de busca (com a flag). `POST /archive` é no-op se já estiver arquivada. |
| **Deletada** | `deleted_at` timestamptz | Soft delete. A linha e a história ficam; toda leitura a filtra. |

Arquivar e deletar são timestamps em vez de booleanos para o módulo registrar *quando* — e é o
`TimeProvider` injetado no aggregate que os testes controlam.

## 5. Deletar uma subárvore

O `DeletePageCommandHandler` faz mais do que virar uma flag. Ele carrega a árvore inteira do usuário,
percorre a subárvore a partir do alvo em largura, e para cada page dela:

1. soft-deleta a page (para nenhum filho ficar apontando para um pai deletado);
2. remove as arestas `PageLink` que **saem** dela — as que apontam **para** ela ficam e são filtradas
   na leitura, o que significa que restaurar a linha restauraria as menções recebidas;
3. limpa as linhas `PageTag` via `PageTagSynchronizer.ClearAsync`, que também varre qualquer tag que
   acabou de perder a última page e não tem cor.

Não existe `ON DELETE CASCADE` em `fk_nte001_parent` de propósito: um cascade de banco seria um hard
delete da história que o soft delete existe para preservar.

## 6. Commands e queries

| Caso de uso | Tipo | Notas |
|---|---|---|
| `CreatePage` | Command | Resolve um slug único; parseia o corpo em busca de links e tags (uma page pode nascer com conteúdo). |
| `UpdatePage` | Command | Título + ícone + corpo — o caminho do autosave. Reconstrói arestas e tags. |
| `MovePage` | Command | Reparent + reorder, rejeitando ciclos. |
| `SetPageFavorite` / `SetPageArchived` | Commands | Duas rotas cada (`/favorite` ↔ `/unfavorite`, `/archive` ↔ `/unarchive`) sobre um comando com flag. |
| `DeletePage` | Command | O passeio pela subárvore acima. |
| `GetPage` | Query | `PageDto` completo, com o corpo e as tags da page. |
| `GetPageTree` | Query | Lista plana de `PageSummaryDto` (sem corpo), filtrada por `includeArchived` e `tagIds`. |

O `PageDto` carrega as tags da page. No caminho de save elas vêm direto do `PageTagSynchronizer`, que
acabou de escrevê-las; nos caminhos de leitura que não tocaram no corpo (abrir, mover, favoritar,
arquivar) quem as carrega é o `PageTagReader`.

## 7. No frontend

- **Sidebar** (`components/NotesSidebar.tsx`): a árvore, com expandir/colapsar, drag-and-drop para
  reordenar e reparentar (`lib/moveMath.ts` calcula o `parentId` + `orderIndex` resultantes), ações
  por page, toggle de arquivadas, filtro de tag e botão de busca.
- **Ícone** (`components/PageIconPicker.tsx`): um botão à esquerda do título abrindo um popover com
  campo livre, uma fileira de sugestões e "remover". Sem lib de emoji — todo sistema já traz um painel
  (`Win+.`), e o que se digita é reduzido ao **primeiro grafema** via `Intl.Segmenter`, porque emoji
  costuma ser vários code points (bandeira, tom de pele, família) e cortar por índice devolveria metade
  de um. O ícone entra no `PageDraft`, então salva pelo mesmo autosave do título e do corpo.
- **A sidebar filtrada vira lista plana**, não árvore: filtrar uma hierarquia por tag a quebra (uma
  filha casa, a mãe não), e inventar uma regra de ancestral responderia uma pergunta que ninguém fez.
  Com filtro ativo, o **drag-and-drop é desligado** — um drop estaria reordenando contra irmãs que não
  estão na tela.
