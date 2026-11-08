# Notes — Plano da reforma em blocos

[← Voltar ao índice](README.md) · 🇺🇸 [English version](../en/product-plan.md)

> **Status:** aprovado em 2026-11-07. B1 a B10 construídas (não commitadas). B10 inclui a tabela de pasta somente leitura aprovada. B11 parcialmente construída (e-mail para página, Agenda e Finanças): [passagem de contexto](../../../handoff/notes-revamp.md). Os outros arquivos descrevem o
> módulo como era antes da reforma, markdown incluído, até cada etapa reescrevê-los.

O Notes deixa de guardar markdown. Uma página vira um **documento de blocos**, como no Notion: cada
parágrafo, título, item de lista, imagem ou tabela é um bloco com id, tipo, atributos e, quando tem
texto, conteúdo inline. Componentes novos são tipos de bloco novos com atributos novos, e os ids
estáveis dos blocos são onde se penduram comentários, links para um bloco, blocos sincronizados e
(depois) a edição simultânea.

A navegação também muda: pastas na barra lateral, uma página de visão geral por espaço, sumário por
página, tags como bloco no topo da página e links para páginas no meio do texto, em caixinha como no
Confluence. O protótipo navegável aprovado pelo usuário é a referência visual.

---

## 1. Decisões

| # | Decisão | Escolhida | Em vez de |
|---|---|---|---|
| 1 | Armazenamento | Um documento `jsonb` por página, id estável por bloco; links, tags, mídia e texto da busca derivados dele ao salvar | Uma linha por bloco |
| 2 | Editor | TipTap (já em uso), um nó por tipo de bloco, um conversor entre o JSON do TipTap e o formato de blocos do Pandora, para o banco não depender do editor | Um motor de editor próprio |
| 3 | Páginas existentes | Nenhuma é mantida: o banco local é apagado e recriado, as migrations são editadas no lugar | Converter markdown em blocos |
| 4 | Modo markdown | CodeMirror e a prévia de markdown saem; markdown volta como exportar/importar (B7) | Manter um segundo editor e a conversão nos dois sentidos |

Decisões de 2026-11-02 que isto reverte: capa, sumário, modelos de página e link público voltam. Um
bloco sincronizado mostra um bloco em várias páginas; uma página inteira continua nunca sendo desenhada
dentro de outra.

## 2. O formato dos blocos

A coluna `content` da página (`jsonb`):

```json
{
  "version": 1,
  "blocks": [
    { "id": "b8f3k2", "type": "tags", "props": { "tags": ["dotnet", "mensageria"] } },
    { "id": "c1x9qa", "type": "heading", "props": { "level": 1 }, "content": [{ "text": "Por que um outbox" }] },
    { "id": "d72mpe", "type": "paragraph", "content": [
        { "text": "Relacionado: " },
        { "mention": { "page": "0198f6a2-…" } },
        { "text": " e ", "marks": [{ "type": "bold" }] }
    ] },
    { "id": "e4a0zz", "type": "bulleted", "content": [{ "text": "Consumidores idempotentes." }], "children": [
        { "id": "f0q1rr", "type": "bulleted", "content": [{ "text": "Guardar os ids processados." }] }
    ] }
  ]
}
```

- **Bloco:** `id` (único na página), `type` (`[a-z][a-z0-9-]*`), e opcionais `props`, `content` (inline)
  e `children` (blocos).
- **Inline:** `{ text, marks? }` ou uma menção: `{ mention: { page } }` agora; datas, status e itens de
  outros módulos depois (B6). Marcas: `bold`, `italic`, `underline`, `strike`, `code`, `link` (`href`),
  `color`, `highlight`.
- **Mídia:** todo `props.mediaId` (e `props.items[].mediaId` numa galeria) é um uso de item da biblioteca.
- **O backend confere a forma, não cada tipo.** Tamanho, profundidade, número de blocos, ids únicos e o
  padrão do tipo são validados; o significado das props de cada tipo é do frontend. Um tipo de bloco
  novo não muda o backend, a não ser que traga dado derivado.

## 3. Dados derivados, refeitos a cada save

