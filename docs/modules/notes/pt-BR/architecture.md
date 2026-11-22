# Arquitetura

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Blocos e editor](blocks.md)

---

## 1. Organização dos projetos

O módulo segue os outros módulos, dividido em projetos por camada em `backend/src/Modules/Notes/`:

```
Pottmayer.Pandora.Modules.Notes.
  Abstractions      → NotesModule, NotesFeatures, NotesPermissions, e as portas que outros módulos
                      chamam: INoteImporter ("salvar no Notes" do Communications), INoteCapture (/nota)
  Application       → Commands, Queries, Dtos, Services, ferramentas do Assistant, DI
  Contracts         → vazio: o módulo não publica eventos de integração
  Domain            → Aggregates, ValueObjects, Errors, Ports (Repositories)
  Infrastructure    → miniaturas com ImageSharp, o leitor de links (HTTP, protegido contra SSRF)
  Persistence       → EntityConfigs, Repositories, o interceptor de versões, NotesDbContext, DI
  Presentation      → Controllers, Requests, DI
```

Aggregates DDD com construtores privados e factories estáticas, um `TimeProvider` para toda leitura de
tempo, e uma pasta por caso de uso na camada de aplicação. Toda escrita passa por um command handler numa
unit of work; leituras passam por query handlers que devolvem DTOs. O módulo **não tem jobs**.

Frontend: `client-web/src/modules/notes`, dividido em `pages/` (rotas), `components/` (com
`components/visual/` para o editor e `components/library/` para a mídia), `hooks/`, `services/`,
`lib/` (lógica pura, com testes) e `models.ts`.

## 2. Peças do domínio

### Aggregates (`Domain/Aggregates`)

| Aggregate | Responsabilidade / invariantes |
|---|---|
| **Space** | Um grupo de páginas; no máximo um padrão por usuário. |
| **Folder** | Um contêiner num espaço, em árvore por `parent_id`; nunca dentro de uma página. |
| **Page** | O documento de blocos mais título, ícone, slug (fixo na criação), lugar na árvore, estados (favorita, arquivada, excluída, modelo) e configurações (aparência, trava, verificação, último acesso, token público). `history_number` conta as versões. |
| **PageVersion** | Um snapshot imutável da página depois de um salvamento que a mudou. |
| **PageLink** | Uma aresta derivada entre duas páginas, tipo `wikilink` ou `embed`. |
| **Tag** / **PageTag** | Um rótulo do usuário e o fato derivado de que uma página o carrega. |
| **PageMedia** | O fato derivado de que uma página usa um item da biblioteca. |
| **BlockStamp** | Quando um bloco foi escrito e editado pela última vez, e a origem da edição. |
| **PageComment** | Um comentário ou resposta, ancorado num bloco e opcionalmente em palavras citadas. |
| **MediaItem**, **MediaFolder**, **MediaRendition** | A biblioteca: itens, pastas, miniaturas. |
| **NotesPreferences** | O modelo diário e a pasta das notas diárias. |
| **DailyNote** | Qual página é a nota diária de uma data, por usuário. |
| **NoteCapture** | Um recibo por captura do Telegram, para um evento reentregue não acrescentar nada. |

Dois **helpers puros** ficam ao lado, no domínio para continuarem testáveis: `PageHierarchy`
(checagem de ciclo, caminhada pela subárvore) e `PageGraph.Neighborhood` (BFS não direcionada do grafo
local).

### Value objects (`Domain/ValueObjects`)

| Tipo | Papel |
|---|---|
| `PageContent` | Lê e valida um documento, e deriva numa passada só: o texto de busca, os ids das páginas mencionadas, os ids das páginas ligadas (blocos de link de página e synced-ref), as tags, os ids de mídia, as impressões digitais dos blocos, as propriedades da página e um texto legível para o Assistant. Também monta um documento a partir de texto puro, junta documentos e copia um modelo com ids novos. |
| `PageAppearance` | Capa (imagem da biblioteca ou gradiente, posição), largura, fonte, texto pequeno. |
| `PageSnapshot` | O que uma versão guarda. |
| `PageVersionSource` | `initial`, `edit`, `assistant`, `restore`. |
| `PageLinkKind` | `wikilink` \| `embed`. |
| `PageProperty` | Uma linha de um bloco de propriedades, como texto puro. |
| `Slugger` | Título → slug: minúsculas, sem acentos, não alfanuméricos viram hífen, no máximo 80 caracteres. |
| `TagName` | Texto da tag → slug: minúsculas, sem acentos, mantém `/`, `-` e `_`, no máximo 50 caracteres, exige uma letra. |
| `PageSearch` | A caixa de busca → um `tsquery` de prefixos seguro, mais o trecho em volta do primeiro acerto. |
| `NoteQuestionSearch` | Uma pergunta → um `tsquery` com OR, sem as palavras comuns de pergunta, para o pergunte às suas notas. |
| `MediaKind` | `image`, `file`, `video` (vídeo recusado no upload por enquanto). |

