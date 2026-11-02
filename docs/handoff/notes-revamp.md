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

- Link de vídeo sozinho na linha aparece como link no editor (player só no modo leitura).
- Link de arquivo da biblioteca no editor visual aparece como link comum (o cartão só no modo leitura).
- HTML inline (`<kbd>`, `<span style>`) é reduzido ao texto se o parágrafo for editado no visual; `<img>`
  inline vira `![|largura](…)`.
- Total de espaço ocupado na tela da biblioteca (o tamanho por item já existe).
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
