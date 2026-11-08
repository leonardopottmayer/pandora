# Passagem de contexto — reforma do Notes (para continuar em outra sessão)

Escrito em 2026-11-02 ao fim de uma sessão do Claude Code, para que outro agente (Codex ou outro) continue
de onde parou sem ter a conversa. Tudo aqui é estado e decisão; o *porquê* técnico de cada peça está nos
docs do módulo, linkados ao longo.

**Atualização da continuação:** o usuário confirmou nesta conversa que a N6 já foi testada e autorizou
o versionamento. Histórico implementado e commitado em `706766d`: todos os saves com alterações preservados,
snapshots completos, sem expiração, agrupamento só visual, diff entre qualquer par e restauração como
nova versão. Ver [Histórico de versões](../modules/notes/pt-BR/version-history.md). `history_number`
entrou no CREATE original da page, seguindo a convenção pré-1.0; o banco local não foi recriado.
As pendências de N6 descritas abaixo são o registro da sessão anterior, já superado por esse retorno.

**Continuação — blocos nativos:** callouts conhecidos e `<details>` simples agora têm título e corpo
editáveis no visual, inclusive blocos aninhados. A seta salva o estado aberto; Enter no título entra
no corpo e Ctrl/Cmd+Enter sai do bloco. HTML personalizado continua cru. Ver a seção 9 de
[Editor](../modules/notes/pt-BR/editor.md).

---

## 1. Regras do usuário (valem sempre)

Estão em [`AGENTS.md`](../../AGENTS.md) / [`claude.md`](../../claude.md); as que mais pegam:

- **Nunca commitar nem dar push sem pedido explícito.** "Pode comitar" vale para aquele commit só.
  Depois de commitar, **mostrar a mensagem do commit no chat**. **Nunca** trailer `Co-Authored-By`.
- **Nunca commitar nem imprimir** `backend/src/Host/Pottmayer.Pandora.Host/appsettings.json` (tem os
  segredos do usuário; aparece como modificado no `git status` de propósito). `migrations/seed/seed.local.sql`
  também não (está no `.gitignore`, tem hash de senha).
- **Nunca digitar senhas do usuário** (nem para logar no app no navegador). A conferência visual é dele.
- Código, pastas, tabelas e rotas internas **em inglês**; português só em comentários, UI e textos.
- Valores de enum no banco com **hífen** (`pending-confirmation`), colunas em snake_case.
- Pré-1.0: migration nova não existe — schema muda **no `CREATE TABLE` original**.
- Toda decisão com 2+ caminhos é apresentada como opções A/B/C, trade-off curto, **⭐ na recomendada**.
- Fala com o usuário em **português**. Ele prefere respostas diretas, sem rodeio.
- Mudanças cirúrgicas, nada especulativo, testes para lógica não trivial.

## 2. O que é a reforma

O usuário quer o módulo Notes como um Confluence/Notion pessoal, em fases, **uma por commit**:

| Fase | O quê | Commit |
|---|---|---|
| N1 | Espaços | `6e7885e` |
| N2 | Layout de documento, desktop + celular | `b316bfd` |
| N3 | Biblioteca de mídia (pastas, miniaturas) | `8ed9a41` |
| N4 | Mídia dentro das notas | `a4321dd` |
| N5 | Players de vídeo, alinhamento de imagem, callout recolhível, `/recolher` | `56d1987` |
| **N6** | **Editor visual estilo Notion** | commitada logo depois de `56d1987` (ver `git log`); falta o teste do usuário no navegador |
| depois | Histórico de versões (`706766d`), callouts/toggles nativos (`8644bc1`), busca ranqueada e grafo (`bd1fc8e`..`664b71d`) | feitos |
| fila | As lacunas do editor visual e o total da biblioteca (seção 3, "Lacunas") | — |

Fora por enquanto (decidido em 2026-11-02): capa, sumário, templates e link público.

Decisões fechadas que não se reabrem:
- **Página embutida (`![[página]]` desenhando outra página) foi descartada** — continua link.
- O texto salvo é **sempre markdown** (`content_markdown`); o editor visual não muda o backend.
- Vídeo/volume grande na biblioteca, se voltar: bucket S3 (Garage ou R2), nunca disco local.

## 3. Estado atual: N6 (commitada, aguardando o teste do usuário)

**Escolha do editor**: o usuário aprovou a ⭐ — **TipTap 3 salvando markdown, com o CodeMirror mantido como
modo "Markdown"**. Alternativas recusadas: BlockNote (salva bem só JSON), live preview no CodeMirror
(menos Notion).

Arquivos (todos em `client-web/`; o commit da N6 tem a lista completa):

| Arquivo | Papel |
|---|---|
| `src/modules/notes/lib/visualMarkdown.ts` | Schema + regras de markdown: `Wikilink`, `MediaImage` (imagem inline, opções no alt), `RawBlock` (callout / `<details>` / HTML de bloco guardados verbatim), `RawHtml`, `ImageParagraph`, separador de listas, `serializeMarkdown` |
| `src/modules/notes/lib/visualMarkdown.test.ts` | Ida e volta de cada construção (27 casos) — **é a rede de segurança; rodar antes de mexer** |
| `src/modules/notes/components/visual/VisualEditor.tsx` | O editor: menus `/` `[[` `#`, barra de seleção, barra de tabela, alça de arrastar, colar arquivo/markdown, modais |
| `.../visual/nodeViews.tsx` | Views React: wikilink clicável, imagem com botões de alinhamento, bloco cru (preview + textarea do markdown) |
| `.../visual/suggestions.ts` + `SuggestionMenu.tsx` | Ponte entre `@tiptap/suggestion` e um menu React único |
| `.../visual/visualContext.ts` | Contexto que as views leem (índice de páginas/tags, navegação) |
| `.../visual/VisualEditor.test.tsx` | Teste de componente (jsdom) |
| `src/modules/notes/components/PagePickerModal.tsx` (+ test) | Modal do `/link para página`: árvore por espaço com ícones + busca com caminho. Usado pelos **dois** editores |
| `pages/NotesPage.tsx` | Modo `visual` novo e padrão (`visual`/`edit`/`split`/`preview`); celular edita no visual; editores começam do **rascunho** |
| `components/notes.css` | Estilos do editor visual, menus, tarefas, bloco cru |
| `package.json` | TipTap 3.32.2 (`@tiptap/*`) + `overrides` forçando o `@tiptap/markdown` a usar o nosso `marked` 18 (senão vem um 17 aninhado e os tipos quebram) |
| i18n `notes.visual.*`, `notes.pagePicker.*`, `notes.viewVisual`, `notes.slash.calloutGroup` | en + pt-BR |
| `docs/modules/notes/{en,pt-BR}/editor.md` §1 e §9, `implementation-status.md` | Documentado |

