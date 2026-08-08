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
