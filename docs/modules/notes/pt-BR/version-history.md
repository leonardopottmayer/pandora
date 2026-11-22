# Histórico de versões

[← Voltar ao índice](README.md) · Relacionados: [Páginas e hierarquia](pages-and-hierarchy.md), [Comentários](comments.md)

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
é comparado linha a linha sobre o texto legível (uma linha por bloco, como o Assistant lê); os metadados mostram antes/depois. A prévia renderiza a versão
selecionada sem modificar a página. A comparação tem limite de tempo de um segundo; se excedido,
as duas versões continuam acessíveis pela prévia.

## Restauração e integridade

Restaurar repõe título, corpo, ícone, espaço, página mãe, posição, favorita e arquivamento. O slug e
a identidade da página permanecem. Tags, links, usos de mídia e datas dos blocos são recalculados a partir do documento restaurado; os
blocos restaurados ficam com a origem `restore`.
Ao mudar o espaço, os descendentes acompanham e recebem suas próprias versões.

O backend valida proprietário, espaço, página mãe e ciclos antes de aplicar. Se a localização antiga
não existe mais ou a página mãe está em outro espaço, a operação falha sem restauração parcial.
O cliente envia o número da versão atual conhecida; uma mudança posterior exige recarregar o
histórico antes de tentar novamente. O contador também protege gravações simultâneas no banco.
Voltar para a aba ou reconectar a rede não avança automaticamente essa versão de referência.
O diff de metadados compara os valores originais, inclusive os IDs de espaços e páginas mãe,
mesmo quando os nomes exibidos são iguais.

Toda versão tem uma origem: `initial`, `edit`, `assistant` (um resultado de IA aceito, uma captura, uma
ferramenta do Assistant) ou `restore` (com `restoredFrom`). O `PageVersionSaveChangesInterceptor` grava
as alterações da página e o snapshot na mesma transação, inclusive edições do Assistant, movimentos de
subárvores e exclusão.

## Exclusão e mídia

Uma página na lixeira mantém as versões; excluí-la de vez as remove. Aparência, trava, verificação,
link público e a marca de modelo não entram nas versões.

Os snapshots guardam referências à mídia sem copiar os bytes. A biblioteca mantém os bytes depois que
um item é excluído, então uma versão antiga ainda mostra as imagens dela; uma futura purga dos bytes
precisará considerar essas referências. O histórico de páginas não é um backup do banco.

## API

Todas as rotas exigem autenticação e pertencimento da página ao usuário:

| Método | Rota | Resposta |
|---|---|---|
| GET | `/api/v1/notes/pages/{id}/versions?before={number}` | Até 100 resumos, número decrescente; `before` é exclusivo |
| GET | `/api/v1/notes/pages/{id}/versions/{number}` | Snapshot completo daquela versão |
| POST | `/api/v1/notes/pages/{id}/versions/{number}/restore` | Página restaurada; corpo `{ "expectedVersion": 18 }` |

Recurso inexistente ou de outro usuário retorna 404; concorrência e destinos inválidos retornam 409
(ou 404 se o espaço/pai não existir). A listagem não carrega os corpos históricos do banco.