| Dado | De onde | Antes |
|---|---|---|
| Texto da busca (`content_text`, por trás do `tsvector`) | Todo texto inline, células de tabela, legendas, código | O markdown |
| Links entre páginas (`nte004`) | Menções de página no texto (tipo `wikilink`) e blocos de link para página (tipo `embed`) | `[[…]]` / `![[…]]` lidos do texto |
| Tags (`nte006`) | `props.tags` do bloco `tags` | `#tag` lido do texto |
| Uso de mídia (`nte010`) | `mediaId` nas props de qualquer bloco | Caminhos da biblioteca lidos do texto |

As menções apontam para o **id** da página, então renomear uma página nunca quebra um link, e nada mais
resolve títulos.

## 4. Etapas

| Etapa | Entrega |
|---|---|
| **B1 Fundação** | O documento de blocos, a validação, os dados derivados, o histórico em blocos, o Assistant em blocos, o editor TipTap salvando blocos, o editor e a prévia de markdown removidos |
| **B2 Navegação** | Pastas (páginas dentro de pastas, subpáginas dentro de páginas, nunca pasta dentro de página), a visão geral do espaço (cards e árvore), a página da pasta, a barra lateral nova, o sumário, o bloco de tags, os links para página em caixinha e "Mencionada em" |
| **B3 Blocos básicos** | Títulos recolhíveis, lista de alternância, colunas, destaque com tom, cores de texto e fundo, alinhamento, links com três aparências (texto, cartão, incorporado), seleção de vários blocos, link para um bloco, comandos `/cor` e `/transformar`, o menu `/` em categorias |
| **B4 Mídia** | Galeria, vídeo, áudio, leitor de PDF, link externo, incorporar (YouTube, Figma, Maps, GitHub, Loom, Drive, Miro, CodePen, X/Bluesky, oEmbed) |
| **B5 Avançados** | Mermaid, fórmula, bloco sincronizado, botão de modelo, decisão, caminho, lista de subpáginas, páginas por tag, propriedades da página |
| **B6 Menções** | Data e lembrete (Agenda), itens de outros módulos, status, fórmula no texto, emoji |
| **B7 Página** | Capa, largura, fonte, texto pequeno, bloqueio, recentes, lixeira com restauração, verificação, link público, exportar markdown/PDF/HTML, importar markdown |
| **B8 Comentários** | Comentários num bloco ou num trecho; autor e datas por bloco |
| **B9 IA** | Pergunte às suas notas, resumir, continuar, melhorar, traduzir, bloco de IA, anotar pelo Telegram |
| **B10 Organização** | Modelos, notas diárias, arrastar para mover na árvore, tabela de propriedades na pasta |
| **B11 Integrações** | Item do Radar e e-mail para página; blocos de Finances e Agenda; blocos de Databases e Projects quando esses módulos existirem |

## 5. O que a B1 construiu

- **Backend:** `PageContent` (Domain) valida o documento e tira dele, numa passada só, texto, menções,
  blocos de link para página, tags e ids de mídia. `nte001.content` é `jsonb`, e `content_text` alimenta
  o `tsvector`. Os parsers de markdown (`WikilinkParser`, `TagParser`, `MediaReferenceParser`) e a busca
  por título saíram: os links apontam para ids. As versões guardam o documento e o comparam como JSON,
  então a ordem de chaves do Postgres não transforma um save sem mudança numa versão nova. O Assistant
  escreve texto como blocos (um por linha, inícios de linha markdown lidos como títulos e listas) e lê
  uma nota de volta como texto.
- **Frontend:** `lib/blocks.ts` converte entre blocos e TipTap; `lib/visualSchema.ts` é o esquema do
  editor, com um `blockId` em cada nó de bloco. Links para página são menções em caixinha digitadas com
  `@` ou `[[`; um tipo de bloco desconhecido é mantido e salvo de volta intacto; as tags são editadas no
  cabeçalho e salvas como o primeiro bloco. O editor de markdown, a prévia de leitura e os modos de
  visualização saíram; no celular a página abre no mesmo editor, só leitura, até "Editar".

## 6. O que a B2 construiu

