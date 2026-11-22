# Status de implementação

[← Voltar ao índice](README.md)

O que está construído, o que ficou de fora de propósito e os pontos em aberto conhecidos. O que falta
está em [product-plan.md](product-plan.md).

---

## Implementado

| Área | Notas |
|---|---|
| **Estrutura do módulo** | Sete projetos em camadas, schema `notes` `nte001`–`nte017`, registrado no Host. Sem jobs, sem eventos de integração. |
| **Documento de blocos** | `PageContent`: validação da forma, texto de busca, links, tags, mídia, impressões digitais e propriedades derivados numa passada; limites de 2 MB, 10.000 blocos, 24 níveis. Ver [Blocos e editor](blocks.md). |
| **Editor** | TipTap 3 com todos os blocos de [Blocos e editor](blocks.md): menus `/`, `@` e `:`, menu do bloco, seleção de vários blocos, links para um bloco, sumário, autosave de 800 ms. |
| **Espaços, pastas, páginas** | Espaço padrão, pastas, subpáginas, movimentos atômicos na árvore, visão geral, página da pasta em lista, cartões e tabela de propriedades somente leitura. Ver [Páginas e hierarquia](pages-and-hierarchy.md). |
| **Configurações da página** | Capa, largura, fonte, texto pequeno, trava, verificação, páginas recentes, link público. |
| **Lixeira** | Excluir com as subpáginas, restaurar no melhor lugar que sobrou, excluir de vez, esvaziar. |
| **Modelos e notas diárias** | `is_template`, nova a partir de modelo com ids novos e `{{date}}`/`{{title}}`; notas diárias na pasta diária a partir do modelo diário (`nte016`, `nte017`). |
| **Exportar e importar** | Exportação em Markdown, HTML e PDF (impressão); importação de Markdown. |
| **Links** | Menções, blocos de link de página, blocos sincronizados, "Mencionada em" (`nte004`). Ver [Links e backlinks](links-and-backlinks.md). |
| **Tags** | Bloco de tags, normalização, criação ao salvar, varredura de órfãs que mantém as coloridas, filtros por interseção (`nte005`, `nte006`). |
| **Busca** | `tsvector` gerado sobre título e texto derivado, `tsquery` de prefixos, título primeiro e depois `ts_rank_cd`, filtros antes do limite de 20, trechos com destaque, `Ctrl+K`. |
| **Biblioteca de mídia** | Itens em pastas, miniaturas WebP, soft delete que mantém os bytes, que páginas usam cada item, espaço usado e sem uso (`nte002`, `nte003`, `nte008`–`nte010`). |
| **Grafo** | Grafo global e local, famílias, recolher, controles de câmera, filtros de visibilidade, inspeção. Ver [Grafo](graph-view.md). |
| **Histórico de versões** | Snapshots completos com origem, comparação, prévia, restauração (`nte011`). |
| **Comentários e datas dos blocos** | Conversas em palavras ou blocos, resolver, painel; carimbos dos blocos com origem (`nte013`, `nte014`). Ver [Comentários](comments.md). |
| **IA** | Resumir, melhorar, traduzir, continuar; resumo da página; pergunte às suas notas; bloco de IA; captura `/nota` pelo Telegram (`nte015`). Ver [IA](ai.md). |
| **Outros módulos** | Blocos ao vivo do Agenda e do Finances; chips de data com lembrete no Agenda; itens de outros módulos; salvar um email no Notes (Communications, pelo `INoteImporter`); cinco ferramentas do Assistant. |
| **Frontend** | `client-web/src/modules/notes`, i18n EN + PT-BR, testes unitários sobre `lib/`. |
| **Testes** | Testes unitários do domínio (`Modules.Notes.Tests`: `PageContent`, hierarquia, grafo, busca, slugger, nome de tag, ferramentas) e testes de integração (`IntegrationTests/Modules/Notes`). |

## Fora de propósito (não é dívida)

- **Desenhar uma página inteira dentro de outra.** Um link de página mostra um cartão ou as primeiras
  linhas; um bloco sincronizado mostra um bloco.
- **Editar uma cópia sincronizada no lugar.** O original é editado onde mora.
- **Renomear tag em todas as páginas** e **hierarquia de tags.**
- **Endpoints de criar/apagar tag.** As páginas criam tags, a varredura remove.
- **`ON DELETE CASCADE` na árvore de páginas.** Os commands percorrem a subárvore eles mesmos.
- **Edição inline na tabela de propriedades da pasta.** Ela é somente leitura.

## Pontos em aberto conhecidos

1. **Verificações com dados reais.** As ações de IA, a captura pelo Telegram e os blocos ao vivo do
   Agenda e do Finances foram construídos e cobertos por testes; não há registro de verificação com o
   provedor real, o Telegram e contas reais.
2. **Retenção dos bytes.** Os bytes da mídia ficam em `bytea`; ir para object storage é o gatilho para
   um backend em bucket.
3. **Bytes sem uso.** Excluir um item mantém os bytes, já que páginas e versões antigas podem ainda
   mostrá-lo. Nada os varre; o lugar "Sem uso" da biblioteca lista os itens, e apagar é decisão do
   usuário.
4. **Grafo em memória.** Certo para um caderno pessoal; é a primeira coisa a rever se o grafo crescer.
5. **Filtros de tag não vão para a URL**, então "páginas com a tag x" não é um link compartilhável.
6. **O pergunte às suas notas é léxico.** Sem sinônimos nem semântica; o `ask_notes` do Assistant
   amplia as palavras pelo modelo.
