# Fase 04 — Wikilinks + `PageLink` + backlinks

> Bloco no plano: MVP §7.1.5 · Depende de: 01, 03 · Camada: backend + frontend

## Objetivo

Materializar o grafo de links a partir do conteúdo: parse de `[[wikilink]]` no backend,
arestas `PageLink` reconstruídas a cada save, e painel de backlinks ("linked mentions")
na page. Este modelo de links é a **fundação do grafo (Fase 06)**.

## Escopo

### Backend

- Entidade/aresta `PageLink`: `SourcePageId, TargetPageId, Kind (Wikilink|Embed)`.
- Migration em `migrations/migrations/notes`.
- No **handler de save da page**: regex `\[\[...\]\]` → resolve alvos por título/slug →
  **regrava** todas as arestas cuja source é a page salva (reconstrução idempotente).
  - Grafo permite ciclos (diferente da árvore).
- Ao soft-deletar uma page: remover arestas onde é **source**; arestas onde é **target**
  ficam "quebradas" (target inexistente) — tratar na leitura.
- Query: backlinks de uma page (quem aponta pra ela).

### Frontend

- Painel "Linked mentions" (backlinks) na page.
- `[[algo]]` para page inexistente → **"create on click"** (cria a page e navega).
- Autocomplete de `[[` no editor (opcional se trivial; senão, deixar registrado).

## Verify

- `[[B]]` escrito em A aparece como backlink em B após salvar.
- Reeditar A removendo o link remove a aresta (reconstrução idempotente, sem duplicatas).
- Clicar em `[[C]]` inexistente cria C e navega.

## Fora de escopo

- Visualização em grafo (Fase 06) — aqui só materializamos os dados que ela vai consumir.

## Notas de implementação (fase concluída)

- Tabela `notes.nte004_page_link` (`source_page_id`, `target_page_id`, `kind`), única por
  `(source, target, kind)`. O save faz **diff** contra as arestas existentes em vez de
  apagar-e-recriar — mesmo resultado idempotente, sem colidir com o índice único dentro da
  mesma transação.
- Resolução do alvo: título (case-insensitive) primeiro, depois slug. `[[Meeting Notes]]` e
  `[[meeting-notes]]` viram uma aresta só; `![[X]]` vira uma aresta `embed` separada.
- Backend: `GET /api/v1/notes/pages/{id}/backlinks`. O parse roda no save **e** no create
  (uma page pode nascer com conteúdo).
- Frontend: `lib/wikilinks.ts` espelha o parser/slugger do backend para o preview resolver o
  link do mesmo jeito que o save vai resolver. Embed é renderizado como link comum — embutir
  o conteúdo do alvo inline não faz parte desta fase.
- **Autocomplete de `[[`: feito depois da Fase 07**, que era o encaixe previsto — ela trouxe
  `@codemirror/autocomplete` como dependência direta (o único motivo do adiamento) e o menu de
  slash commands, ao lado de quem o de wikilink agora se registra.
  - `wikilinkTriggerAt` (em `lib/wikilinks.ts`, junto do resto do parse) abre o menu depois de
    `[[` — e de `![[`, que escreve o alvo igual — e **recusa** depois de `]` ou de `|`: link
    fechado ou metade do alias não são mais o alvo sendo digitado.
  - `filterPages` casa por **título e por slug**, então `cafe` acha `Café com Pão`; teto de 10
    opções. Mesmo motivo do menu de `/`: o `CompletionResult` vai com `filter: false`, senão a
    página que casou pelo slug seria descartada por não casar com o `label` (o título).
  - Ao aplicar, se já houver um `]]` logo depois do cursor ele é **reaproveitado** em vez de
    duplicado — é exatamente o estado que o slash command `/wikilink` deixa (`[[]]` com o cursor
    no meio).
  - A lista de pages entra por **ref** (`() => pagesRef.current`), pelo mesmo motivo do `t`: uma
    page nova não pode reconstruir o editor e perder o histórico de undo. São as mesmas pages que
    o preview usa para resolver — arquivadas incluídas.