- **Pastas** (`nte012_folder`): uma pasta guarda pastas e páginas e vive num espaço; uma página fica numa
  pasta (`nte001.folder_id`) só quando é de primeiro nível, já que uma subpágina vive sob a página pai.
  A API é `/notes/folders` (listar por espaço, criar, editar, mover, excluir), com as permissões
  `notes.folders.*`; criar e mover página aceitam `folderId`. Excluir uma pasta exclui (soft delete) as
  subpastas e todas as páginas dentro delas, com as subpáginas; um espaço com pastas não está vazio. As
  versões guardam a pasta, e restaurar uma versão cuja pasta sumiu põe a página no topo do espaço.
- **Barra lateral:** "Visão geral" primeiro, depois pastas e páginas numa árvore só (pastas antes). O `+`
  de uma pasta cria página ou pasta dentro dela; arrastar e soltar move páginas para pastas ou para
  baixo de páginas e pastas para pastas, nunca pasta para dentro de página. O que está aberto fica
  expandido.
- **Visão geral** do espaço: contagens, páginas editadas recentemente e a estrutura em cards (cada item
  com o que guarda) ou em árvore. **Página da pasta** (`/notes/f/:id`): nome e descrição editados ali
  mesmo, o conteúdo em lista ou em cards.
- **Página:** o sumário (títulos 1 a 3) ao lado da página em tela larga e recolhido acima dela nas
  outras, marcando a seção que está sendo lida; abaixo da página, as subpáginas e "Mencionada em"; o
  caminho (breadcrumb) passa pelas pastas também.

O menu `/` agrupa os blocos como combinado na discussão do protótipo: **Básicos**, **Mídia**,
**Avançados**, **Dados** (Databases, Projects, Finances, Agenda), **No texto** (menções) e
**Incorporar**.

## 7. O que a B3 construiu

Tudo no frontend: o backend só confere o formato, e o bloco `page-link` já alimentava os links.

| Bloco ou prop | Guardado como |
|---|---|
| Cor do texto, fundo, alinhamento | `props.color`, `props.background` (`gray`, `orange`, `yellow`, `green`, `blue`, `purple`, `magenta`, `red`), `props.align` (`center`, `right`) em parágrafos, títulos, itens de lista, citações, código, destaques e alternâncias |
| Cor num trecho do texto | Marcas `color` e `highlight`, `attrs.color` com as mesmas oito |
| Título recolhível | `heading` com `props.toggle: true` e `props.open`; a seção são os `children` |
| Lista de alternância | `toggle` (o nome em português passou a ser "Lista de alternância") |
| Colunas | `columns` com 2 a 4 blocos `column` nos `children`; tela estreita empilha |
| Tons do destaque | `props.tone`: `note`, `info`, `success`, `warning`, `error`, escolhido no ícone do destaque |
| Link para página | Menção no texto, ou bloco `page-link` com `props.pageId` e `props.look` (`preview`; cartão quando ausente). A prévia mostra as primeiras linhas da página, nunca a página inteira |

- **Menu do bloco:** um clique na alça seleciona o bloco (Shift estende a seleção) e abre transformar
  em, cor, alinhamento, copiar link do bloco, duplicar, mover para página e excluir.
- **Vários blocos:** Ctrl/Cmd+arrastar, Shift+↑/↓ ou Ctrl/Cmd+A selecionam blocos inteiros; uma barra
  sobre eles abre as mesmas ações, e a alça move todos juntos. Mover para outra página acrescenta os
  blocos no fim dela, com ids novos.
- **Link para um bloco:** `/notes/<página>#block-<id>` rola até o bloco e o marca por um instante.
- **Menu `/`:** em categorias (Básicos, Mídia, Avançados, Dados, No texto, Incorporar). Digitando
  aparecem as cores (`/vermelho`, `/fundo azul`) e os comandos "transformar em", que agem no bloco
  onde a barra foi digitada. Os blocos de dados de outros módulos chegam com a B11.
- **Trocar o visual de um link de página:** cartão e prévia oferecem no texto, cartão e prévia; uma
  menção selecionada oferece cartão e prévia.

## 8. O que a B4 construiu

