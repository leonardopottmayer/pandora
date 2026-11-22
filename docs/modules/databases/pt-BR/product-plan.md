# Módulo Databases — Plano de Produto

> **Status:** design aprovado (2026-11-07); nada construído ainda. A entrega segue as etapas T1–T6 (§9).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Relacionados: [Admin](../../admin/pt-BR/README.md) (flags e permissões) · [Files](../../files/README.md) ·
> [Assistant](../../assistant/README.md) · [Arquitetura de mensageria](../../../architecture/pt-BR/messaging.md)

---

## 1. O que o módulo faz

O **Databases** deixa cada usuário montar um pequeno banco de dados próprio dentro do Pandora, no
espírito das bases do Airtable ou das databases do Notion. O usuário cria **bases**, cada uma com
**tabelas**, dá a cada tabela os **campos** que quiser (de uma lista de tipos que o Pandora oferece) e
guarda **registros** nelas. As tabelas podem apontar umas para as outras por campos de **relação**.

Em cima disso, o usuário:

- filtra e ordena com um construtor visual de filtros e, depois, com uma sintaxe de busca digitada;
- salva as consultas como **visões**;
- importa arquivos CSV e Excel para uma tabela nova ou existente;
- exporta o resultado de uma visão para CSV e Excel.

```
Base "Leituras"
 ├── Tabela "Livros"    campos: Título (texto, primário) · Autor (relação → Autores) · Nota (inteiro) · Lido (booleano)
 │     visões: "Todos", "Não lidos" (Lido = false, ordenada por Título)
 └── Tabela "Autores"   campos: Nome (texto, primário) · País (escolha única)
```

### O que ele não é

- **Não é um console SQL.** O usuário nunca escreve SQL, e nenhuma entrada do usuário vira texto SQL:
  consultas são uma árvore compilada para SQL parametrizado (§5).
- **Não é compartilhado, ainda.** Cada base pertence a um usuário, como os dados de todos os outros
  módulos. Compartilhamento é uma etapa futura (§9, T6+).
- **Não substitui os outros módulos.** Finances, Agenda e Notes mantêm seus próprios modelos. Uma tabela
  do usuário pode ser *ligada* a eles depois, por eventos ou tipos de campo, nunca fundida com eles.

---

## 2. Nomes e coordenadas

| Item | Valor |
|---|---|
| Nome do módulo | `Databases` (UI: "Bases" em pt-BR, "Databases" em en) |
| Projetos do backend | `Pottmayer.Pandora.Modules.Databases.{Abstractions,Contracts,Domain,Application,Infrastructure,Persistence,Presentation}` |
| Schema PostgreSQL | `databases` |
| Prefixo de tabela | `dbsXXX_`, PK `uuid_generate_v7()` |
| Seção de options | `Pandora:Databases` |
| Base da API | `/api/v{version}/databases`, JWT |
| Frontend | `client-web/src/modules/databases`, rotas `databases`, `databases/:baseId`, `databases/:baseId/tables/:tableId` |
| Migrations | `migrations/migrations/databases/` |

No código, o contêiner de topo do usuário é uma **`Base`** (não `Database`, que em todo lugar seria lido
como o banco do servidor). O módulo mantém o nome `Databases`.

---

## 3. Princípios

1. **Schema é metadado, não DDL.** Criar uma tabela ou um campo grava linhas em `dbs002`/`dbs003`. O
   Pandora nunca roda `CREATE TABLE` nem `ALTER TABLE` para uma tabela do usuário.
2. **Valores são chaveados pelo id do campo.** O `data` de um registro é um objeto JSON chaveado pelo id
   do campo, nunca pelo nome, então renomear um campo mexe em uma linha.
3. **Cada tipo é um plugin.** O que um tipo de campo aceita, como filtra, ordena, importa e exporta vive
   numa classe por tipo (§4). Um tipo novo é uma classe nova, mais o editor dele no frontend.
4. **Um só formato de consulta.** O construtor de filtros, a sintaxe de busca, as visões salvas, os
   exports e (depois) o Assistant produzem a mesma árvore de consulta (§5).
5. **Do usuário.** Toda consulta filtra por `UserId`, como em todos os módulos. Relações só podem apontar
   para tabelas da mesma base.
