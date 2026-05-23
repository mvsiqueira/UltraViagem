# Schema do trip.json

Cada viagem é armazenada em uma pasta dentro do repositório:

```
<repo>/
  <yyyy-mm-nome-da-viagem>/
    trip.json          ← dados da viagem (este documento)
    *.pdf, *.png ...   ← arquivos anexados (referenciados em attachments)
    .cache/            ← imagens e previews gerados pelo app (ignorar no controle de versão)
```

O arquivo é serializado em **camelCase**, indentado, com campos nulos omitidos (`DefaultIgnoreCondition.WhenWritingNull`).

---

## Raiz — `Trip`

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `schemaVersion` | `int` | `1` | Versão do schema. Reservado para migrações futuras. |
| `id` | `string` | `""` | Identificador da viagem — igual ao nome da pasta (ex.: `2024-11-patagonia`). |
| `title` | `string` | `""` | Nome exibido da viagem. |
| `startDate` | `string\|null` | `null` | Data de início no formato `yyyy-MM-dd`. |
| `endDate` | `string\|null` | `null` | Data de término no formato `yyyy-MM-dd`. |
| `baseCurrency` | `string` | `"BRL"` | Código ISO 4217 da moeda base para conversão de gastos. |
| `people` | `int` | `1` | Número de viajantes (usado em cálculos de orçamento). |
| `rateDecimalDigits` | `int` | `2` | Casas decimais exibidas nas cotações de câmbio. |
| `myMapsUrl` | `string\|null` | `null` | URL pública do Google My Maps (link de edição ou visualização). |
| `itinerarySlotsPerDay` | `int` | `16` | Número de slots de tempo por dia no canvas do roteiro. |
| `itineraryBlockHeight` | `int` | `44` | Altura em pixels de cada slot no canvas. |
| `itineraryFontSize` | `int` | `11` | Tamanho da fonte nos blocos do roteiro. |
| `showItineraryGrid` | `bool` | `false` | Exibe linhas verticais divisórias de slot no canvas. |
| `activeVersionId` | `string` | `""` | `id` da versão de roteiro atualmente ativa. |
| `itineraryVersions` | `ItineraryVersion[]` | `[]` | Versões do roteiro (mínimo 1 após criação). |
| `tasks` | `TaskItem[]` | `[]` | Lista de tarefas da viagem. |
| `links` | `LinkItem[]` | `[]` | Dicas / links úteis. |
| `expenses` | `ExpenseItem[]` | `[]` | Itens de orçamento. |
| `currencyRates` | `CurrencyRateItem[]` | `[]` | Cotações de moedas em relação à `baseCurrency`. |
| `attachments` | `AttachmentItem[]` | `[]` | Arquivos anexados (devem existir fisicamente na pasta). |

---

## `ItineraryVersion`

Representa uma versão do roteiro. Uma viagem pode ter múltiplas versões (ex.: plano A e plano B).

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `id` | `string` | `""` | Identificador único gerado como `v-{guid}`. |
| `name` | `string` | `"Versão 1"` | Nome exibido na aba de versão. |
| `itinerary` | `ItineraryDay[]` | `[]` | Dias do roteiro nesta versão. |
| `bankRows` | `int` | `2` | Número de linhas no banco de atividades. |
| `bankActivities` | `ItineraryActivity[]` | `[]` | Atividades no banco (não alocadas a nenhum dia). |

---

## `ItineraryDay`

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `id` | `string` | `""` | Identificador único gerado como `dia-{guid}`. |
| `date` | `string\|null` | `null` | Data do dia no formato `yyyy-MM-dd`. |
| `title` | `string` | `""` | Título do dia (ex.: `"Dia 1"`). |
| `summary` | `string` | `""` | Resumo livre do dia — usado como base para buscar imagem de capa na visão geral. |
| `activities` | `ItineraryActivity[]` | `[]` | Atividades alocadas neste dia. |

---

## `ItineraryActivity`

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `id` | `string` | `""` | Identificador único. |
| `title` | `string` | `""` | Nome da atividade. |
| `type` | `string` | `"Atividade"` | Categoria: `"Atividade"`, `"Refeição"`, `"Pernoite"`, `"Transporte"` ou qualquer texto livre. |
| `color` | `string` | `"#DBEAFE"` | Cor do bloco no canvas (hex). |
| `icon` | `string` | `""` | Emoji ou símbolo exibido no bloco e na visão geral. |
| `startSlot` | `int` | `0` | Slot de início (0 = começo do dia). |
| `durationSlots` | `int` | `2` | Duração em slots. |
| `bankRow` | `int` | `0` | Linha no banco de atividades (ignorado quando alocado em um dia). |
| `details` | `string\|null` | `null` | Descrição detalhada. |
| `additionalData` | `string\|null` | `null` | Campo livre para dados extras. |

---

## `TaskItem`

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `id` | `string` | `""` | Identificador único gerado como `tarefa-{timestamp}`. |
| `title` | `string` | `""` | Descrição da tarefa. |
| `status` | `string` | `"pending"` | `"pending"` ou `"done"`. |
| `notes` | `string\|null` | `null` | Notas adicionais. |

---

## `LinkItem` (Dicas)

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `id` | `string` | `""` | Identificador único. |
| `title` | `string` | `""` | Nome/label do link. |
| `url` | `string` | `""` | URL ou texto livre. |