| Bloco | Guardado como | Aparece como |
|---|---|---|
| Galeria | `gallery`, `props.items[]` com `{ mediaId, caption? }` | Grade de imagens da biblioteca, que abre o visualizador; um quadro adiciona mais uma |
| Vídeo | `video`, `props.url` | Player do YouTube, Vimeo ou Loom, ou o do navegador para um arquivo de vídeo |
| Áudio | `audio`, `props.mediaId` (+ `name`) ou `props.url` | O player do navegador |
| PDF | `pdf`, `props.mediaId` (+ `name`) ou `props.url` | O leitor de PDF do navegador, dentro da página |
| Link com prévia | `bookmark`, `props.url`, `title`, `description`, `siteName`, `imageUrl`, `iconUrl` | Um cartão; o que a página dizia fica guardado no bloco quando ele é criado |
| Incorporar | `embed`, `props.url` | O frame do site para YouTube, Vimeo, Loom, Figma, Google Maps, Drive e Docs, Miro, CodePen e gists; para X, Bluesky e qualquer site com oEmbed, o HTML do site; senão, um cartão |

- **Prévia de link** (`GET /notes/links/preview`): o servidor lê a página, recusando endereços
  privados (SSRF), e assim o link com prévia ganha título e imagem e o embed ganha o oEmbed.
- **Frames:** o frame de um site tem o sandbox de todo frame de nota; HTML de oEmbed ou de gist roda
  num frame escrito por nós, com sandbox **sem** a nossa origem, que só informa a própria altura.
- **Um PDF da biblioteca** é lido como `application/pdf`, seja qual for o tipo com que foi enviado,
  para que um arquivo nunca rode como página do app.
- **Colar um link sozinho numa linha** pergunta: link, link com prévia ou incorporar. Isso substitui o
  player que aparecia embaixo de um link de vídeo sozinho.
- **Envios:** um áudio solto ou escolhido vira bloco de áudio, um PDF vira bloco de PDF; os outros
  arquivos continuam como cartão. A biblioteca ainda recusa vídeos (precisam de bucket), então o
  bloco de vídeo recebe um link.
- **Menu `/`:** Mídia ganha galeria, vídeo, áudio e PDF; Incorporar ganha link com prévia e incorporar.

## 9. O que a B5 construiu

| Bloco | Guardado como | O que faz |
|---|---|---|
| Mermaid | `mermaid`, `props.code` | O diagrama, desenhado pelo Mermaid (segurança estrita, rótulos como texto SVG, SVG sanitizado); o código é editado embaixo enquanto o bloco está selecionado |
| Matemática | `math`, `props.latex` | A fórmula, desenhada pelo KaTeX (`trust: false`) |
| Bloco sincronizado | `synced` com `children`; as cópias são `synced-ref` com `props.pageId` e `props.blockId` | O original é editado onde está. "Copiar para sincronizar" põe ele na área de transferência; colado em outra página vira uma cópia que mostra os blocos do original só para leitura, com um atalho para editar o original. Uma cópia conta como link de página (tipo `embed`) |
| Botão de modelo | `template-button`, `props.label`, o modelo nos `children` | Um clique põe uma cópia do modelo embaixo dele, com ids novos; "Editar modelo" abre o corpo |
| Decisão | `decision`, `props.status` (`decided`, `rejected`; em aberto quando ausente), o texto em `content` | Uma linha com a situação numa etiqueta, que muda pela própria etiqueta |
| Caminho da página | `breadcrumb` | O espaço, as pastas e as páginas pai da página |
| Lista de subpáginas | `subpages` | As subpáginas da página, na ordem delas |
| Páginas por tag | `tag-pages`, `props.tags` (nomes) | As páginas que têm essas tags |
| Propriedades da página | `properties`, `props.rows[]` com `{ key, value }`, cada um uma lista inline | Uma tabela de duas colunas; o texto entra na busca como o resto da página |

- Mermaid e KaTeX carregam na primeira vez que uma página mostra um deles (chunks separados).
- A cópia sincronizada é só leitura: a edição acontece no original, o que evita duas páginas gravando o
  mesmo bloco. Um relatório de propriedades entre páginas (o "page properties report" do Confluence)
  fica para depois.

## 10. O que a B6 construiu

Novos tipos inline ao lado de `text` e `mention`; o texto da busca lê cada um pelas suas palavras:

