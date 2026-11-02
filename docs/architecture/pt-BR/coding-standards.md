# Padrões de projeto e código

[Índice da documentação](../../README.md) · [English](../en/coding-standards.md)

Consulte antes de implementar ou revisar alterações. Leia um caso de uso equivalente no módulo
afetado antes de escrever código; siga suas convenções em vez de introduzir uma estrutura paralela.
Estas são as regras gerais; as regras específicas de cada módulo ficam na documentação dele.

## Backend: responsabilidades e arquivos

| Local | Responsabilidade |
|---|---|
| `Presentation/Controllers` | Binding HTTP, usuário autenticado, envio ao mediator e mapeamento da resposta |
| `Presentation/Requests` | Contratos de entrada HTTP; não declarar dentro da controller |
| `Presentation/Filters` | Filtros HTTP; não colocar em `Controllers` |
| `Application/Commands/<UseCase>` | Escrita: `<UseCase>Command.cs` e `<UseCase>CommandHandler.cs` |
| `Application/Queries/<UseCase>` | Leitura: `<UseCase>Query.cs` e `<UseCase>QueryHandler.cs` |
| `Application/Dtos`, `Application/Services` | DTOs de resposta e auxiliares de orquestração da aplicação |
| `Domain/Aggregates`, `Domain/ValueObjects`, `Domain/Errors` | Estado e invariantes, tipos de valor e erros de domínio |
| `Domain/Ports`, `Persistence`, `Infrastructure` | Interfaces, implementações de banco e adaptadores externos |

- Cada novo caso de uso tem sua pasta, inclusive queries distintas de listagem/detalhe. O handler
  fica em arquivo separado. O record `Input` pode compartilhar o arquivo do comando/query, como nos existentes.
- DTOs e tipos de domínio independentes ficam em arquivos próprios e nas pastas correspondentes.
  Não juntar records sem relação no arquivo de um aggregate. Projeções de repositório podem ficar
  junto ao port quando esse for o padrão do módulo.
- Controllers não contêm regras de negócio, repositórios ou EF. Usar o fluxo existente de
  `CommandBase`/`QueryBase`, `Result`, erros de domínio e `ToActionResult`.
- Manter setters privados nos aggregates e criação por factories. Reutilizar os valores/constantes
  de domínio do módulo para estados e origens, sem repetir strings entre camadas.
- Usar DI para serviços e interceptors. Interceptors de banco ficam em `Persistence/Interceptors`;
  registrar pela configuração de DI do módulo, seguindo os lifetimes existentes.
- Usar `TimeProvider`; armazenar instantes em UTC e interpretar datas do usuário no fuso da conta.
  Manter datas puras como `DateOnly` quando o domínio exigir.

## Persistência e limites

- Usar repositórios pela unidade de trabalho do módulo. Gravar alterações relacionadas atomicamente,
  incluindo auditoria/histórico. Filtrar dados pelo dono; recursos de outro usuário seguem a regra de 404 do módulo.
- Seguir os mappings EF e nomes SQL existentes: colunas em snake_case, prefixos de tabela do módulo,
  constraints/índices nomeados e convenções de enum em string (por exemplo `pending-confirmation`).
- Pré-1.0: alterar o schema existente no `CREATE TABLE` original; tabelas novas recebem scripts próprios
  de criação/down. Não criar migrations incrementais de `ALTER` para tabelas existentes. Recriar um
  banco local com dados é uma operação destrutiva separada, não uma etapa automática de implementação.
- Reutilizar a integração com Tars e os limites dos módulos documentados em
  [Tars wiring](tars-wiring.md) e [Mensageria](messaging.md).

## Frontend e legibilidade

- Telas de rota em `pages`, UI em `components`, hooks de Query em `hooks`, HTTP em `services`, lógica
  pura em `lib` e espelhos dos DTOs em `models.ts`. Adicionar query keys à fábrica central do módulo
  e invalidar explicitamente os caches afetados.
- Seguir [Pandora's look](../../../client-web/DESIGN.md). Usar tokens do tema e traduções en/pt-BR;
  consultar a API da versão instalada do Ant Design antes de usar ou alterar componentes.
- Seguir imports, nomes e formatação do entorno. Usar props e auxiliares descritivos; evitar
  instruções comprimidas e ternários aninhados que escondam os caminhos de loading, erro e sucesso.
- Identificadores de código, caminhos, tabelas e rotas em inglês. Métodos C# assíncronos têm sufixo
  `Async`, seguindo o código existente, inclusive nos auxiliares de teste.

## Validação e documentação

- Criar testes de regressão para bugs de comportamento e testes relevantes para lógica não trivial.
  Rodar os testes .NET afetados; no frontend, Vitest pertinente, checagens de tipos/build e ESLint.
- Revisar o diff contra estas regras; quantidade de testes não substitui verificar limites e localização
  dos arquivos. Informar o que foi realmente validado e as limitações restantes.
- Atualizar as docs do módulo afetado: inglês é o principal, com os mesmos tópicos espelhados em pt-BR.
  Usar o [índice da documentação](../../README.md); handoffs registram contexto de sessão e podem estar desatualizados.
- Manter alterações cirúrgicas. Não imprimir nem commitar segredos locais como `appsettings.json`
  ou `seed.local.sql`. As regras de commit/push continuam nas instruções dos agentes na raiz.
