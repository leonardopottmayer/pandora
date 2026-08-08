# Fase 00 — Fundação: scaffold do módulo `Notes` + registro no Host

> Bloco no plano: MVP §7.0.1 · Depende de: — · Camada: backend

## Objetivo

Criar o esqueleto do módulo `Notes` (7 projetos, espelhando Finances) e registrá-lo no
Host, sem nenhum domínio ainda. Ao final, a app sobe com o módulo carregado e o health
continua ok.

## Escopo

- 7 projetos em `backend/src/Modules/Notes/Pottmayer.Pandora.Modules.Notes.*`:
  - `Abstractions`, `Contracts`, `Domain`, `Application`, `Infrastructure`,
    `Persistence`, `Presentation`.
- Projeto de testes `backend/tests/Pottmayer.Pandora.Modules.Notes.Tests`.
- Referências entre projetos iguais às do Finances.
- Módulo adicionado à solution e registrado no Host (DI + rotas), seguindo o mesmo
  ponto de extensão que Finances usa (`AddNotesModule` / `MapNotesModule` ou equivalente).

## Passos

1. Espelhar a estrutura de `Modules/Finances` — copiar a anatomia dos `.csproj` e
   referências, trocando `Finances` por `Notes`. Remover todo conteúdo específico de
   domínio; deixar apenas o extension point de registro vazio.
2. Adicionar os 8 projetos à solution.
3. Registrar o módulo no Host (mesma mecânica de composição usada pelos outros módulos).

## Verify

- `dotnet build` da solution passa.
- App sobe e o endpoint de health responde ok com o módulo `Notes` registrado.
- Nenhum endpoint novo funcional ainda (só o esqueleto) — isso é esperado.

## Fora de escopo

- Qualquer entidade, migration, endpoint de negócio (vem na Fase 01).