| Inline | Guardado como | No editor |
|---|---|---|
| Data | `{ date: { value, reminderId? } }`, `value` `YYYY-MM-DD` ou `YYYY-MM-DDTHH:mm` (local) | "Hoje", "Amanhã" ou a data; um clique muda a data, põe horário e liga ou desliga o lembrete na Agenda |
| Status | `{ status: { label, color? } }`, `color` uma das oito | Uma etiqueta; um clique troca o texto e a cor |
| Item de outro módulo | `{ item: { module, kind, id, label } }` | Tarefas e lembretes da Agenda, contas e cartões das Finanças; o nome fica como era quando foi ligado, e um clique abre o item (a página dele, ou a lista do módulo) |
| Fórmula no texto | `{ math: { latex } }` | KaTeX na linha; `$x^2$` digitado no texto vira uma |

- **`@`** lista páginas, depois datas (`@hoje`, `@amanhã 14h`, `@25/12`, `@2026-12-25`) e, quando algo
  é digitado, itens dos outros módulos. `@lembrar amanhã 9:00` (`@remind …`) cria a data já com o
  lembrete; um dia inteiro toca às 9:00. O lembrete leva o texto da linha, ou o título da página
  quando a linha não tem mais nada.
- **`:`** seguido de uma palavra oferece emoji (`:fogo`, `:fire`, `:check`) de uma lista curta nas duas
  línguas; o painel de emoji do sistema tem o resto.
- **Menu `/`, No texto:** data ou lembrete, status, fórmula no texto.
- Apagar uma data do texto não exclui o lembrete na Agenda; ele continua lá para ser feito ou
  dispensado.

## 11. O que a B7 construiu

Colunas novas em `nte001_page` (no CREATE TABLE dela): `appearance` (jsonb), `is_locked`, `verified_at`,
`verified_until`, `viewed_at` e `public_token` (único enquanto existir). Nenhuma delas entra nas versões
da página.

| Recurso | Como funciona |
|---|---|
| Capa | Uma imagem da biblioteca (que dá para reposicionar) ou um de seis degradês, pelo menu da página ou sobre a capa; capa da biblioteca conta como uso do item |
| Largura, fonte, texto pequeno | `appearance.width` (`normal`, `wide`), `font` (`default`, `serif`, `mono`), `smallText` |
| Travar | Uma página travada abre para leitura, em qualquer tela; a etiqueta "Travada" no cabeçalho destrava |
| Verificação | Verificada por 7, 30, 90 ou 365 dias, ou sem validade; o cabeçalho mostra até quando e "vencida" quando passa |
| Páginas recentes | Abrir uma página grava `viewed_at` direto no banco (não é edição, então nem o "editada" nem as versões mudam); a lateral mostra as cinco últimas |
| Lixeira | Excluir grava um único instante para a página e as subpáginas, para que voltem juntas. Restaurar põe a página sob a página pai, ou no topo do espaço (na pasta dela) quando a pai sumiu, no espaço padrão quando o espaço sumiu, com slug numerado quando uma página viva ficou com ele; links, usos de mídia e tags são refeitos. Excluir para sempre (uma ou a lixeira inteira) apaga as linhas, as versões e todo link de ou para elas |
| Link público | Um token aleatório de 32 bytes; `/p/<token>` lê a página sem entrar. Pelo link só saem os itens da biblioteca que a página usa, uma página mencionada aparece como "Página privada", e desligar e ligar o link cria um novo |
| Exportar | Markdown e um arquivo HTML independente (com as imagens da biblioteca dentro, em data URL); PDF pela janela de impressão do navegador, com uma folha de estilo que deixa só a página |
| Importar | Um arquivo Markdown pelo menu `+` vira página: o primeiro título é o título, `[[Título]]` vira menção de uma página que existe |

## 12. O que a B8 construiu

Duas tabelas novas: `nte013_page_comment` e `nte014_block_stamp`. Nenhuma entra nas versões da página;
excluir a página para sempre apaga as duas.

