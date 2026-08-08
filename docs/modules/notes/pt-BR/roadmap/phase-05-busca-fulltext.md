# Fase 05 — Busca full-text + Ctrl+K

> Bloco no plano: MVP §7.1.6 · Depende de: 01, 03 · Camada: backend + frontend

## Objetivo

Achar pages por trecho do título ou do corpo, via full-text do Postgres, acessível por
um command palette (Ctrl+K). Fecha o MVP.

## Escopo

### Backend

- `tsvector` sobre `Title + ContentMarkdown` (coluna gerada ou índice, conforme padrão
  do projeto). Migration em `migrations/migrations/notes`.
- Query/endpoint de busca: `GET /api/notes/search?q=...` retornando pages que casam,
  com o mínimo pra exibir (id, título, ícone, trecho).

### Frontend

- Command palette (Ctrl+K) que consulta o endpoint e navega para a page escolhida.

## Verify

- Buscar por um trecho do **corpo** de uma page a encontra.
- Ctrl+K abre o palette, digita, seleciona e navega.

## Fora de escopo

- Ranking/realce de resultados (previsto para v2 no plano — §3).