Verificação feita: `npx tsc -b --noEmit` limpo, `npx eslint .` limpo, **734 testes** do front passando,
`vite build` ok. **Não verificado no navegador** — o painel do app estava deslogado e senha não se digita.

### Rodada de correções pedida pelo usuário (já aplicada, aguardando o teste dele)

1. `/image` quebrava (`RangeError: Invalid content for node doc: <image>`): o Paragraph do TipTap tira do
   parágrafo uma imagem sozinha (lá imagem é bloco). Corrigido estendendo o `Paragraph` (`ImageParagraph`).
2. To-do desalinhado no editor (margem do `<p>` dentro do item) e com bullet no modo leitura: lista de
   tarefas e lista com marcador separadas só por linha em branco viram **uma lista só** no CommonMark.
   Corrigido com CSS e com um `<!-- -->` que o editor visual escreve entre duas listas seguidas do mesmo
   marcador (e descarta ao ler).
3. "Link para página" virou o `PagePickerModal`; o menu do `[[` ganhou ícones e ficou mais largo.
4. 401 esporádico em `/api/v1/notes/media/{id}`: o `renderHTML` da imagem (usado ao copiar/arrastar)
   saía com `src` da API e o navegador buscava sem token. Agora sai `data-media-src` para mídia da
   biblioteca, e o `parseHTML` aceita os dois. Mesma regra da N5: **nenhum `<img>` com `src` da API, nunca**.

O usuário disse: "depois eu testo novamente e a gente vê se tem mais". **Próximo passo: esperar o
retorno dele do teste no navegador**, corrigir o que aparecer, e só commitar quando ele pedir.
Nada foi enviado ao remoto (sem push).

### Como o editor visual funciona (o essencial)

- `@tiptap/markdown` faz parse com `marked` e serializa por regras por nó. Cada `new Marked()` é próprio
  do editor (os tokenizers dele não podem vazar para o `MarkdownPreview`).
- Ele **escapa** `\ ` `` ` `` `* _ [ ] ~` e codifica `<`/`&` no texto. Por isso wikilink precisa ser **nó**
  (texto `[[x]]` viraria `\[\[x\]\]`). Tags `#x` sobrevivem como texto.
- O que o editor não sabe editar sem estragar vira **bloco cru**: callout (`> [!tipo]`…), `<details>`
  inteiro (atravessando linhas em branco, com aninhamento) e HTML de bloco. Ele é desenhado pelo próprio
  `MarkdownPreview` e editado como markdown (duplo clique / botão `</>`).
- Abrir a página não salva nada; só uma edição dispara `onChange`. Ao salvar, o markdown é normalizado
  (`*`→`-`, `_x_`→`*x*`, entidades) — renderiza igual. URL pura volta a ser URL pura (senão o player de
  vídeo some). Parágrafo vazio final não é escrito.
- Sublinhado desligado (não existe em markdown).

### Lacunas conhecidas / próximos passos naturais

Callout e toggle nativos já foram feitos (`8644bc1`). O usuário decidiu (2026-11-02) corrigir o resto,
**um item por commit**:

- ~~Link de vídeo sozinho na linha aparece como link no editor.~~ Feito: o player aparece embaixo do link
  (`components/visual/videoPreview.ts`, decoração do ProseMirror).
- ~~Link de arquivo da biblioteca aparece como link comum no editor.~~ Feito: nó `mediaFile`
  (`lib/visualMarkdown.ts`) desenhado como o cartão (`MediaFileView` em `nodeViews.tsx`).
- ~~HTML inline reduzido ao texto no visual.~~ Feito: nó `inlineHtml` (`lib/visualMarkdown.ts`) guarda o
  elemento como foi escrito; `<img>` inline continua virando `![|largura](…)`.
- ~~Total de espaço ocupado na biblioteca.~~ Feito: `TotalBytes`/`UnusedBytes` no `GET /media-folders`,
  mostrados abaixo das pastas.
- ~~Bundle do front num chunk só (3,9 MB).~~ Feito: as telas autenticadas usam o `lazy` das rotas do
  React Router (`src/app/router.tsx`); a entrada caiu para ~1,2 MB e o Notes (TipTap) virou um pedaço
  próprio de 1,3 MB, baixado só ao abrir o Notes. Uma barra fina no topo (`AppLayout`) indica a espera.
- Mídia sem uso continua guardada de propósito; bucket e vídeo seguem previstos, sem data.

## 4. Como rodar e verificar

```bash
cd client-web
npx vitest run src/modules/notes     # suíte do Notes (~270 testes)
npx vitest run                       # front inteiro (~734)
npx tsc -b --noEmit
npx eslint .
npm run dev                          # Vite em :5173, proxy /api
```

- Banco local: container Docker `pottmayer-postgres` (porta 5433), base `pandora`, schema `notes`. Pages em
  `notes.nte001_page` (corpo em `content_markdown`), mídia em `notes.nte002_media`. Consultas de leitura
  via `docker exec pottmayer-postgres psql -U postgres -d pandora -c "…"` ajudam a ver o que o editor salvou.
- 5 testes de integração `OAuthCallbackTests` do backend falham localmente por causa do `appsettings.json`
  local — pré-existente, não é da reforma.

## 5. Skills e modos usados nesta sessão

Skills do Claude Code (não são do repositório — o equivalente no Codex é seguir as mesmas práticas):

- **`antd`** — o CLI `@ant-design/cli` (`npm i -g @ant-design/cli`), metadados offline do Ant Design v4–v6.
  Prática: **consultar a API antes de escrever componente antd**, sem confiar na memória — o projeto é
  **antd v6** (`antd info <Componente> --version 6.x --format json`, `antd demo`, `antd token`,
  `antd lint <arquivo>` depois de mexer). Ex.: em v6 o `Drawer` usa `size`, o `Modal` usa `destroyOnHidden`.
- **`frontend-design`** — diretrizes de interface com intenção (tipografia, cor, movimento, composição),
  evitando visual genérico. **No Pandora quem manda é o [`client-web/DESIGN.md`](../../client-web/DESIGN.md)**
  (modo + tema + cor de destaque em `src/app/theme.ts`, tokens do antd via `theme.useToken()` e variáveis
  `--ant-*` no CSS); a skill serve de critério, não de licença para fugir do tema.