| Recurso | Como funciona |
|---|---|
| Comentar um trecho | "Comentar" na barra da seleção. A conversa guarda o bloco e as palavras citadas; o editor marca as palavras com uma marca `comment` que só tem o id da conversa, então um bloco pode ter várias conversas sobre as mesmas palavras |
| Comentar um bloco | "Comentar" no menu do bloco (a alça de arrastar). Uma conversa aberta sobre o bloco inteiro aparece como uma linha ao lado dele |
| Conversas | Respostas, editar, excluir (a conversa vai com as respostas), resolver e reabrir. Só o primeiro comentário de uma conversa é resolvido, e uma resposta não pode ser respondida |
| Destaque | A página pinta só as marcas das conversas abertas; uma conversa resolvida ou excluída deixa o texto como estava, sem mudar o editor. Clicar nas palavras comentadas abre a conversa |
| Painel de comentários | O botão da barra, com a contagem de conversas abertas. Listas de abertas e resolvidas; a conversa de um bloco removido continua, com a etiqueta "Bloco removido"; clicar na citação leva ao bloco |
| Datas do bloco | Cada salvamento compara uma impressão digital de cada bloco (o JSON dele sem os filhos) com a guardada: um id novo é um bloco novo, uma impressão diferente é uma edição. O menu do bloco mostra quando ele foi escrito e editado, e se a última edição veio do Assistente ou de uma restauração |

Cada página tem um único usuário, então o autor de um comentário é sempre o dono da página; as datas
do bloco dizem *o que* escreveu a mudança (uma edição, o Assistente, uma restauração).

## 13. O que a B9 já construiu

- IA na barra da seleção: resumir, melhorar e traduzir para um idioma informado. `/Continuar com IA`
  usa os últimos 8000 caracteres anteriores ao cursor. A seleção também é limitada a 8000 caracteres.
- Uma prévia mostra o texto a enviar antes de Gerar, depois oferece substituir a seleção, inserir
  abaixo ou descartar. Continuar oferece inserir abaixo. Gerar e descartar não salvam nada; aceitar
  exige que o documento ainda seja o mesmo da prévia. O resultado entra como texto simples.
- `POST /notes/ai/transform` delega ao `ITextCompletion` de `Assistant.Abstractions`, passando por um
  comando autorizado do Assistant e reutilizando perfil, modelo e chave do usuário no Integrations.
  Exige `notes.pages.update` e `assistant.commands.run`, módulos ativos e perfil ligado.
  Sem tools, histórico de conversa, persistência de prompts ou tabelas novas.
- O PUT da página aceita `source: edit | assistant` (padrão `edit`). O primeiro autosave após aceitar
  IA marca os blocos alterados e a versão como `assistant`; saves manuais seguintes voltam a `edit`.
  A origem é por save: alterações manuais agrupadas no mesmo autosave também recebem essa origem.
- Falha, timeout, resposta vazia ou acima de 16000 caracteres viram erro amigável, sem detalhes do
  provedor. Fechar a prévia cancela a requisição do navegador.

- Resumo da página inteira: o menu de opções da página editável oferece “Resumir página com IA”.
  Edições pendentes são salvas antes de abrir a prévia. `POST /notes/pages/{id}/ai/summary` lê somente
  a página salva do dono e resume seu texto derivado, incluindo células de tabela, propriedades e
  chips. Texto vazio ou acima de 32000 caracteres é recusado antes do provedor, sem corte silencioso.
  Aceitar insere um novo destaque “Resumo” no topo, preservando os blocos existentes e usando o
  autosave com origem Assistente. Descartar não muda o documento. Uma prévia de documento alterado
  não pode ser aceita. Só o texto salvo desta página é enviado; páginas ligadas não são lidas.

- Perguntar às notas: uma aba ao lado da busca aceita pergunta de até 1000 caracteres e consulta o
  espaço atual ou todos os espaços. A busca retira palavras comuns de perguntas em português/inglês
  e procura alternativas no índice tsvector existente; a busca comum de páginas não muda. Até cinco
  páginas salvas (arquivadas incluídas, excluídas fora) fornecem trechos de 4000 caracteres mais
  indicadores de corte, centrados numa palavra encontrada. Títulos e trechos vão ao Assistant do
  usuário. Sem embeddings, busca semântica/sinônimos, tools, histórico de conversa ou gravação de notas.
