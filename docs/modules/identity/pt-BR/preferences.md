# Preferências

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md)

---

As preferências por usuário (`idt003`, uma linha por usuário) guardam escolhas de UI **e** os padrões
de agendamento que outros módulos leem.

| Campo | Valores | Consumido por |
|---|---|---|
| `theme` | `light` \| `dark` \| `system` | O cliente web. |
| `language` | `pt-BR` \| `en` | O cliente web; o locale carregado nas notificações. |
| `time_zone` | IANA (padrão `America/Sao_Paulo`) | O fuso **padrão a nível de usuário**. |
| `week_starts_on` | `sunday`…`saturday` | Renderização da semana no calendário. |
| `default_alert_offset_minutes` | int com sinal (padrão `-15`) | [Agenda](../../agenda/pt-BR/overview.md) como offset de alerta padrão de novos itens. |

## API

- `GET /identity/preferences` — leitura (`GetPreferences`).
- `PUT /identity/preferences` — upsert (`UpsertPreferences`). Valida tema e idioma contra os conjuntos
  suportados e o fuso com `TimeZoneInfo.TryFindSystemTimeZoneById`.

## Nota entre módulos

O Identity **carrega** o fuso IANA, o início da semana e o offset de alerta padrão do usuário — o trio
que o plano da Agenda chamava de pré-requisito de "fase 0". Duas peças consumidoras restam em outros
módulos:

- **Agenda** guarda um `time_zone` em cada item porque a recorrência precisa expandir no fuso do
  *próprio item* (que pode diferir do padrão do usuário); ligar a preferência do Identity como o padrão
  de novos itens é um follow-up pequeno.
- As **quiet hours** do Channels ainda não foram construídas; precisam deste fuso, que agora está
  disponível — então estão desbloqueadas, não bloqueadas. Ver
  [product-plan do Channels](../../channels/pt-BR/product-plan.md).
