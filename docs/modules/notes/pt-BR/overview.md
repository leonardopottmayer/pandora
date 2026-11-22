# Visão geral — Produto e princípios

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Blocos e editor](blocks.md)

---

## 1. O que o módulo faz

O **Notes** é uma base de conhecimento pessoal dentro do monolito modular Pandora. Ele permite a um
usuário:

- Organizar páginas em **espaços** e **pastas**, com **subpáginas** sob páginas, numa árvore na barra
  lateral com arrastar e soltar; abrir a **visão geral** do espaço, a página de uma pasta, as
  **páginas recentes** e a **nota do dia** pelos atalhos no topo da barra lateral.
- Escrever num **editor de blocos** como o do Notion: títulos, listas, tarefas, listas de alternância,
  colunas, callouts, tabelas, código, citações, imagens, galerias, vídeo, áudio, PDFs, bookmarks,
  embeds, Mermaid, fórmulas, blocos sincronizados, botões de modelo, decisões, propriedades da página
  e blocos ao vivo do Agenda e do Finances. `/` abre o menu de blocos, `@` menciona páginas, datas e
  itens de outros módulos.
- Ligar páginas **mencionando-as** ou com um **bloco de link de página**, e ver quem menciona a página
  aberta ("Mencionada em").
- Marcar páginas com um **bloco de tags** no topo, e filtrar a barra lateral, a busca e o grafo por
  elas.
- Manter uma **biblioteca de mídia** de imagens e arquivos, em pastas, com miniaturas.
- Achar qualquer coisa com a **busca de texto** (`Ctrl+K`), ou **fazer uma pergunta** e receber a
  resposta com as fontes.
- Ver a rede como um **grafo**, global ou em volta da página aberta.
- Voltar no tempo com o **histórico de versões**, comentar em texto ou blocos, e ver quando cada bloco
  foi escrito.
- Vestir a página com **capa**, largura, fonte, trava e verificação; compartilhar por **link
  público**; exportar em Markdown, HTML ou PDF, e importar Markdown.
- Usar **modelos** e **notas diárias**, e rodar ações de **IA** (resumir, melhorar, traduzir,
  continuar, bloco de IA) pelo Assistant.

## 2. Princípios

1. **Uma página é um documento de blocos.** Um documento `jsonb` por página, cada bloco com id
   estável. É nos ids estáveis que se apoiam comentários, links para um bloco, blocos sincronizados e
   as datas dos blocos.
2. **Dado derivado é reconstruído, nunca editado.** Links, tags, uso de mídia, texto de busca e datas
   dos blocos são recalculados do documento a cada salvamento, reconciliados por diff: salvar o mesmo
   documento duas vezes não muda nada.
3. **O backend confere a forma, não cada tipo de bloco.** Tamanho, profundidade, quantidade de blocos,
   ids únicos e o padrão do tipo são validados; o que as props de cada tipo significam é com o
   frontend. Um tipo novo de bloco não exige mudança no backend, a menos que traga dado derivado.
4. **Links apontam para ids.** Uma menção guarda o id da página, então renomear nunca quebra um link e
   nada resolve títulos.
5. **Hierarquia ≠ grafo.** A árvore (pastas e páginas-pai) é o arquivo e proíbe ciclos; os links são a
   rede de significado e esperam ciclos.
6. **Excluir vai para a lixeira.** Uma página excluída e suas subpáginas mantêm as linhas até serem
   restauradas ou excluídas de vez.

## 3. Linguagem ubíqua (glossário)

| Termo | Significado |
|---|---|
| **Espaço** | Um grupo de páginas e pastas, como um espaço do Confluence. Um é o padrão. |
| **Pasta** | Um contêiner de pastas e páginas dentro de um espaço. Nunca dentro de uma página. |
| **Página** | Um documento de blocos do usuário, o aggregate principal do módulo. Tem título, ícone, slug, lugar na árvore e configurações (capa, trava, verificação, link público). |
| **Subpágina** | Uma página cujo `parent_id` é outra página. Fica no espaço da página-pai. |
| **Bloco** | Uma unidade do documento: `id`, `type`, `props` opcionais, `content` inline e blocos filhos. |
| **Inline** | Um trecho de texto dentro de um bloco: texto com marcas, uma menção a página, ou um chip (data, status, item de outro módulo, fórmula inline). |
| **Menção** | Uma referência inline a uma página pelo id. Cria uma aresta `wikilink`. |
| **Bloco de link de página** | Um bloco que mostra outra página como cartão ou prévia. Cria uma aresta `embed`. |
| **Bloco sincronizado** | Um bloco cujas cópias (`synced-ref`) o mostram somente leitura em outras páginas. |
| **Backlink / "Mencionada em"** | A leitura reversa de uma aresta: as páginas que referenciam a aberta. |
| **Tag** | Um rótulo do bloco de tags da página. O slug é a identidade por usuário; a cor é a única coisa que a linha da tag acrescenta. |
| **Item de mídia** | Uma imagem ou arquivo da biblioteca, mais o par `(storage_backend, storage_key)` que localiza os bytes. |
| **Versão** | Um snapshot completo da página depois de um salvamento que a mudou. |
| **Carimbo do bloco** | Quando um bloco foi escrito e editado pela última vez, e por quê (edição, Assistant, restauração). |
| **Modelo** | Uma página marcada `is_template`; páginas novas a copiam com ids de bloco novos. |
| **Nota diária** | A página de uma data, criada na primeira abertura a partir do modelo diário. |

## 4. Escopo

### Dentro do escopo (implementado — ver [Status de implementação](implementation-status.md))

Espaços, pastas, páginas e subpáginas com arrastar e soltar; o editor de blocos com todos os blocos de
[Blocos e editor](blocks.md); menções, links de página, blocos sincronizados e backlinks; tags com cores
e filtros; busca de texto e pergunte às suas notas; a biblioteca de mídia; grafos global e local;
histórico de versões; comentários e datas dos blocos; capa, largura e fonte, trava, verificação,
páginas recentes; lixeira; links públicos; exportação em Markdown, HTML e PDF e importação de Markdown;
modelos e notas diárias; ações de IA; captura pelo Telegram; salvar um email no Notes; blocos ao vivo do
Agenda e do Finances; as ferramentas do Assistant.

### Fora do escopo / futuro

| Recurso | Status |
|---|---|
| **Blocos do Databases e do Projects, item do Radar para página** | Esperam esses módulos; ver [Plano de produto](product-plan.md). |
| **Edição simultânea** | Não planejada. Os ids estáveis dos blocos deixam espaço para ela. |
| **Filtros de tag na URL** | Não implementado: os filtros ficam no estado do componente. |
| **Renomear tag em todas as páginas, hierarquia de tags** | Fora. |
| **Armazenamento em bucket e upload de vídeo** | Previstos no desenho, sem data. Ver [Biblioteca de mídia](media-library.md). |
| **Log de eventos de auditoria** | Não faz parte deste módulo. O histórico de versões e as datas dos blocos cobrem o que mudou. |