- O modelo devolve resposta e ids das páginas citadas. O backend recusa respostas malformadas,
  afirmações sem citação e fontes fora das páginas consultadas. Sem resultados ou com indicação
  explícita de evidência insuficiente, aparece uma mensagem localizada. Isso valida a identidade das
  fontes, não a veracidade de cada afirmação; a interface exibe texto simples e trechos com botões
  para abrir as páginas. Mudar pergunta/escopo limpa a resposta; fechar ou sair da aba cancela a requisição.

- Bloco de IA: `/Bloco de IA` insere um `richBlock` de kind `ai`, salvo com `{ prompt, output, generatedAt }`
  nas props. `output` é um array inline de texto simples, já lido pelo percurso de texto derivado para
  busca. `POST /notes/ai/generate { prompt }` manda só o prompt (1–8000 caracteres) pelo Assistant
  configurado e responde texto e instante UTC do servidor. Sem tools, contexto da página ou geração
  automática ao abrir. O resultado aceito anterior permanece durante a prévia; aceitar substitui em
  uma transação com origem Assistente, descartar preserva. Editar o prompt é edição comum e limpa
  prévia obsoleta. Aceitar confere se o bloco de destino continua igual. Leitura/link público exibem
  prompt e resultado salvos, sem controles de geração; os dois fazem parte da página compartilhada.
  Exportação Markdown/HTML/PDF e texto legível incluem o resultado aceito; snapshots preservam todas
  as props e o diff textual mostra prompt/resultado. Sem schema ou sincronizador novo.

- Captura Telegram: `/nota <texto>` no bot Assistant vinculado acrescenta um parágrafo literal numa
  página Captura dedicada, criada no espaço padrão se faltar. Cada parágrafo começa com data/hora do
  recebimento no fuso efetivo da conta. Não passa pelo Gemini nem exige perfil de IA ligado/chave, mas
  exige permissões/flags de execução do Assistant e leitura/criação/edição de páginas e leitura de espaços.
  Renomear/mover/arquivar mantém a identidade da caixa; se excluída, outra é criada sem restaurá-la.
  Uma página comum de mesmo título nunca é reaproveitada. Caixa travada recusa novas capturas.
- `nte015_note_capture` guarda id do evento, dono, página e instante de processamento junto com
  página/histórico/derivados. Reentrega não acrescenta texto nem versão, mesmo após remover a página
  ou o parágrafo capturado. Recibos não guardam texto e sobrevivem à exclusão definitiva da página.
  O relay serial do outbox Channels entrega uma captura por vez. A confirmação pode se repetir numa
  tentativa; o conteúdo não duplica. Capturas só de texto preservam quebras e Markdown literalmente;
  mídia e linguagem natural seguem seus fluxos anteriores. Sem endpoint HTTP novo.

**Implementação da B9 concluída.** Validação real em navegador/provedor/Telegram e revisão do usuário pendentes.
**Validação visual e com provedor real:** pendente; sem commit.

## B10: modelos de página

Construído, sem commit. Marcar/desmarcar no menu da página, seção Modelos na lateral e criação a
partir de qualquer modelo ativo do usuário no espaço/pasta atual. A cópia mantém conteúdo e ícone,
recebe IDs novos em todos os níveis, remove âncoras de comentários e expande `{{date}}` (data ISO no
fuso da conta) e `{{title}}` (título final) nos textos visíveis. O título aceita `{{date}}`.
Subpáginas e configurações não são copiadas; `is_template` não é versionado. Notas diárias e tabela
de propriedades da pasta pendentes; o arrastar foi revisado abaixo. O CREATE
original ganha `is_template`; banco local não recriado nem migrado. Validação no navegador pendente.

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
pendentes. Período/calendário de próximos eventos construídos; ver status de implementação. Blocos Radar/Databases/Projects dependem dos respectivos módulos.

## Escopo restante da B11

Escopo dos módulos disponíveis construído: captura de e-mail, Agenda Hoje/tarefas/próximos eventos
(7/30 dias e calendário) e resumos de Finanças. Integrações restantes dependem de implementar
Radar/Databases/Projects. Próximo: validar navegador/conta real, conferir migrações locais
pendentes preservando o banco e corrigir problemas encontrados. Commit somente com pedido
explícito. Datas fixas personalizadas ficam fora desta fatia de janela móvel aprovada.
