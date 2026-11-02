# Espaços

[← Voltar ao índice](README.md) · Relacionados: [Pages e hierarquia](pages-and-hierarchy.md), [Modelo de dados](data-model.md), [Referência de API](api-reference.md)

---

## 1. O que é um espaço

Um **espaço** agrupa pages como um espaço do Confluence: "Pessoal", "Trabalho", "Estudos". Toda page
pertence a exatamente um (`nte001_page.space_id`), e a sidebar mostra a árvore de um espaço por vez.
Fora isso o espaço é só organização — ele **não isola** nada:

- **Tags** são do usuário, não do espaço. `#pandora` junta pages de qualquer espaço; quem quer só um
  usa o filtro de espaço da busca ou do grafo.
- **`[[Wikilinks]]`** alcançam qualquer espaço (ver §4).
- **Busca e grafo** começam no espaço da tela e podem abrir para todos.

## 2. O espaço padrão

Cada usuário tem no máximo um espaço com `is_default` (índice único parcial
`uq_nte007_user_default`). É para lá que vai uma page criada sem espaço — pelo assistente, ou por
uma chamada de API que não disse qual. O primeiro espaço que o usuário cria vira o padrão; um usuário
sem espaço nenhum que cria uma page sem espaço ganha um padrão chamado `Personal`
(`SpaceResolver.FallbackName`) — o app web sempre nomeia o espaço, então esse caminho é só de API.

O padrão **não pode ser arquivado nem excluído** (`Spaces.IsDefault`, 409). Tornar outro espaço o
padrão (`POST /spaces/{id}/default`) tira a marca do anterior numa transação própria, antes de pôr
no novo, para o índice único nunca ver dois — o mesmo padrão do calendário padrão do Agenda. Um
espaço arquivado que vira padrão volta do arquivo.

## 3. Pages e espaços

- **Uma filha mora no espaço da mãe**, qualquer que seja o espaço pedido no `POST /pages`. Assim a
  árvore nunca atravessa espaços.
- **Mover para outro espaço leva a subárvore.** `POST /pages/{id}/move` com `parentId: null` e um
  `spaceId` põe a page na raiz daquele espaço; mover para debaixo de uma page de outro espaço também
  muda o espaço. Nos dois casos o `MovePageCommandHandler` percorre a subárvore
  (`PageHierarchy.Subtree`) e muda o espaço de todas.
- **Excluir um espaço** só é aceito com ele vazio — sem page viva, arquivadas incluídas
  (`Spaces.NotEmpty`, 409). A exclusão é soft, e a FK `fk_nte001_space` garante que nenhuma page
  aponte para um espaço que não existe.

## 4. Wikilinks entre espaços

O `PageLinkSynchronizer` continua resolvendo título antes de slug, mas, entre pages com o mesmo
título, **a do espaço da page que linka vence**. Dois espaços podem ter cada um a sua "Reunião", e
`[[Reunião]]` aponta para a do próprio espaço; um título que só existe em outro espaço ainda resolve
para lá. O `buildPageIndex` do frontend segue a mesma ordem, para o preview resolver o que o save vai
resolver.

## 5. Commands e queries

| Caso de uso | Tipo | Notas |
|---|---|---|
| `CreateSpace` | Command | O primeiro do usuário vira o padrão. |
| `UpdateSpace` | Command | Nome, ícone emoji, cor hex (`#rrggbb`) e descrição. |
| `SetDefaultSpace` | Command | Duas transações, ver §2. |
| `SetSpaceArchived` | Command | Recusa o padrão. |
| `DeleteSpace` | Command | Recusa o padrão e um espaço com pages. |
| `GetSpaces` | Query | O padrão primeiro, depois por nome, cada um com `PageCount` e `LastEditedAt`. |

## 6. No frontend

- **`/notes`** (`pages/SpacesPage.tsx`) lista os espaços em cards: ícone, nome, descrição, quantas
  pages e quando foi a última edição. O menu de cada card edita, torna padrão, arquiva e exclui — o
  padrão não oferece arquivar nem excluir, e um espaço com pages mostra "Excluir" desligado.
- **`/notes/s/:spaceId`** abre o espaço: a sidebar com a árvore dele e, no lugar da page, a visão do
  espaço (`components/SpaceOverview.tsx`) com as pages da raiz.
- **`/notes/:id`** abre uma page; o espaço vem da própria page.
- **O seletor no topo da sidebar** troca de espaço ou volta para todos. O menu de cada page ganhou
  "Mover para o espaço".
- **A busca (Ctrl+K)** começa no espaço da tela e tem "Neste espaço / Todos os espaços"; com todos,
  cada resultado mostra o espaço dele. **O grafo** tem um seletor de espaço.
- **O assistente** aceita `space` no `create_note` ("anota no espaço Trabalho…"): casa o nome sem
  diferenciar maiúsculas, depois por trecho, e lista os espaços existentes quando não acha.