- **`brainstorming`** — antes de feature criativa: entender, propor 2–3 abordagens com ⭐, só então
  implementar. Foi como a N6 chegou à escolha do editor.
- **Modo "ponytail"** (estava ativo) — preguiça boa: reaproveitar o que já existe no código, stdlib/recurso
  nativo antes de dependência, menor diff que resolve a causa raiz, um teste executável para lógica não
  trivial, sem abstração especulativa.
- Navegador embutido do app para conferir telas — inútil sem login; a verificação visual fica com o usuário.

## 6. Onde está o resto do contexto

- Módulo Notes: [`docs/modules/notes/README.md`](../modules/notes/README.md) e
  [`pt-BR/README.md`](../modules/notes/pt-BR/README.md) — `editor.md`, `media-library.md`, `spaces.md`,
  `implementation-status.md` (cada um em `en/` e `pt-BR/`).
- Visual do app: [`client-web/DESIGN.md`](../../client-web/DESIGN.md).
- Histórico das fases: `git log --oneline` (mensagens de commit explicam o porquê de cada fase).

---

# Reforma em blocos — estado em 2026-11-07 e como seguir (B9–B11)

Escrito ao fim da sessão do Claude Code que construiu B1–B8. **Nada de B1–B8 está commitado.** O usuário
vai **dropar e recriar o banco local** (as migrations do Notes foram editadas no lugar, pré-1.0; nte013 e
nte014 são novas). O `migrations/seed/seed.local.sql` do usuário ainda usa `content_markdown` — não mexer,
avisar.

O que cada etapa construiu está no [plano do produto](../modules/notes/pt-BR/product-plan.md), seções
5–12; as rotas, no [api-reference](../modules/notes/pt-BR/api-reference.md). Ler essas duas antes de tudo.

## Como trabalhar nesta reforma (padrões já estabelecidos)

- **Formato:** `nte001.content` é `{version:1, blocks:[{id,type,props?,content?,children?}]}`. O backend
  valida só a forma (`Domain/ValueObjects/PageContent.cs`) e deriva texto, menções, page links, tags,
  mídias e impressões digitais por bloco. Bloco novo sem dado derivado **não precisa de backend**.
- **Editor:** TipTap 3.32.2. `client-web/src/modules/notes/lib/blocks.ts` converte blocos ↔ JSON do
  editor (`toBlocks`/`toEditorDoc`); todo tipo novo entra nos dois lados e no teste de ida-e-volta em
  `lib/blocks.test.ts`. Blocos genéricos usam o nó `richBlock` (`attrs.kind` + `props`) e a view em
  `components/visual/RichBlockView.tsx`/`AdvancedBlocks.tsx`; inline genérico usa o nó `chip`.
- **Menu `/`:** `lib/slashCommands.ts` (grupos basic, media, advanced, data, inline, embed).
- **Salvar:** todo save passa por `CreatePage`, `UpdatePage` ou `RestorePageVersion`, que chamam os
  sincronizadores em `Application/Services/` (links, mídia, tags, `BlockStampSynchronizer`). Algo derivado
  novo = um sincronizador novo chamado nesses três handlers + purge em `TrashTree.PurgeAsync`
  (`Commands/Trash/TrashCommands.cs`).
- **Comandos:** um arquivo por assunto com input/command/handler juntos (ex.:
  `Commands/Comments/CommentCommands.cs`, `Commands/PageSettings/PageSettingsCommands.cs`), permissão por
  `[RequiresPermission(NotesPermissions.Pages.X)]`, repositório registrado sozinho por
  `AddTarsDataRepositoriesFromAssemblies`, entidade em `Persistence/EntityConfigs`.
- **Frontend:** service em `services/`, hook TanStack em `hooks/`, chave em `hooks/queryKeys.ts`, textos em
  `client-web/src/i18n/locales/{en,pt-BR}.json` (editar o objeto inteiro, sem reformatar o JSON), CSS em
  `components/notes.css` com cores `light-dark()` (dois temas).
- **Verificar sempre** (tudo verde ao fim da B8: 116 unit, 116 integração do Notes, 779 vitest):
  - `cd backend; dotnet build Pottmayer.Pandora.slnx -c Release`
  - `dotnet test tests/Pottmayer.Pandora.Modules.Notes.Tests -c Release`
  - `export Pandora__Integrations__Google__ClientId=" "; dotnet test tests/Pottmayer.Pandora.IntegrationTests -c Release --filter "FullyQualifiedName~Modules.Notes"`
  - `cd client-web; npx tsc -p tsconfig.app.json --noEmit; npx eslint src/modules/notes; npx antd lint src/modules/notes --format json; npx vitest run`
- **Docs:** ao fechar cada etapa, uma seção "O que a Bx construiu" no product-plan (en e pt-BR), as rotas no
  api-reference e a linha de status ("B1 a Bx construídas").
- **Navegador:** o painel fica na tela de login e não se digita senha do usuário; diga sempre "não visto no
  navegador".

## B9 — IA

**Continuação Codex — 2026-11-07:** a fatia 1 abaixo foi implementada, sem commit: ações de seleção
e continuar pelo `/`, com prévia, aceitar/descartar, `POST /notes/ai/transform` e origem `assistant`
no primeiro autosave aceito. O port escolhido foi `ITextCompletion` em `Assistant.Abstractions`
(chamada síncrona, conforme coding-standards; não `Contracts`, reservado a eventos). Ele passa por
`CompleteTextCommand` autorizado e reutiliza perfil, modelo e chave individuais. O PUT já recebe e
valida `source`. A prévia não grava página/conversa; somente texto simples é inserido. A atribuição
é por save, incluindo edições manuais agrupadas no mesmo autosave. Ver product-plan §13 e
implementation-status. Validação no navegador e com provider real pendente. **Próxima fatia: 2,
resumir a página**, depois 3–5. As instruções abaixo são o plano original; os pontos da fatia 1 e
a dúvida sobre `source` já estão resolvidos.

**Continuação Codex — fatia 2:** resumo da página inteira implementado, sem commit. No menu da página
editável, “Resumir página com IA” salva pendências antes de abrir a prévia. A rota
`POST /notes/pages/{id}/ai/summary` confere dono e permissões de leitura/edição, usa `PageContent.Text`
(inclui tabelas, propriedades e chips, omitidos por `ToReadableText`) e rejeita texto vazio ou acima
de 32000 caracteres sem truncar. Aceitar insere um destaque Resumo no topo como origem Assistente;
descartar não grava nada. O editor confere o snapshot antes de aplicar. **Próxima fatia: 3, perguntar
às notas com fontes.** Navegador e provider real continuam pendentes; banco local intacto.