---

## `ExpenseItem`

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `id` | `string` | `""` | Identificador único. |
| `isActive` | `bool` | `true` | `false` = item cancelado/inativo (excluído do total). |
| `title` | `string` | `""` | Nome do gasto. |
| `type` | `string\|null` | `null` | Categoria (ex.: `"Hospedagem"`, `"Transporte"`). |
| `company` | `string\|null` | `null` | Fornecedor/empresa. |
| `link` | `string\|null` | `null` | URL de referência (reserva, comprovante etc.). |
| `notes` | `string\|null` | `null` | Observações. |
| `price` | `decimal` | `0` | Preço unitário na moeda do item. |
| `taxes` | `decimal` | `0` | Taxas/encargos por unidade. |
| `people` | `int` | `1` | Número de pessoas (multiplicador). |
| `quantity` | `int` | `1` | Quantidade (multiplicador). |
| `currency` | `string` | `"BRL"` | Código ISO 4217 da moeda do item. |
| `exchangeRateToBase` | `decimal` | `1` | Taxa de câmbio para `baseCurrency`. Atualizada via AwesomeAPI. |
| `useFixedRate` | `bool` | `false` | Se `true`, a taxa não é atualizada automaticamente. |
| `paidAmount` | `decimal` | `0` | Valor já pago na moeda base. |

> **Fórmulas calculadas (não persistidas):**
> `subtotal = (price + taxes) × people × quantity`
> `subtotalBase = isActive ? subtotal × exchangeRateToBase : 0`

---

## `CurrencyRateItem`

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `currency` | `string` | `"BRL"` | Código ISO 4217. |
| `symbol` | `string` | `""` | Símbolo exibido (ex.: `"$"`, `"€"`). |
| `name` | `string` | `""` | Nome completo (ex.: `"Dólar Americano"`). |
| `decimalDigits` | `int` | `2` | Casas decimais da moeda. |
| `rateToBase` | `decimal` | `1` | Quantas unidades de `baseCurrency` equivalem a 1 unidade desta moeda. |
| `updatedAt` | `string\|null` | `null` | ISO 8601 da última atualização via AwesomeAPI. |

---

## `AttachmentItem`

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `id` | `string` | `""` | Identificador único gerado como `arquivo-{timestamp}`. |
| `file` | `string` | `""` | Nome do arquivo físico na pasta da viagem (apenas o nome, sem caminho). |

---

## Exemplo completo

```json
{
  "schemaVersion": 1,
  "id": "2024-11-patagonia",
  "title": "Patagônia 2024",
  "startDate": "2024-11-20",
  "endDate": "2024-11-30",
  "baseCurrency": "BRL",
  "people": 2,
  "rateDecimalDigits": 2,
  "myMapsUrl": "https://www.google.com/maps/d/edit?mid=1abc123",
  "itinerarySlotsPerDay": 16,
  "itineraryBlockHeight": 44,
  "itineraryFontSize": 11,
  "showItineraryGrid": false,
  "activeVersionId": "v-a1b2c3d4",
  "itineraryVersions": [
    {
      "id": "v-a1b2c3d4",
      "name": "Versão 1",
      "bankRows": 2,
      "bankActivities": [],
      "itinerary": [
        {
          "id": "dia-001",
          "date": "2024-11-20",
          "title": "Dia 1",
          "summary": "El Chaltén",
          "activities": [
            {
              "id": "ativ-001",
              "title": "Rio x Balmaceda",
              "type": "Transporte",
              "color": "#DBEAFE",
              "icon": "✈️",
              "startSlot": 0,
              "durationSlots": 4,
              "bankRow": 0
            },
            {
              "id": "ativ-002",
              "title": "Hotel Los Cerros",
              "type": "Pernoite",
              "color": "#F3E8FF",
              "icon": "🛏",
              "startSlot": 12,
              "durationSlots": 4,
              "bankRow": 0
            }
          ]
        }
      ]
    }
  ],
  "tasks": [
    {
      "id": "tarefa-20241001120000000",
      "title": "Comprar seguro viagem",
      "status": "done"
    },
    {
      "id": "tarefa-20241001120001000",
      "title": "Confirmar reserva do hotel",
      "status": "pending",
      "notes": "Ligar para o hotel 48h antes"
    }
  ],
  "links": [
    {
      "id": "link-001",
      "title": "Trilhas de El Chaltén",
      "url": "https://elchalten.com/trilhas"
    }
  ],
  "expenses": [
    {
      "id": "gasto-001",
      "isActive": true,
      "title": "Passagem Rio → Balmaceda",
      "type": "Transporte",
      "company": "LATAM",
      "price": 1800.00,
      "taxes": 0,
      "people": 2,
      "quantity": 1,
      "currency": "BRL",
      "exchangeRateToBase": 1,
      "useFixedRate": false,
      "paidAmount": 3600.00
    }
  ],
  "currencyRates": [
    {
      "currency": "USD",
      "symbol": "$",
      "name": "Dólar Americano",
      "decimalDigits": 2,
      "rateToBase": 4.97,
      "updatedAt": "2024-10-15T10:30:00Z"
    }
  ],
  "attachments": [
    {
      "id": "arquivo-20241001130000000",
      "file": "passagem-latam.pdf"
    }
  ]
}
```
