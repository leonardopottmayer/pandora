# Histórico de versões

Cada salvamento que altera a página preserva um snapshot completo e imutável em
`notes.nte011_page_version`. Não há expiração ou descarte de versões intermediárias.
Salvamentos sem mudanças não criam versões; restaurar cria uma versão mesmo que o conteúdo coincida.

## Uso

Abra **Histórico de versões** pelo botão de relógio na barra da página. O editor aguarda os saves
pendentes antes de abrir; falhas mantêm o rascunho e impedem essa transição.

O histórico lista as versões da mais recente para a mais antiga, em lotes de 100. Edições manuais
consecutivas com até cinco minutos de intervalo são agrupadas apenas na apresentação: expandir o
grupo revela cada salvamento. Criações, restaurações e ações do assistente aparecem separadamente.

Os seletores permitem comparar quaisquer duas versões carregadas, em qualquer direção. O corpo
mostra um diff por linha de Markdown; os metadados mostram antes/depois. A prévia renderiza a versão
selecionada sem modificar a página. A comparação tem limite de tempo de um segundo; se excedido,
as duas versões continuam acessíveis pela prévia.

## Restauração e integridade

Restaurar repõe título, corpo, ícone, espaço, página mãe, posição, favorita e arquivamento. O slug e
a identidade da página permanecem. Tags, wikilinks e uso de mídia são recalculados a partir do corpo.
Ao mudar o espaço, os descendentes acompanham e recebem suas próprias versões.

O backend valida proprietário, espaço, página mãe e ciclos antes de aplicar. Se a localização antiga
não existe mais ou a página mãe está em outro espaço, a operação falha sem restauração parcial.
O cliente envia o número da versão atual conhecida; uma mudança posterior exige recarregar o
histórico antes de tentar novamente. O contador também protege gravações simultâneas no banco.
Voltar para a aba ou reconectar a rede não avança automaticamente essa versão de referência.
O diff de metadados compara os valores originais, inclusive os IDs de espaços e páginas mãe,
mesmo quando os nomes exibidos são iguais.

A restauração gera uma nova versão com origem `restore` e `restoredFrom`, preservando as anteriores.
Snapshots e alterações da página são gravados na mesma transação, pelo interceptor do Notes.
Mudanças feitas via assistente, movimentos de subárvores e soft delete passam pelo mesmo caminho.

## Páginas existentes, exclusão e mídia

A migration `20261102140000` cria uma versão inicial do estado das páginas já existentes, inclusive
as excluídas. Não reconstrói alterações anteriores à implantação. Exclusão continua sendo soft delete:
as versões permanecem no banco, mas os endpoints não expõem páginas excluídas. Esta entrega não
adiciona uma lixeira nem restauração de páginas excluídas.

Conforme a convenção pré-1.0 do projeto, `history_number` foi acrescentado ao `CREATE TABLE` original
de `nte001_page`; a migration nova cria apenas a nova tabela. Um banco local com o schema anterior
precisa ser atualizado/recriado antes de executar esta versão. A implementação não recria nem apaga
o banco local automaticamente.

Os snapshots preservam referências à mídia, sem duplicar seus bytes. A biblioteca atual mantém os
bytes de itens excluídos logicamente; uma futura purga física precisará considerar essas referências.
O histórico de páginas não é um backup independente do banco.

## API

Todas as rotas exigem autenticação e pertencimento da página ao usuário:

| Método | Rota | Resposta |
|---|---|---|
| GET | `/api/v1/notes/pages/{id}/versions?before={number}` | Até 100 resumos, número decrescente; `before` é exclusivo |
| GET | `/api/v1/notes/pages/{id}/versions/{number}` | Snapshot completo daquela versão |
| POST | `/api/v1/notes/pages/{id}/versions/{number}/restore` | Página restaurada; corpo `{ "expectedVersion": 18 }` |

Recurso inexistente ou de outro usuário retorna 404; concorrência e destinos inválidos retornam 409
(ou 404 se o espaço/pai não existir). A listagem não carrega os corpos históricos do banco.
