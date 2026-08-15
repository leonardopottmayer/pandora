# Notes — Roadmap de implementação

Quebra do [product-plan.md](../product-plan.md) (§7) em fases sequenciais, uma por
sessão de trabalho. Cada arquivo é autocontido: objetivo, pré-requisitos, escopo,
passos e **critérios de verificação**. Implemente em ordem — cada fase assume que as
anteriores estão prontas e verdes.

Referência de arquitetura: espelha o módulo Finances em
`backend/src/Modules/Finances/Pottmayer.Pandora.Modules.Finances.*` (7 projetos).

| Fase | Título | Bloco no plano | Depende de |
|---|---|---|---|
| [00](phase-00-scaffold-modulo.md) | Fundação — scaffold do módulo `Notes` + registro no Host | MVP §7.0.1 | — |
| [01](phase-01-page-aggregate-crud.md) | `Page` aggregate + migration + CRUD | MVP §7.0.2 | 00 |
| [02](phase-02-anexos-storage.md) | Anexos — `IFileStorage` + `DatabaseFileStorage` + upload/download | MVP §7.1.3 | 01 |
| [03](phase-03-frontend-editor.md) | Frontend — sidebar em árvore + editor CodeMirror + autosave | MVP §7.1.4 | 01, 02 |
| [04](phase-04-wikilinks-backlinks.md) | Wikilinks + `PageLink` + backlinks | MVP §7.1.5 | 01, 03 |
| [05](phase-05-busca-fulltext.md) | Busca full-text + Ctrl+K | MVP §7.1.6 | 01, 03 |
| [06](phase-06-graph-view.md) | Graph view (global + local) | v2 §7.2.7 | 04 |
| [07](phase-07-blocos-ricos.md) | Slash commands, callouts, tabelas | v2 §7.2.8 | 03 |
| [08](phase-08-tags.md) | Tags (`#tag` no markdown) + filtros | MVP §3 | 04, 05, 06 |

**MVP (v1)** = fases 00–05. **v2** = fases 06–07. A Fase 08 fecha as tags, que o §3 marcava
como MVP mas a Fase 01 adiou — e sem as quais o filtro por tag do grafo (Fase 06) não tinha
o que filtrar.

## Convenções por fase

- **Backend**: novo módulo `Pottmayer.Pandora.Modules.Notes.*`; migrations em
  `migrations/migrations/notes`.
- **Frontend**: `client-web/src/modules/notes`; i18n EN + PT-BR.
- Cada fase termina com build + testes verdes e o critério de *verify* atendido antes
  de abrir a próxima.