6. **Limites são configuração.** Quantas bases, tabelas, campos, registros e linhas de import um usuário
   pode ter fica nas options (§6.4), para que um import descontrolado não encha o disco.

---

## 4. Tipos de campo

### 4.1 O contrato

Cada tipo implementa `IFieldType`, registrado no DI e resolvido pelo id:

| Membro | O que faz |
|---|---|
| `Id` | O id do tipo gravado, com hífen (`short-text`, `single-select`) |
| `ValidateConfig(config)` | Valida as configurações do campo (opções da escolha, código da moeda, casas decimais) |
| `Normalize(value, config)` | Transforma um valor JSON recebido na forma gravada, ou devolve um erro de validação |
| `Operators` | Os operadores de filtro que o tipo aceita (§5.2) |
| `SqlExpression(fieldId)` | A expressão SQL que lê o valor de `data` para filtrar e ordenar (`(data->>'<id>')::numeric`) |
| `Parse(text, config)` | Lê uma célula de um arquivo importado, com a cultura do import |
| `Format(value, config)` | Escreve o valor para o export CSV/Excel |
| `ConvertFrom(otherType, value)` | Converte um valor quando o usuário troca o tipo de um campo; `null` quando não dá |

### 4.2 Tipos das primeiras etapas

| Id | Gravado como | Notas |
|---|---|---|
| `short-text` | string | Até 1 000 caracteres; o campo primário padrão |
| `long-text` | string | Até 100 000 caracteres, texto puro |
| `email` | string | Formato validado |
| `url` | string | Só `http`/`https` |
| `integer` | number | 64 bits |
| `decimal` | string | Decimal guardado como string para evitar ponto flutuante binário; config: casas |
| `currency` | string | Como `decimal`; config: código da moeda (`BRL`) |
| `boolean` | boolean | Ausente significa `false` |
| `date` | string `yyyy-MM-dd` | Sem fuso |
| `date-time` | string, ISO 8601 UTC | Exibido no fuso do usuário (preferência do Identity) |
| `single-select` | id da opção | Config: opções com nome e cor |
| `multi-select` | array de ids de opção | Mesma config |
| `relation` | — (em `dbs005`) | Config: tabela alvo, um ou muitos; ver §6.3 |

Planejados para depois: `attachment` (via Files), `formula`, `lookup`/`rollup` (através de uma relação),
`autonumber`, `created-at`/`updated-at`, `user`.

### 4.3 O campo primário

Cada tabela tem exatamente um campo primário (por padrão o primeiro `short-text`). Ele é o título do
registro: o que uma relação mostra, o que a busca casa primeiro, a primeira coluna da grade. Ele não pode
ser excluído, só substituído por outro campo.

### 4.4 Alterando um campo

- **Renomear, reordenar, mudar config:** só metadado.
- **Trocar o tipo:** o backend conta quantos valores o `ConvertFrom` consegue manter e devolve a contagem
  primeiro; o usuário confirma e os valores que não convertem ficam vazios.
- **Excluir:** remove o campo e a chave dele de todos os registros da tabela num único `UPDATE`, e os
  vínculos dele em `dbs005`. As visões que o usavam perdem as condições sobre ele.
- **Remover uma opção de escolha:** o id da opção sai de todos os registros que a tinham.

---

## 5. Consultas

### 5.1 A árvore de consulta

Uma consulta é JSON, gravado como está numa visão e enviado como está pelo cliente:

```json
{
  "filter": {
    "and": [
      { "field": "<lido-id>", "op": "eq", "value": false },
      { "or": [
        { "field": "<nota-id>", "op": "gte", "value": 4 },
        { "field": "<titulo-id>", "op": "contains", "value": "duna" }
      ] }
    ]
  },
  "sort": [ { "field": "<titulo-id>", "dir": "asc" } ],
  "search": "texto livre"
}
```

`search` casa o campo primário e todos os campos de texto (`ILIKE`), para a caixa de busca acima da grade.

### 5.2 Operadores

| Grupo | Operadores | Tipos |
|---|---|---|
| Igualdade | `eq`, `neq` | todos menos `long-text` e `relation` |
| Vazio | `empty`, `not-empty` | todos |
| Texto | `contains`, `not-contains`, `starts-with` | tipos de texto |
| Ordem | `lt`, `lte`, `gt`, `gte`, `between` | números, datas |
| Datas | valores relativos: `today`, `-7d`, `+1m`, `this-week`, `this-month` | `date`, `date-time` |
| Conjuntos | `in`, `not-in` | `single-select` |
| Conjuntos | `has-any`, `has-all`, `has-none` | `multi-select`, `relation` |