Verificação da fatia 2: 794 testes frontend, 129 unitários Notes e 126 integrações Notes aprovados;
build Release da solução, build frontend, typecheck, ESLint e lint antd aprovados. Os testes usam
provedor falso; não houve chamada ao provedor real nem validação visual no navegador.

**Continuação Codex — fatia 3:** “Perguntar às notas” implementado como aba na paleta de busca.
`POST /notes/ai/ask { question, spaceId? }` consulta até cinco páginas salvas do dono (arquivadas sim,
excluídas não), manda título e trechos de 4K caracteres ao `ITextCompletion` e devolve resposta com
fontes clicáveis. `NoteQuestionSearch` usa alternativas de palavras-chave sem mudar a busca comum;
remove palavras comuns de perguntas em PT/EN. `NoteAnswerParser` aceita só resposta estruturada e ids
consultados; saída inválida dá erro amigável, sem hits/evidência mostra mensagem explícita. Sem gravação
de notas/conversas. Busca lexical, sem embeddings/sinônimos. A identidade das fontes é validada, não a
veracidade automática da resposta. **Próxima fatia: 4, bloco de IA**, depois captura Telegram. Sem commit;
navegador/provider real pendentes; banco local intacto.

Verificação da fatia 3: 799 testes frontend, 140 unitários Notes e 134 integrações Notes aprovados.
Build Release da solução, build frontend, typecheck, ESLint e lint antd aprovados; avisos anteriores
de dependências/chunks continuam. Integrações usam provedor falso; sem teste no navegador/provider real.

**Continuação Codex — fatia 4:** bloco de IA implementado. `/Bloco de IA` insere `richBlock` kind `ai`;
props `{ prompt, output: [{ text }], generatedAt }`. Só o prompt (até 8K) vai para
`POST /notes/ai/generate`, que usa `ITextCompletion` e devolve instante UTC do servidor. Prévia local
antes de aceitar; descartar/erro mantém resultado anterior. Aceitar compara o snapshot do bloco e
aplica tudo com `AI_EDIT_META`, gerando origem Assistant no autosave. Editar prompt limpa prévia e
salva como edição comum. `AiBlock.tsx` é renderizado por `RichBlockView`; `blocks.ts` preserva o tipo,
props/ids e mostra prompt/resultado no diff. O backend já indexava arrays inline em props, então
`output` entra na busca sem sincronizador novo; `ToReadableText` agora também o lê. Markdown/HTML
exportam resultado como texto; leitura/link público mostram prompt/resultado sem gerar. **Próxima
fatia: 5, captura Telegram**, última da B9. Sem commit; navegador/provider real pendentes; banco intacto.

Verificação da fatia 4: 807 testes frontend, 146 unitários Notes e 136 integrações Notes aprovados.
Build Release da solução, build frontend, typecheck, ESLint e lint antd aprovados. Impressão esconde
prompt, controles e prévia não aceita do bloco de IA; o resultado aceito permanece. Navegador,
impressão real e provider real não verificados; testes usam provedor falso.

**Continuação Codex — fatia 5 / B9 construída:** captura direta `/nota <texto>` no bot Assistant.
Channels antes tratava todo comando slash localmente; a triagem agora encaminha `/nota` só de texto
no bot `assistant`, preservando checagem de vínculo, sufixo @bot e quebras de linha. Assistant chama
`INoteCapture` de Notes.Abstractions antes da IA. Não precisa de perfil ligado/chave; permissões/flags
de execução do Assistant e read/create/update de páginas + leitura de espaços valem normalmente.
Notes cria página Captura dedicada no espaço padrão (não reutiliza página comum de mesmo título),
reutiliza após renomear/mover/arquivar e cria outra se excluída. Caixa travada recusa. Cada mensagem
vira parágrafo literal com data/hora do recebimento no fuso efetivo da conta e origem Assistant.
`nte015_note_capture` guarda EventId como PK + user/page/capturedAt junto com o save. Recibos sem texto
e sem FK sobrevivem à purga para não recapturar evento repetido. O relay Channels atual é serial;
respostas podem se repetir numa reentrega, mas o conteúdo não. Migration nova
`20261107150000-create-table-nte015-note-capture` testada só no banco isolado, não aplicada localmente.
**B9 implementada; próximo é B10 Organização.** Sem commit, Telegram real/navegador/provider pendentes.

Verificação da fatia 5: 146 unitários Notes, 106 Assistant, 130 Channels e 159 integrações Notes/Channels
aprovados; build Release da solução aprovado, com avisos anteriores. Sem mudança no frontend nesta
fatia. A migration de `nte015` foi executada pelos testes em PostgreSQL isolado; não aplicada ao banco
local. Não houve mensagem enviada pelo Telegram real nem validação no navegador.

Verificação desta continuação: build Release da solução; 129 unitários de Notes, 96 do Assistant,
122 integrações de Notes e 791 testes do frontend aprovados. Typecheck, ESLint do Notes, lint antd
e build do frontend aprovados; avisos de dependências/chunks já existentes continuam. Não visto no
navegador nem executado com provedor real. Banco local e seed não alterados nesta continuação.

Escopo do plano: perguntar às notas, resumir, continuar, melhorar, traduzir, bloco de IA, captura pelo
Telegram.

**Onde está a IA hoje:** o módulo Assistant (`backend/src/Modules/Assistant`) usa a família
`Pottmayer.Tars.Ai.Chat.*` (provider Gemini; ver `InterpretCommandHandler.cs`) e o perfil do usuário
(`AssistantProfile`). O Notes já expõe ferramentas para o Assistant em
`Notes.Application/Assistant/` (`CreateNoteTool`, `AppendToNoteTool`, `ReadNoteTool`, `SearchNotesTool`).
Notes **não** deve referenciar o Assistant.Application: chamar via contrato (`Assistant.Contracts`) ou
consumir direto as abstrações do tars (`Ai.Chat.Abstractions`) com a configuração do provider. Decidir com
o usuário (opções com ⭐): ⭐ A) Notes pede ao Assistant por um contrato novo "complete text" em
`Assistant.Contracts` (um lugar só configura provider/chave); B) Notes usa o cliente de chat do tars direto.