### Portas

Um `I…Repository` por aggregate em `Domain/Ports/Repositories`, mais `IImageProcessor` (ImageSharp) e
o leitor de links. Os bytes passam pela porta compartilhada `IFileStorage` com a chave
`NotesModule.DatabaseKey`; o backend de banco grava na `nte003_file_blob`. Chamadas de IA passam pelo
`ITextCompletion` do `Assistant.Abstractions`.

### Serviços de aplicação (`Application/Services`)

| Serviço | Papel |
|---|---|
| `PageLinkSynchronizer` | Reconstrói as arestas que saem de uma página a partir do documento, por diff. |
| `PageTagSynchronizer` | O mesmo para as tags, criando as que o documento trouxe e varrendo as sem cor que nenhuma página carrega mais. |
| `PageMediaSynchronizer` | O mesmo para o uso de mídia (blocos e capa). |
| `BlockStampSynchronizer` | Compara a impressão digital de cada bloco com a guardada e carimba blocos novos e editados. |
| `PageDeletion` | Lixeira e exclusão definitiva de uma página e suas subpáginas. |
| `SpaceResolver`, `SpaceValidation` | O espaço padrão, e as checagens do espaço de uma página ou pasta. |
| `TagFilter` | "Que páginas têm todas estas tags?" — uma regra para barra lateral, busca e grafo. |
| `PageTagReader` | As tags da página para os handlers que não mexeram no documento. |
| `NoteImporter`, `NoteCaptureService` | As portas `INoteImporter` e `INoteCapture`. |
| `NoteAnswerParser` | Valida a resposta do modelo no pergunte às suas notas. |

### Versões sem chamada em cada handler

O `PageVersionSaveChangesInterceptor` (Persistence) observa a unit of work: sempre que campos
versionados de uma página mudam, ele acrescenta um snapshot na mesma transação e incrementa o
`history_number`. Assim todo caminho grava o histórico do mesmo jeito: o editor, o Assistant, mover uma
subárvore, uma restauração, uma captura.

## 3. Decisões de design

| # | Decisão | Motivo (alternativa rejeitada) |
|---|---|---|
| **D1** | Um documento `jsonb` por página, id estável por bloco. | A página carrega e salva como uma unidade, e o dado derivado sai dela. Rejeitado: uma linha por bloco. |
| **D2** | TipTap no frontend, com um conversor (`lib/blocks.ts`) entre o JSON do TipTap e os blocos do Pandora. | O banco não depende do editor. Rejeitado: escrever um motor de editor. |
| **D3** | Linhas derivadas são reconciliadas por **diff**, não apagadas e reinseridas. | Mesmo resultado idempotente, e nunca esbarra num índice único dentro de uma transação. |
| **D4** | Links apontam para ids de página. | Renomear nunca quebra um link; nada resolve títulos. |
| **D5** | O backend valida a forma, não cada tipo de bloco. | Bloco novo é trabalho só de frontend. |
| **D6** | A árvore (pastas, páginas-pai) e os links são sistemas separados. | A árvore é arquivo (acíclica), os links são significado (cíclicos). |
| **D7** | Excluir é soft e vai para a lixeira, feito no command, não por cascata no banco. | Subpáginas saem e voltam juntas, e o histórico sobrevive. |
| **D8** | `search_vector` é uma coluna gerada sobre `title` e `content_text`. | O Postgres mantém em dia; configuração `simple` porque o caderno mistura português e inglês. |
| **D9** | Os bytes ficam atrás do `IFileStorage`, com backend e chave em cada linha de mídia. | Um bucket depois não precisa de migration. |
| **D10** | O grafo local é recortado em memória. | Um caderno pessoal é pequeno; BFS em C# fica testável. |
| **D11** | Mídia é servida por endpoint autenticado; uma página pública serve só os itens que usa. | Os bytes são do usuário. |
| **D12** | Uma cópia sincronizada é somente leitura; o original é editado onde mora. | Duas páginas nunca escrevem o mesmo bloco. |
| **D13** | Blocos ao vivo (Agenda, Finances) guardam só a configuração. | Os números nunca entram no documento, nas versões nem numa página pública. |

## 4. Regras transversais

- **Multi-tenant por usuário.** Toda tabela de aggregate tem `user_id`, ou chega a ele pela página.
  Todo endpoint é restrito ao usuário do token; recurso de outro usuário devolve **404**.
- **Permissões e features.** Todo command e query declara sua permissão `notes.*` (`NotesPermissions`)
  e a feature do Notes; ações que tocam outro módulo exigem também a permissão e a feature dele. Ver
  [Admin](../../admin/pt-BR/README.md).
- **`TimeProvider` em todo lugar.** Nenhum aggregate lê `DateTime.Now`.
- **Limites.** Um documento tem no máximo 2 MB, 10.000 blocos e 24 níveis.