### 5.3 Compilação

O compilador percorre a árvore, pede a cada tipo de campo a `SqlExpression` e o SQL do operador, e passa
os valores **sempre** como parâmetros. Campos desconhecidos, operadores que o tipo não aceita e valores
que falham no `Normalize` são recusados com um erro de validação que aponta a condição. Um limite de
profundidade e de condições impede uma consulta de crescer sem fim.

A paginação é por offset (`limit`/`offset`, 50 por página), já que a ordenação pode ser por qualquer
campo.

> ponytail: paginação por offset fica lenta passando de algumas centenas de milhares de linhas; trocar
> por keyset em `(valor da ordenação, id)` se alguma tabela chegar lá.

### 5.4 Sintaxe de busca (T5)

Uma linguagem pequena para quem prefere digitar, convertida na mesma árvore:

```
Lido = false and (Nota >= 4 or Título contains "duna")
[Data de leitura] > -30d and Tags has-any ("ficção", "clássico")
Autor is-empty
```

- Nomes dos campos como estão na tabela; nomes com espaço entre colchetes.
- `and`, `or`, `not`, parênteses; strings entre aspas duplas; datas como `2026-11-07` ou relativas.
- O parser devolve erros com posição ("campo desconhecido `Notaa` na coluna 18, quis dizer `Nota`?").
- O construtor de filtros mostra o texto do filtro atual, e o contrário também.

---

## 6. Pandora: o módulo Databases

### 6.1 Modelo de dados

| Tabela | O que guarda | Colunas principais |
|---|---|---|
| `dbs001_base` | Uma base do usuário | `id`, `user_id`, `name`, `description`, `icon`, `color`, `created_at`, `updated_at` |
| `dbs002_table` | Uma tabela de uma base | `id`, `base_id`, `user_id`, `name`, `description`, `position`, timestamps |
| `dbs003_field` | Um campo de uma tabela | `id`, `table_id`, `name`, `type`, `config jsonb`, `is_primary`, `is_required`, `position` |
| `dbs004_record` | Uma linha | `id`, `table_id`, `user_id`, `data jsonb`, `created_at`, `updated_at` |
| `dbs005_record_link` | Um valor de relação | `field_id`, `from_record_id`, `to_record_id`, `position`; PK nas três primeiras |
| `dbs006_view` | Uma consulta salva | `id`, `table_id`, `name`, `type` (`grid`), `query jsonb`, `layout jsonb` (colunas, larguras, ocultas), `position` |
| `dbs007_import` | Um job de import | `id`, `user_id`, `base_id`, `table_id`, `status`, `file_name`, `format`, `mapping jsonb`, `rows_total`, `rows_imported`, `errors jsonb`, timestamps |

Índices: `dbs004 (table_id, created_at)`, GIN em `dbs004.data` (`jsonb_path_ops`), `dbs005 (to_record_id)`
para buscas reversas. Índices de expressão em campos isolados entram só quando uma tabela real precisar.

Excluir uma base ou uma tabela remove tudo abaixo dela (cascata). As exclusões são definitivas; não há
lixeira nas primeiras etapas.

### 6.2 Por que JSONB

| Opção | Veredito |
|---|---|
| **Um `data jsonb` por registro (escolhida)** | Mudança de schema é metadado, qualquer número de tabelas por usuário, um só conjunto de migrations. Indexável com GIN e índices de expressão. |
| Uma tabela física por tabela do usuário | Colunas e constraints de verdade, mas DDL em runtime, fora do migris, com schema divergente e backups arriscados. |
| EAV (uma linha por valor) | Flexível, mas toda consulta vira N joins e fica lenta. |

### 6.3 Relações

Um campo `relation` não grava nada em `data`; os valores dele são linhas em `dbs005_record_link`.

- Config: `targetTableId` (só da mesma base) e `multiple` (um ou muitos).
- **Lado reverso:** criar uma relação cria também o campo correspondente na tabela alvo (estilo
  Airtable), então "Autores" mostra os livros que apontam para cada autor. Excluir qualquer lado exclui
  os dois.