Sugestão de fatias, nessa ordem:
1. **Ação de texto sobre a seleção** (resumir, melhorar, traduzir, continuar): `POST /notes/ai/transform`
   `{ action, text, language?, context? }` → `{ text }`. Sem estado, sem tabela. No editor: item "IA ▾" na
   barra de seleção (`VisualEditor.tsx`, BubbleMenu `formatMenu`) e em "continuar" no menu `/` (usa o texto
   dos blocos antes do cursor como contexto, limitar a ~8 mil caracteres). O resultado aparece numa prévia
   (substituir / inserir abaixo / descartar) antes de mexer no documento. O save marca a origem: mandar
   `source: 'assistant'` no PUT da página quando o texto veio da IA? Hoje `UpdatePageInput.Source` existe;
   conferir se a rota aceita — assim as datas do bloco (B8) dizem "pelo Assistente".
2. **Resumir a página:** mesma rota com `action: summarize` e o `PageContent.ToReadableText()` no backend
   (rota `POST /notes/pages/{id}/ai/summary`), resultado inserido como callout no topo.
3. **Perguntar às notas (RAG simples):** `POST /notes/ai/ask { question, spaceId? }` → busca as N melhores
   páginas pela busca existente (`IPageRepository.SearchAsync`, tsvector), manda os trechos (título +
   texto truncado) ao modelo pedindo resposta com citações por id de página, devolve `{ answer, sources[] }`.
   Sem embeddings na primeira versão (anotar `ponytail:` com o teto e o caminho: pgvector). UI: aba ou
   modal "Perguntar" na paleta de busca (`components/SearchPalette.tsx`).
4. **Bloco de IA:** `richBlock` kind `ai` com `props: { prompt, output, generatedAt }`; botão "gerar de novo".
   O texto gerado fica em `props.output` como blocos/inlines simples; incluir `output` no texto de busca
   (PageContent.Walk lê props? hoje só lê inlines de propriedades conhecidas — conferir `Props()`).
5. **Captura pelo Telegram:** o Channels já recebe mensagens e o Assistant já cria/anexa nota
   (`CreateNoteTool`/`AppendToNoteTool`). O que falta é a "caixa de entrada": uma página padrão (ex.:
   "Captura" no espaço padrão) onde `/nota <texto>` anexa um parágrafo com data. Ver a doc do Channels e
   do Assistant antes; provavelmente só um comando/intenção nova no Assistant.

Testes: unit do prompt/limites no backend com um cliente de chat falso; integração da rota com o provider
substituído; vitest da prévia (aceitar/descartar). Erros do provider viram mensagem amigável, nunca 500.

## B10 — Organização

Escopo: templates, notas diárias, arrastar para mover na árvore, tabela de propriedades na pasta.

1. **Templates:** a forma mais barata é "página marcada como modelo": coluna `is_template` na nte001
   (CREATE TABLE original) + filtro na árvore (seção "Modelos" na lateral) + "Nova página ▾ › a partir de
   modelo" que copia `content` gerando **ids novos** para todos os blocos (há `newBlockId` no front; no
   backend, um `PageContent.WithFreshIds()`). Variáveis simples no título/texto: `{{date}}`, `{{title}}`
   trocadas na cópia. O `template-button` (B5) já copia blocos dentro da página — reutilizar a lógica.
2. **Notas diárias:** rota `POST /notes/daily { date }` que acha ou cria a página "AAAA-MM-DD" numa pasta
   "Diário" do espaço padrão (criar se faltar), usando o modelo marcado como diário se houver
   (`appearance`? melhor uma preferência do módulo — ver memória: preferência de módulo fica no módulo).
   Atalho na lateral "Hoje" e na Home. Unicidade: índice por (user, space, slug).
3. **Arrastar na árvore:** `NotesSidebar` + rota de mover que já existe (`POST /pages/{id}/move`, e mover
   pasta em `MoveFolder`). Usar arrastar nativo do antd `Tree` (`draggable`, `onDrop`) — regra: página
   nunca contém pasta; soltar entre itens muda `order_index`. Testar ciclo (o backend já recusa:
   `PageErrors.CycleDetected`).
4. **Tabela de propriedades na pasta:** a pasta (`components/FolderView.tsx`) ganha visão "Tabela": uma
   linha por página filha, colunas = chaves dos blocos `properties` (B5, `props.rows[{key,value}]` com
   inlines) dessas páginas. Ler no backend: rota `GET /notes/folders/{id}/properties` que percorre as
   páginas da pasta e extrai as linhas (método novo em PageContent, ex. `Properties()`), sem tabela nova.
   Editar uma célula = atualizar o bloco `properties` da página (PUT da página com o conteúdo trocado) —
   ou deixar só leitura na primeira versão (perguntar ao usuário).

## B11 — Integrações

Escopo: item do Radar e e-mail para página; blocos de Finanças e Agenda; blocos de Databases e Projects
quando esses módulos existirem (ainda não existem: só docs em `docs/modules/databases` e `projects`).

- **E-mail → página:** o Communications tem as mensagens (`com005_mail_content`). Ação "Salvar no Notes"
  na tela do e-mail que chama um contrato do Notes (`Notes.Abstractions.Import.INoteImporter`: porta síncrona de criação) — o
  Communications não referencia o Notes.Application. Converter o HTML do e-mail: texto limpo em
  parágrafos + bloco `bookmark`/link de volta ao e-mail; anexos podem ir para a biblioteca (mídia) depois.
- **Radar → página:** o Radar ainda não foi construído (R1–R5 só em docs); deixar para quando existir,
  mesmo contrato.
- **Blocos de Finanças/Agenda:** `richBlock` kinds `finance-summary` (saldo de uma conta/cartão, gastos do
  mês por categoria) e `agenda-list` (tarefas de uma lista, próximos eventos). O chip `item` (B6,
  `services/moduleItems.service.ts`) já lista contas/cartões/tarefas/lembretes — reaproveitar o seletor.
  Dados lidos ao vivo pelo front com os services/hooks já existentes desses módulos (não guardar números
  no bloco, só ids e filtros em `props`). Na página pública (`publicView`) esses blocos mostram
  "conteúdo privado".

## Pendências fora das etapas

- Comentar numa página travada: hoje não dá (barra e alça só aparecem quando editável); o painel ainda
  responde/resolve. Decidir com o usuário se a página travada deve aceitar comentário novo.
- Datas dos blocos: um GET pequeno após cada autosave (`useUpdatePage` invalida `blockStamps`). Se pesar,
  buscar só ao abrir o menu da alça.
- Tudo de B1–B8 precisa do teste do usuário no navegador e do commit, que **só com pedido explícito**.

## B10: primeira fatia entregue - modelos

