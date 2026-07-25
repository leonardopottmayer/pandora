# Módulo Notes — Plano de Produto

> Notion/Obsidian pessoal do Pandora. Orientado a markdown, com upload de imagens/anexos,
> pages hierárquicas e (v2) visualização em grafo das relações entre pages.

Status: **planejamento** · Autor: Leonardo · Última atualização: 2026-07-17

---

## 1. Visão

Um espaço único, orientado a markdown, onde o conhecimento é capturado em **pages
hierárquicas** (organização estilo Notion) e navegado pelas **conexões entre elas
via grafo** (rede estilo Obsidian). Markdown é a fonte da verdade — portável e
versionável —, mas a edição é fluida, com upload de imagens e anexos inline.

Duas formas de organização convivem sobre a mesma entidade `Page`:

- **Árvore** — hierarquia pai/filho, exibida na sidebar. É a organização "de arquivo".
- **Grafo** — links arbitrários `[[wikilink]]` entre pages. É a rede de significado.

Princípio de design central: **hierarquia ≠ grafo**. São sistemas independentes.
A árvore usa `ParentId`; o grafo usa arestas `PageLink` derivadas do conteúdo.

## 2. Jobs-to-be-done

Usuário único (Leonardo). JTBDs:

- "Anotar algo rápido sem pensar onde guardar" → captura rápida + mover depois.
- "Estruturar um assunto em sub-tópicos" → pages filhas.
- "Conectar ideias que moram em lugares diferentes da árvore" → wikilinks + backlinks.
- "Ver o mapa mental do que construí" → grafo (v2).
- "Colar/subir uma imagem no meio do texto" → upload inline.
- "Achar aquilo que escrevi há meses" → busca full-text.

## 3. Escopo faseado

| Capacidade | MVP (v1) | v2 | Futuro |
|---|:---:|:---:|:---:|
| CRUD de pages + editor markdown (CodeMirror) | ✅ | | |
| Hierarquia pai/filho (árvore na sidebar) | ✅ | | |
| Upload de imagem inline + anexos | ✅ | | |
| Wikilinks `[[Page]]` + backlinks | ✅ | | |
| Busca full-text (básica) | ✅ | ranking/realce | |
| **Graph view** | | ✅ | |
| Slash commands / blocos ricos (tabelas, callouts) | | ✅ | |
| Tags, favoritos, arquivar | ✅ (mínimo) | filtros | |
| Histórico de versões | | | ✅ |
| Templates de page | | | ✅ |
| Colaboração / share público | | | ✅ (talvez nunca) |

O **grafo fica no v2**. Ele depende do modelo de links (`PageLink`) já estar sólido —
que construímos no MVP para os backlinks. Assim o grafo vira apenas uma visualização
de dados que já existem.

## 4. Funcionalidades

### Editor (decisão: Obsidian-like / CodeMirror 6)

Markdown cru com live preview, fiel ao "orientado a markdown". Fonte da verdade é o
texto markdown.

