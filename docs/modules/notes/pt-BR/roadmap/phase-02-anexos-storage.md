# Fase 02 — Anexos: `IFileStorage` + `DatabaseFileStorage` + upload/download

> Bloco no plano: MVP §7.1.3 · Depende de: 01 · Camada: backend

## Objetivo

Permitir upload e download autenticado de anexos (imagens para embedar, zips, PDFs),
com uma abstração de storage pronta para S3 no futuro, mas com **um único backend real**
no MVP: tabela no Postgres.

## Escopo

### Abstração (no `Shared` — não existe hoje, criar)

- `IFileStorage` com operações de salvar/ler/deletar blob.
- Implementação **`DatabaseFileStorage`**: binário em tabela dedicada.

### Persistence + migration

- Tabela de blob genérica: `Id, FileName, ContentType (MIME), SizeBytes,
  Content (bytea), CreatedAt`.
- Registro `Attachment`: `Id, PageId?, FileName, ContentType, SizeBytes,
  StorageBackend, StorageKey, CreatedAt`.
  - No MVP `StorageBackend` é sempre `Database`; `StorageKey` = id da linha de blob.
  - Campos existem já para permitir S3 no futuro **sem migração** (leitura auto-descritiva).
- Migration em `migrations/migrations/notes`.

### Presentation

- `POST /api/notes/attachments` — upload (multipart), retorna id/URL.
- `GET /api/notes/attachments/{id}` — download **autenticado** (via Identity), com
  `Content-Type` e `Content-Disposition` corretos. Nunca servir por path direto.

## Verify

- Subir uma imagem e um zip; recuperar ambos por URL autenticada com o `Content-Type`
  correto.
- Embedar `![](/api/notes/attachments/{id})` no markdown de uma page e a imagem
  renderizar (validação de renderização pode ser fechada na Fase 03, mas o endpoint
  já deve servir o binário certo aqui).
- Acesso sem auth é rejeitado.

## Fora de escopo

- Backend S3/MinIO (só a abstração fica pronta; nada de S3 implementado).
- UI de upload (colar/arrastar) — vem na Fase 03.