Marcar/desmarcar pelo menu da página; seção Modelos na lateral; criação pelo menu +, com seletor de modelos de todos os espaços do usuário e destino atual. `is_template` no CREATE original da nte001 e no EF/DTO; fora do snapshot. Nova cópia usa CreatePage para manter histórico, tags, links, mídia e datas dos blocos. Copia conteúdo/ícone, gera IDs novos inclusive nos filhos e remove âncoras de comentários. Não copia subpáginas nem configurações. Variáveis: `{{date}}` = data ISO no fuso efetivo; `{{title}}` = título final no texto; título aceita `{{date}}`. Backend recusa modelo de outro usuário, desmarcado, arquivado ou excluído. Sem commit; banco local não recriado; navegador pendente. Próxima fatia recomendada: notas diárias (definir preferência de modelo do diário no módulo).

Verificação da fatia: 147 testes unitários Notes, 148 integrações Notes e 812 testes frontend passaram; TypeScript, ESLint, Ant Design lint, builds backend/frontend e git diff --check passaram. Navegador e banco local pendentes.

## B10: notas diárias

Construído, sem commit. Hoje na lateral do Notes e na Home abre ou cria a nota diária da conta.
`POST /notes/daily { date? }` aceita data de calendário; ausente usa hoje no fuso efetivo da conta.
Páginas novas recebem título AAAA-MM-DD na pasta Diário (Daily em inglês) do espaço padrão. Uma
pasta raiz com esse nome é reutilizada inicialmente; o ID salvo mantém a identidade após renomear.
Pasta ausente/excluída é substituída.

A identidade do dia fica separada de título/localização em `nte017_daily_note`, única por usuário e
data. Renomear/mover/arquivar/bloquear mantém a página; reabrir não edita nem cria versão. Página na
lixeira ou eliminada é substituída no próximo acesso; restaurar a antiga não troca a nota diária
atual. Páginas comuns com título igual à data não são reutilizadas. Conflitos de slug recebem sufixo
numérico.

`nte016_notes_preferences` guarda modelo opcional e ID da pasta no próprio Notes. Configurações do
diário na lateral seleciona modelo marcado, ativo e do usuário; null limpa a escolha. Somente novas
notas usam o modelo: IDs aninhados novos, sem âncoras de comentários, cópia de ícone/conteúdo e
variáveis de data/título usando a data escolhida da nota. Modelo indisponível resulta em página
vazia. Entradas existentes preservadas. Páginas novas mantêm histórico/busca/tags/links/mídia/datas
dos blocos.

Lock consultivo de sessão PostgreSQL por usuário cobre leitura até commit atômico da unidade de
trabalho, serializando primeiros acessos e alterações de preferência entre instâncias. Liberação
explícita na mesma conexão aberta. Unicidade no banco garante um registro por usuário/data. Relações
EF para links e tags agora declaram a página para ordenar a inserção quando a pasta também é nova.

Permissões: abrir exige notes.pages.read/create, notes.folders.create e notes.spaces.read.
Preferências usam notes.preferences.read/update; selecionar modelo exige também notes.pages.read.
Vinculadas à feature Notes. Migrações novas criam nte016 e nte017; banco local não recriado nem
migrado. Navegador pendente.

Próxima fatia: revisar o arrastar existente da árvore e depois a tabela de propriedades de pasta.
Sem commit.

Verificação das notas diárias: 819 testes frontend, 147 unitários Notes e 159 integrações Notes
passaram, incluindo concorrência, fuso, modelo, tags/links, permissões e lixeira/purge. Builds
backend/frontend, TypeScript, ESLint, Ant Design lint e diff --check passaram. Navegador e aplicação
das migrações no banco local continuam pendentes; sem commit.

## B10: revisão do arrastar na árvore

Construído, sem commit. Arrastar na lateral move/reordena páginas e pastas por uma chamada atômica
`POST /notes/tree/move`. Pastas continuam antes de páginas; soltar sobre pasta coloca dentro dela,
sobre página cria subpágina e intervalos reordenam irmãos. Ciclos e pastas dentro de páginas são
recusados. Só os irmãos visíveis da árvore principal são renumerados; modelos continuam separados,
sem arraste nem reordenação implícita.

Arraste disponível em um espaço selecionado, sem filtro de tags, conforme permissão de atualização
do tipo de item. Todos os espaços e filtro de tags não representam hierarquia completa para
reordenar. Novos movimentos ficam bloqueados até terminar gravação e recarga; destino expande após
sucesso. Destinos inválidos são bloqueados antes de soltar. Erros recarregam a árvore e permitem
tentar de novo. O menu de mover para outro espaço conserva a rota de página individual.

Backend valida dono, espaço, destino, IDs distintos e ciclos de todo o lote antes de alterar
entidades. Usa Page.Move/Folder.Move existentes e um único commit, com histórico das páginas. Lock
por usuário serializa lotes; falha não move irmãos parcialmente nem cria versões. A implementação
não despacha comandos individuais de movimento porque gravações aninhadas independentes não
garantiriam atomicidade. Sem mudança de esquema ou migração nova. Arraste nativo com mouse/toque
ainda não conferido no navegador; testes de UI cobrem o contrato de eventos Tree e envio à API.

Próxima fatia da B10: tabela de propriedades nas pastas. Validação visual e aplicação das migrações
anteriores no banco local continuam pendentes. Sem commit.

Verificação do arrastar: 827 testes frontend, 147 unitários Notes e 168 integrações Notes
passaram. Inclui lotes de páginas/pastas, ordem após recarregar, ciclos, permissões, falha sem
alterações/versões parciais e UI enviando uma chamada sem renumerar modelos. Builds, TypeScript,
ESLint, Ant Design lint e diff --check passaram. Testes UI usam o contrato Tree; geometria de
arraste nativo e toque não verificados no navegador. Sem commit; banco local preservado.

## B10: tabela de propriedades da pasta, somente leitura

Construído, sem commit. A pasta oferece Tabela junto das visões Lista/Cartões existentes. Padrão
anterior preservado; selecionar Tabela usa a mesma preferência local de visualização. Propriedades
carregam apenas nessa visão. Cada página diretamente na pasta ganha linha, inclusive sem
propriedades; subpastas e subpáginas ficam de fora. Respeita filtros de arquivadas e interseção de
tags do Notes. Clicar no título abre a página; células somente leitura e valores ausentes/vazios
mostram travessão.