- Excluir um registro exclui os vínculos dele dos dois lados.
- Na grade, uma relação mostra o campo primário dos registros alvo; o editor é um seletor de registros
  com busca.

### 6.4 Options

```json
"Pandora": {
  "Databases": {
    "Limits": {
      "MaxBases": 50,
      "MaxTablesPerBase": 50,
      "MaxFieldsPerTable": 100,
      "MaxRecordsPerTable": 50000,
      "MaxImportRows": 50000,
      "MaxImportFileMegabytes": 20,
      "MaxExportRows": 100000
    }
  }
}
```

### 6.5 Import (T4)

Um assistente em três passos, apoiado em `dbs007_import`:

1. **Upload** de um arquivo CSV ou XLSX (e, no XLSX, a escolha da planilha). O arquivo é lido em
   streaming.
2. **Mapeamento.** O backend devolve as colunas, as primeiras linhas e um tipo inferido para cada
   coluna. O usuário escolhe "tabela nova" (cada coluna vira um campo, o tipo pode ser trocado) ou
   "tabela existente" (cada coluna vai para um campo ou é ignorada), além da cultura do arquivo (vírgula
   decimal, formato de data).
3. **Execução.** Arquivos pequenos rodam na própria requisição; os grandes, como job em background. O
   resultado diz quantas linhas entraram e lista as rejeitadas com o motivo, para baixar em CSV.

Os formatos de import são implementações de `IRecordImporter` escolhidas pelo formato, então JSON ou
outro formato é mais uma classe.

### 6.6 Export (T4)

A visão atual (filtro, ordenação e colunas visíveis) é exportada em streaming para CSV (UTF-8 com BOM,
para o Excel abrir os acentos certo) ou XLSX. Relações exportam o campo primário dos registros alvo;
escolhas exportam o nome das opções. Como no import, os formatos são implementações de
`IRecordExporter`.

Biblioteca de planilhas: **MiniExcel** (leitura e escrita em streaming, Apache 2.0). O ClosedXML carrega
o arquivo inteiro em memória; o EPPlus tem licença comercial.

### 6.7 Flags e permissões

Declaradas em `Databases.Abstractions` como constantes (`DatabasesFeatures`, `DatabasesPermissions`),
como em todo módulo:

| Tipo | Chave | Protege |
|---|---|---|
| Feature | `databases` | O módulo inteiro |
| Feature | `databases.import` | Import |
| Feature | `databases.export` | Export |
| Permissão | `databases.bases.{read,create,update,delete}` | Bases, tabelas, campos e visões (o schema) |
| Permissão | `databases.records.{read,create,update,delete}` | Registros e seus vínculos |
| Permissão | `databases.imports.run` | Rodar um import |
| Permissão | `databases.exports.run` | Rodar um export |

O papel padrão `user` recebe todas.

### 6.8 API (esboço)

| Método | Rota | O quê |
|---|---|---|
| `GET/POST` | `/databases/bases` | Listar, criar bases |
| `GET/PUT/DELETE` | `/databases/bases/{baseId}` | Uma base, com as tabelas |
| `POST` | `/databases/bases/{baseId}/tables` | Criar uma tabela (com um campo primário) |
| `GET/PUT/DELETE` | `/databases/tables/{tableId}` | Uma tabela, com campos e visões |
| `POST` | `/databases/tables/{tableId}/fields` | Adicionar um campo |
| `PUT/DELETE` | `/databases/fields/{fieldId}` | Alterar, excluir um campo |
| `POST` | `/databases/fields/{fieldId}/type-change-preview` | Quantos valores sobrevivem a uma troca de tipo |
| `POST` | `/databases/tables/{tableId}/records/query` | Rodar uma árvore de consulta, paginada |
| `POST` | `/databases/tables/{tableId}/records` | Criar um registro |
| `PATCH/DELETE` | `/databases/records/{recordId}` | Alterar alguns campos, excluir |
| `POST` | `/databases/tables/{tableId}/records/delete` | Excluir vários |
| `GET/POST` | `/databases/tables/{tableId}/views` | Listar, criar visões |
| `PUT/DELETE` | `/databases/views/{viewId}` | Alterar, excluir uma visão |
| `POST` | `/databases/tables/{tableId}/export` | Exportar uma árvore de consulta (formato no corpo) |
| `POST` | `/databases/imports` · `/imports/{id}/mapping` · `/imports/{id}/run` · `GET /imports/{id}` | O assistente de import |
| `POST` | `/databases/query/parse` | Sintaxe de texto → árvore de consulta (T5) |

