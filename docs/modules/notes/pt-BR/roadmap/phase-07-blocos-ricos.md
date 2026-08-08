# Fase 07 — Slash commands, callouts, tabelas

> Bloco no plano: v2 §7.2.8 · Depende de: 03 · Camada: frontend

## Objetivo

Enriquecer a edição com blocos ricos sobre o editor CodeMirror, mantendo markdown como
fonte da verdade.

## Escopo

- **Slash commands** (`/`) no editor para inserir blocos.
- **Callouts** (blocos de destaque estilo Obsidian/Notion).
- **Tabelas** — inserção e edição assistida.

> Escopo aberto no plano original (§7.2.8 não detalha). Refinar os blocos exatos numa
> sessão de brainstorming antes de implementar, garantindo que tudo serialize para
> markdown padrão (fonte da verdade).

## Verify

- Slash command insere o bloco escolhido; o resultado persiste como markdown válido.
- Callouts e tabelas sobrevivem a reload (round-trip markdown sem perda).

## Fora de escopo

- Histórico de versões, templates de page, colaboração/share (marcados como "Futuro"
  no plano — §3).