`GET /notes/folders/{id}/properties` verifica dono da pasta e permissões de leitura de
pastas/páginas. Lê apenas páginas diretamente na pasta, não excluídas, retornando resumos, união dos
nomes de propriedades e valores em texto, sem documentos completos. `PageContent.Properties()`
percorre blocos properties em qualquer nível, aparando nomes/valores, ignorando linhas sem nome e
lendo inlines de texto/status/data/item/fórmula. Variações de maiúsculas/minúsculas usam o primeiro
rótulo encontrado; chaves repetidas na mesma página preservam valores separados por quebra de linha.
Colunas seguem a ordem de encontro e páginas seguem order_index/id. Não grava conteúdo, versões ou
dados derivados.

Strings renderizadas como texto, rolagem horizontal quando necessário e estados de vazio/erro
explícitos. Trocar pasta limpa resultados anteriores. Chaves de consulta ficam sob a família de
páginas: criar/mover/arquivar/excluir invalida a projeção; autosave também atualiza as projeções
explicitamente. Sem tabela, coluna ou migração nova. Navegador e aplicação das migrações anteriores
no banco local pendentes.

Implementação da B10 concluída no escopo somente leitura aprovado. Edição nas células não incluída.
B11 integrações é a próxima; revisão visual continua pendente.

Verificação da tabela de propriedades: 833 testes frontend, 149 unitários Notes e 174 integrações
Notes passaram. Extração aninhada/chips, chaves repetidas, filtros, isolamento/permissões,
consulta sem gravação, troca de visão/pasta, texto escapado e recarga após autosave cobertos.
Builds backend/frontend, TypeScript, ESLint, Ant Design lint e diff --check passaram.
B10 implementada no escopo somente leitura aprovado; navegador e banco local pendentes. Sem commit.

## B11: e-mail para Notes

Construído, sem commit. Salvar no Notes no leitor de e-mail abre modal com título, espaço ativo e
pasta existente (inclusive aninhada), ou raiz do espaço. Assunto sugere título, limitado a 255
caracteres. Trocar espaço limpa a pasta. Salvar abre a nova página. Cada salvamento explícito cria
cópia independente; alterações posteriores no e-mail não atualizam a nota. Anexos, imagens inline e
formatação HTML não são copiados.

`POST /communications/messages/{id}/notes` lê mensagem do usuário pelo fluxo OpenMessage existente,
incluindo cache/provedor. Cliente envia apenas título/destino, nunca o corpo. Prioriza parte MIME em
texto; e-mail somente HTML usa tokenizer MimeKit: entidades decodificadas, separação de
parágrafos/listas/linhas preservada, scripts/styles/head/template/noscript/comentários descartados,
sem executar HTML ou carregar recursos remotos. Corpo vira parágrafos literais; linhas parecidas com
markdown não viram comandos ou tags.

Communications chama `INoteImporter` em Notes.Abstractions. Implementação no Notes despacha
ImportNoteCommand e CreatePageCommand normal, preservando dono do destino, busca, histórico inicial
e datas dos blocos. Sem referência a Application/Domain/Persistence de outro módulo, acesso cruzado
a tabelas ou evento de integração novo. Porta síncrona retorna ID da página já gravada para a UI.
Título/conteúdo inválido ou destino indisponível não cria página parcial. Limites normais de
tamanho/blocos da página continuam valendo.

Origem em bookmark `/mail?message={id}`. MailPage mantém mensagem selecionada nessa URL e pode
abri-la fora da lista/caixa atual. Mensagem ausente/alheia mostra erro; voltar no celular retorna à
lista. A nota copiada continua independente se o original desaparecer. Endpoint não envia e-mail,
muda flags do provedor ou importa anexos; acesso/retenção normal do cache de corpo continua
ocorrendo.

Exige communications.messages.read, notes.pages.read/create, notes.spaces.read e notes.folders.read,
com os dois módulos habilitados. Ação escondida sem acesso Notes e desabilitada até carregar o corpo
atual. Erro ao carregar destinos bloqueia salvar. Salvamento pendente bloqueia submissão
duplicada/cancelamento; falha conserva título/destino para tentar novamente. Consultas Notes
invalidadas após sucesso. Sem mudança de esquema ou migração. Navegador/provedor real pendentes;
banco local preservado. Blocos vivos de Agenda/Finanças da B11 construídos; Radar/Databases/Projects
dependem dos respectivos módulos.

Verificação de e-mail para Notes: 839 testes frontend, 149 unitários Notes e 165 Communications,
296 integrações Notes + Communications passaram. Dez integrações novas cobrem texto/HTML,
origem, permissões, dono, destinos, histórico/busca, limites e anexos não copiados. UI cobre
destino aninhado, troca de espaço, falhas, navegação à nota e retorno ao e-mail por URL.
Builds, TypeScript, ESLint, Ant Design lint e diff --check passaram. Navegador/provedor real
pendentes; sem migração nova, banco local preservado e sem commit. Próxima fatia: blocos vivos
de Agenda/Finanças (as menções de itens existentes continuam funcionando).

## B11: bloco de Agenda com dados atuais

Construído, sem commit. `/Agenda` insere rich block agenda-list. Origem padrão Hoje usa a visão
unificada do dia no fuso da conta (eventos, tarefas e lembretes, com estados atuais). Outro modo
seleciona lista ativa e mostra somente tarefas Todo/InProgress dela. Ambos somente leitura: sem
concluir tarefa, reconhecer lembrete ou editar evento pelo Notes. Abrir na Agenda leva à tela
correspondente.

Configuração salva apenas `{ mode: "today" }` ou `{ mode: "tasks", listId }`; props vazias usam
today. Títulos, nomes de listas, estados, datas e resultados consultados não entram no bloco,
autosave, texto de busca ou snapshots. Trocar modo limpa lista anterior. Lista não configurada,
excluída, alheia ou arquivada mostra estado explícito, sem carregar todas as tarefas como fallback.
Valida ID entre listas ativas do usuário antes de montar consulta de tarefas.

Reutiliza hooks, services e famílias de query keys da Agenda. Mutações normais da Agenda atualizam
blocos ativos pelo mesmo cache; Atualizar invalida explicitamente a origem escolhida. Mantém regras
normais de cache/foco, sem polling ou assinatura push. Consultar/atualizar não altera nota. Datas
exibidas usam fuso da conta e formatadores existentes; decidir o dia de hoje continua no servidor.
Falha da API esconde linhas antigas e permite tentar novamente.