### 6.9 Frontend

- Página **Bases:** cards com ícone e cor, criar e renomear.
- Página **Base:** tabelas como abas, a grade da tabela ativa abaixo.
- **Grade:** colunas por campo, edição inline com o editor do tipo, adicionar linha no fim, seleção de
  várias linhas para excluir em lote, menu da coluna (renomear, trocar tipo, ocultar, ordenar, excluir),
  drawer do campo para a config.
- **Barra de ferramentas:** seletor de visão, construtor de filtros, ordenação, caixa de busca, campos
  ocultos, import, export.
- Cada tipo de campo tem o renderizador de célula e o editor num só lugar (`fieldTypes/<type>.tsx`),
  espelhando o registro do backend.

---

## 7. Pontos de extensão

| Ponto | Como estende | Exemplo |
|---|---|---|
| `IFieldType` | Uma classe nova (+ renderizador/editor de célula) | `formula`, `attachment`, `rating` |
| Árvore de consulta | Operadores novos num tipo; novos produtores da árvore | O Assistant transformando "livros não lidos" num filtro |
| `IRecordImporter` / `IRecordExporter` | Uma classe de formato nova | JSON, ODS |
| `type` da visão | Um layout novo lendo a mesma consulta | `kanban` (por uma escolha), `calendar` (por uma data), `gallery` |
| Eventos de integração | `RecordCreated/Updated/Deleted` pelo outbox, publicados quando houver um consumidor | Um lembrete do Agenda a partir de um campo de data |
| Ferramentas do Assistant | Ferramentas sobre os comandos da Application | "adiciona o livro Duna do Herbert, nota 5" |

Nada nas duas últimas linhas é construído até uma etapa precisar.

---

## 8. Decisões

| # | Decisão | Escolhida | Em vez de |
|---|---|---|---|
| 1 | Onde ficam os valores | `data jsonb` chaveado pelo id do campo | Tabelas físicas por tabela do usuário; EAV |
| 2 | Relações | Tabela de vínculos `dbs005` com campo reverso | Arrays de ids em `data` |
| 3 | Busca | Árvore de consulta + sintaxe própria pequena (T5), construtor de filtros antes (T2) | Só o construtor; SQL cru |
| 4 | Import | Assistente em três passos com mapeamento e relatório de linhas rejeitadas | Import direto sem mapeamento |
| 5 | Biblioteca de planilhas | MiniExcel | ClosedXML; EPPlus |
| 6 | Nome do módulo | `Databases` (entidade de topo `Base`) | `Tables`; `Collections` |
| 7 | Acesso | Por usuário, flags e permissões no catálogo do Admin, limites nas options | Compartilhamento desde o início |

---

## 9. Entrega

| Etapa | Entrega | Pronto quando |
|---|---|---|
| **T1** | Bases, tabelas, campos (tipos do §4.2 menos `relation`), CRUD de registros, a grade com edição inline e paginação, flags e permissões | Um usuário monta "Livros" com cinco tipos de campo, edita linhas na grade e troca o tipo de um campo |
| **T2** | A árvore de consulta, construtor de filtros, ordenação, caixa de busca, visões salvas | Uma visão "Não lidos" filtra, ordena e sobrevive a um reload |
| **T3** | `relation` com campo reverso, seletor de registros, `has-any/has-all/has-none` | "Livros → Autores" mostra os dois lados e filtra por autor |
| **T4** | Export CSV/XLSX de uma visão; import CSV/XLSX com o assistente | Uma planilha de 5 000 linhas é importada numa tabela nova e exportada de volta com os mesmos valores |
| **T5** | Sintaxe de busca em texto, com erros por posição | Os exemplos do §5.4 são lidos e trazem o mesmo resultado do construtor |
| **T6+** | Fórmulas e rollups, visões kanban e calendário, anexos, ferramentas do Assistant, compartilhamento | Uma de cada vez, cada uma com seu plano |
