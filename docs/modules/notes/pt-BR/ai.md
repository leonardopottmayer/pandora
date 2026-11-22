# IA

[← Voltar ao índice](README.md) · Relacionados: [Busca](search.md), [Assistant](../../assistant/pt-BR/README.md)

O Notes só chama um modelo pelo [Assistant](../../assistant/pt-BR/README.md): o `ITextCompletion` do
`Assistant.Abstractions` usa o perfil, o modelo e a chave do Integrations configurados no Assistant.
Toda ação de IA exige a permissão do Notes sobre a página, `assistant.commands.run`, os dois módulos
ligados e um perfil do Assistant ativo. Nada é gerado sozinho: o usuário pede, vê uma prévia, e aceita
ou descarta.

---

## 1. Ações de texto

- **Resumir, melhorar, traduzir** pela barra de seleção; **Continuar com IA** pelo menu `/`, usando os
  últimos 8.000 caracteres antes do cursor. Uma seleção tem no máximo 8.000 caracteres.
- `POST /notes/ai/transform { action, text, language?, context? }`.
- Uma prévia mostra o resultado antes de mudar qualquer coisa: substituir a seleção, inserir abaixo,
  ou descartar. Aceitar exige que o documento ainda bata com a prévia. O resultado entra como texto
  puro.
- **Autoria:** o PUT da página aceita `source: edit | assistant`. O primeiro autosave depois de aceitar
  marca os blocos alterados e a versão como `assistant`; os salvamentos manuais seguintes voltam a ser
  `edit`.
- Falhas do provedor, tempo esgotado, respostas vazias e respostas acima de 16.000 caracteres viram
  erros amigáveis, sem detalhes do provedor. Fechar a prévia cancela a requisição.

## 2. Resumo da página

"Resumir página com IA" no menu da página grava as edições pendentes, e então
`POST /notes/pages/{id}/ai/summary` resume o texto derivado **salvo** da página (tabelas, propriedades e
chips incluídos). Texto vazio ou acima de 32.000 caracteres é recusado sem chamar o provedor. Aceitar
insere um callout de informação "Resumo" no topo. Só o texto desta página é enviado; páginas ligadas
não são lidas.

## 3. Pergunte às suas notas

Uma aba ao lado da busca recebe uma pergunta de até 1.000 caracteres, no espaço atual ou em todos
(`POST /notes/ai/ask`).

1. O `NoteQuestionSearch` tira as palavras comuns de pergunta em português e inglês e monta um
   `tsquery` com **OU** sobre o vetor de busca existente. A busca comum não muda.
2. No máximo **cinco** páginas (arquivadas incluídas, excluídas não) contribuem com trechos de até
   4.000 caracteres, centrados numa palavra encontrada.
3. Os títulos e trechos vão ao modelo, que precisa responder `{ answer, pageIds }`.
4. O `NoteAnswerParser` recusa respostas malformadas, respostas sem citação, e citações fora das
   páginas enviadas. Nenhum resultado, ou um "não há informação suficiente" explícito, mostra uma
   mensagem traduzida.

A resposta é texto puro com botões para abrir as páginas citadas e ver os trechos. A busca é léxica,
sem sinônimos nem semântica; a ferramenta `ask_notes` do Assistant a amplia com palavras que o modelo
acrescenta (§6). Nada é gravado.

## 4. O que sai do Pandora

| Ação | Enviado ao provedor |
|---|---|
| Ações de texto | A seleção, ou os 8.000 caracteres antes do cursor |
| Resumo da página | O texto derivado daquela página |
| Pergunte às suas notas | A pergunta e trechos de até cinco páginas |
| Bloco de IA | Só o prompt do bloco |

## 5. Bloco de IA

`/Bloco de IA` insere um bloco `ai` que guarda `{ prompt, output, generatedAt }`.

- `POST /notes/ai/generate { prompt }` (1–8.000 caracteres) envia só o prompt, sem contexto da página,
  e devolve texto puro com o horário UTC do servidor.
- A saída anterior continua enquanto uma nova está em prévia; aceitar a substitui como edição de origem
  Assistant, e descartar a deixa como está. Aceitar confere que o bloco não mudou.
- `output` é uma lista de inlines de texto puro, então busca, exportações, texto legível e diffs de
  versão o incluem.
- Visões somente leitura e públicas mostram o prompt e o resultado salvos, sem gerar.

## 6. Ferramentas do Assistant

O Notes contribui com cinco ferramentas para o [Assistant](../../assistant/pt-BR/README.md)
(`Application/Assistant`):

| Ferramenta | Faz |
|---|---|
| `create_note` | Cria uma página a partir de texto (um bloco por linha; linhas começando com `#`, `-`, `1.` viram títulos e listas), opcionalmente num espaço pelo nome |
| `search_notes` | Lista as páginas encontradas, numeradas, para o "abre o 2" funcionar em seguida |
| `read_note` | Lê uma página como texto |
| `append_to_note` | Acrescenta texto a uma página |
| `ask_notes` | Responde a uma pergunta a partir das notas (§3), com `search_terms` que o modelo acrescenta para ampliar a busca |

## 7. Captura pelo Telegram

`/nota <texto>` no bot do Assistant vinculado acrescenta um parágrafo literal a uma página
**Captura** dedicada, criada no espaço padrão quando precisa.

- Sem chamada ao modelo: não precisa de perfil de IA nem de chave, só das permissões do Assistant e do
  Notes.
- Cada parágrafo começa com a data e a hora do recebimento no fuso da conta. O texto fica literal: as
  quebras de linha são mantidas, e linhas que parecem Markdown não viram títulos nem comandos.
- A `nte015_note_capture` guarda o id do evento de entrada, gravado na mesma transação da página, então
  um evento reentregue não acrescenta nada, mesmo depois de a página ou o parágrafo serem removidos.
- Renomear, mover ou arquivar a página Captura a mantém em uso; uma excluída é substituída. Uma página
  com o mesmo título nunca é adotada. Uma página Captura travada recusa a captura.