- Sintaxe markdown padrão renderizada ao vivo (`#`, `-`, `**`, ` ``` `, etc.).
- Colar imagem (Ctrl+V) ou arrastar arquivo → upload automático → insere
  `![](/api/notes/attachments/{id})`.
- Autosave com debounce (mutation via TanStack Query).

### Pages & hierarquia

- Sidebar em árvore: expandir/colapsar, drag-and-drop para reordenar e reparentar.
- Cada page: título, ícone (emoji), page pai opcional, ordem entre irmãs.
- Ações: criar filha, mover, arquivar, favoritar, deletar (soft-delete).

### Links & backlinks

- Sintaxe `[[Título da Page]]`.
- Ao salvar, o **backend** faz parse do markdown, resolve os alvos por título/slug e
  materializa arestas em `PageLink`.
- Cada page exibe painel "Linked mentions" (backlinks) — quem aponta pra ela.
- Link para page inexistente (`[[algo]]`) vira "create on click".

### Graph view (v2)

- Nós = pages; arestas = `PageLink`.
- Clique navega; hover destaca a vizinhança.
- Modos: grafo global e **local graph** (vizinhança da page atual, estilo Obsidian).
- Filtros por tag e profundidade.
- Lib candidata: `react-force-graph` (d3-force), leve e suficiente para volume pessoal.

### Busca (MVP)

- Full-text no Postgres (`tsvector` sobre título + conteúdo).
- Command palette (Ctrl+K).

## 5. Modelo de domínio

Novo módulo `Notes`, mesma anatomia dos demais. Aggregate root: `Page`.

- **Page** (root): `Id, ParentId?, Title, Slug, ContentMarkdown, Icon?, OrderIndex,
  IsFavorite, IsArchived, CreatedAt, UpdatedAt`.
- **Attachment**: `Id, PageId?, FileName, ContentType, SizeBytes, StorageBackend,
  StorageKey, CreatedAt` — ver §6 para `StorageBackend`/`StorageKey`.
- **PageLink** (aresta derivada do conteúdo): `SourcePageId, TargetPageId,
  Kind (Wikilink|Embed)`. Reconstruída a cada save da source. Alimenta backlinks **e** grafo.
- **Tag** + link (opcional no MVP; pode espelhar o padrão de tags do módulo Finances).

Regras de agregado:

- Hierarquia é apenas `ParentId` (árvore, **sem ciclos** — validar no reparent).
- Grafo é `PageLink` (ciclos são esperados e permitidos).
- Deletar page: soft-delete + remover arestas onde é source; arestas onde é target
  passam a ser "quebradas" (target inexistente).

## 6. Decisões técnicas

### Editor — CodeMirror 6

Obsidian-like: markdown cru + live preview. Mais simples e fiel ao markdown do que uma
abordagem WYSIWYG/ProseMirror.

### Storage de anexos — plugável via feature flag (DB ↔ S3/MinIO)

Requisito: dois backends de armazenamento, alternáveis por configuração.

- Abstração `IFileStorage` no `Shared` (não existe hoje — criar), com implementações:
  - **DatabaseFileStorage** — binário salvo em tabela dedicada no Postgres.
  - **S3FileStorage** — S3-compatível (MinIO / AWS S3).
- Uma **feature flag** de configuração decide qual backend é usado **na escrita**.
- O registro `Attachment` grava **`StorageBackend`** (Database | S3) e **`StorageKey`**
  (id da linha de blob, ou chave do objeto no bucket). Assim a **leitura** é
  auto-descritiva: o sistema lê o backend do próprio registro e busca no lugar certo,
  independentemente de a flag ter mudado depois.
- Servido por endpoint autenticado `GET /api/notes/attachments/{id}` (auth via Identity),
  nunca por path direto no disco/bucket.

### Parse de wikilinks — no backend

No handler de save da page: regex `\[\[...\]\]` → resolve alvos por título/slug →
regrava as arestas `PageLink` da source. Mantém o frontend simples e a fonte da verdade única.

### Encaixe na arquitetura

- Backend: novo módulo `Pottmayer.Pandora.Modules.Notes.*` (7 projetos, espelhando
  Finances: Abstractions, Contracts, Domain, Application, Infrastructure, Persistence,
  Presentation), registrado no Host.
- Migrations: `migrations/migrations/notes`.
- Frontend: `client-web/src/modules/notes`.
- i18n: EN + PT-BR, como os demais módulos.

## 7. Roadmap com critérios de verificação

### Fase 0 — Fundação (backend)

1. Scaffold do módulo `Notes` (7 camadas) + registro no Host → *verify: app sobe, health ok.*
2. `Page` aggregate + migration + CRUD (Commands/Queries) → *verify: testes de integração
   criam / leem / movem page.*

### Fase 1 — MVP editor

3. `IFileStorage` (Database + S3) + feature flag + endpoint upload/download de attachment →
   *verify: subir imagem em cada backend, recuperar por URL autenticada; alternar a flag
   e confirmar que anexos antigos continuam legíveis pelo `StorageBackend` gravado.*
4. Frontend: sidebar em árvore + editor CodeMirror + autosave → *verify: criar page,
   escrever markdown, colar imagem, recarregar e persistir.*
5. Wikilinks + `PageLink` + painel de backlinks → *verify: `[[B]]` em A aparece como
   backlink em B.*
6. Busca full-text + Ctrl+K → *verify: acha page por trecho do corpo.*

### Fase 2 — Grafo & blocos

7. Graph view (global + local) → *verify: nós/arestas batem com `PageLink`; clique navega.*
8. Slash commands, callouts, tabelas.

## 8. Decisões travadas

- **Editor**: Obsidian-like (CodeMirror 6), markdown cru + live preview.
- **Storage**: plugável DB ↔ S3/MinIO via feature flag; backend gravado no registro do anexo.
- **Grafo**: v2.