Visão pública retorna Conteúdo privado antes de montar consultas, mesmo com dados do dono no cache.
Nota privada exige feature Agenda e permissões da origem: Hoje exige
agenda.events.read/tasks.read/reminders.read; lista exige agenda.tasks.read/lists.read. Nota
travada/em leitura pode consultar e atualizar, mas não configurar origem/lista. Exportações
Markdown/HTML emitem somente [Agenda], sem IDs ou valores consultados; PDF pelo navegador usa a
visão do dono renderizada naquele momento. IDs/configuração sobrevivem ao editor e versões.

Sem endpoint backend, esquema, migração ou dependência backend cruzada nova. Navegador/conta real
pendentes. Restante B11: resumos de Finanças; configurar período/calendário de eventos fica fora
desta fatia inicial Hoje/lista. Blocos Radar/Databases/Projects dependem dos respectivos módulos.

Verificação do bloco Agenda: 859 testes frontend passaram, incluindo dados atuais, fuso,
configuração sem snapshots, invalidação por mutação Agenda, permissões por origem,
lista indisponível sem fallback, erros/retry, roundtrip, exportação e render público sem
consultas (inclusive com dados privados no cache). Build frontend, TypeScript, ESLint,
Ant Design lint e diff --check passaram. Sem mudança backend/banco; suíte backend não
rerodada nesta fatia. Navegador/conta real pendentes; sem commit. Próxima fatia: Finanças.

## B11: resumos de Finanças com dados atuais

Construído, sem commit. /Finanças insere rich block finance-summary. Conta mostra saldos
efetivado/projetado; cartão mostra limites total/disponível, distinguindo zero de limite não
definido. Origem arquivada, excluída ou de outro dono aparece indisponível. O ID deve estar na
lista ativa do usuário antes da consulta de valores. Nenhuma origem é selecionada automaticamente.

Despesas mensais agregam todas as páginas dos lançamentos kind=expense, status=posted, pela data
do lançamento (from/to inclusivos). Total bruto: reembolsos não são descontados;
pendentes/anulados, receitas, transferências e pagamentos de fatura ficam fora. Moedas permanecem
separadas. Categorias agrupam por identidade, incluindo nomes históricos inativos; categoria
indisponível difere de despesa sem categoria. Erro em lançamentos/categorias esconde o resultado
para evitar total parcial.

Salva somente configuração: { mode: "account", accountId }, { mode: "card", cardId } ou { mode:
"expenses", month?: "YYYY-MM" }. Props vazias usam conta. Sem month, resolve o mês atual no fuso
da conta; mês fixo usa datas de calendário, incluindo bissextos. Mês inválido não consulta
valores. Trocar modo limpa origem/mês anterior. Nomes, saldos e totais não entram em persistência,
autosave ou versões/templates.

Frontend privado reutiliza hooks/services/query keys de Finanças. Mutações normais invalidam
resumos ativos; Atualizar recarrega a origem selecionada. Cache/foco normais, sem polling.
Consultar/atualizar não altera nota. Nota travada/em leitura pode consultar e atualizar, mas não
configurar.

Exige feature Finanças e permissões da origem: finances.accounts.read, finances.cards.read ou
ambas finances.transactions.read e finances.categories.read. Página pública retorna Conteúdo
privado antes de montar consultas, mesmo com cache do dono. Markdown/HTML emitem apenas
[Finances]; PDF usa a visão do dono renderizada no momento. Sem rota backend, dependência, esquema
ou migração nova. Navegador/conta real pendentes.

Verificação: 893 testes frontend passaram (34 novos para agregação, meses, paginação, acesso,
público, configuração, atualização e invalidação por mutação). Build/typecheck, ESLint Notes e
antd lint passaram. Backend sem alteração; suítes não rerodadas. Navegador/conta real pendentes.
Sem commit ou alteração no banco local.

Próxima fatia: configurar intervalo/calendário de eventos da Agenda. Radar/Databases/Projects
dependem da disponibilidade dos respectivos módulos. B11 continua parcial; revisão visual pendente.

## B11: próximos eventos da Agenda

Construído, sem commit. O bloco /Agenda oferece Próximos eventos. Configuração: { mode: "events",
days: 7 | 30, calendarId?: string }; days ausente/não suportado usa 7. Sem calendarId, considera
todos os calendários ativos, independentemente da visibilidade na tela Calendário. ID configurado
deve estar na lista ativa do dono. Calendário ausente/arquivado/de outro dono fica indisponível,
sem cair em todos. Lista ativa vazia não consulta eventos.

Janela cobre hoje mais as próximas 6 ou 29 datas no fuso da conta. Cada meia-noite local é
convertida separadamente para UTC, respeitando horário de verão. Fim exclusivo: meia-noite
seguinte à última data. Mostra eventos em andamento e futuros em ordem de início; oculta
encerrados/cancelados e início no/após limite final. Eventos de dia inteiro/vários dias usam
formatador existente da Agenda, incluindo fim exclusivo. Janela resolve ao renderizar/reabrir e
Atualizar; sem polling ou timer de meia-noite.

Reutiliza useCalendars/useEventOccurrences e query keys da Agenda. Endpoint existente expande
recorrências e aplica edições/cancelamentos de ocorrência; Notes não reimplementa recorrência nem
consulta detalhe de série. Filtro envia chaves calendarIds repetidas para binding ASP.NET.
Mutações normais de eventos/calendários invalidam blocos ativos; Atualizar recarrega ambas as
famílias. Erro esconde resultados antigos e permite tentar novamente.

Exige feature Agenda, agenda.events.read e agenda.calendars.read. Nota travada/em leitura pode
consultar/atualizar, mas não configurar. Trocar origem limpa filtros. Persiste apenas dias/ID;
nomes, ocorrências e datas resolvidas não entram em autosave, histórico ou templates. Página
pública bloqueia consultas mesmo com cache do dono. Markdown/HTML mantêm [Agenda]; PDF usa a visão
do dono renderizada. Sem mudança backend, esquema ou migração.

Verificação: 909 testes frontend passaram, incluindo 16 casos novos para limites no fuso/horário
de verão, ocorrências expandidas, eventos em andamento/dia inteiro, fim exclusivo,
acesso/privacidade, configuração e invalidação por mutação. Build/typecheck, ESLint Notes e
service Agenda alterado, e antd lint Notes passaram. Backend sem alteração; suítes não rerodadas.
Navegador/conta real pendentes. Sem commit ou alteração no banco local.

Próxima etapa: validação no navegador/conta real e revisão das migrações locais pendentes,
preservando dados. Escopo B11 dos módulos disponíveis entregue; Radar/Databases/Projects dependem
dos respectivos módulos. Sem nova implementação funcional obrigatória antes dessa validação.
