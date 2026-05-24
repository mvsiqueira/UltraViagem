# Guia de Importação de Planilhas

Como as planilhas de viagem não seguem um padrão fixo, a importação é feita
manualmente via Claude. Este documento registra os dados e regras utilizados.

## Fluxo de Importação

1. Informar ao Claude o caminho da planilha e da pasta de arquivos.
2. Claude lê a planilha, mapeia os dados e cria o `trip.json`.
3. Claude copia a pasta inteira (incluindo todos os arquivos) para o repositório.

## Regras de Mapeamento

### Nome da Viagem (`trip.title`)
- Usar o **nome da pasta** como título, não o nome do arquivo xlsx.
- Exemplo: pasta `2026-04 Chapada dos Veadeiros` → título `"2026-04 Chapada dos Veadeiros"`.

### Repositório de Destino
- Caminho padrão: `G:\Meu Drive\UltraViagens\`
- Criar subpasta com o mesmo nome da pasta de origem.
- Copiar **todos os arquivos** da pasta de origem (PDFs, XLSXs, etc.) junto com o `trip.json`.

### Datas e Pessoas
- `startDate` / `endDate`: primeiro e último dia do roteiro.
- `people`: inferido dos gastos (coluna Pessoas dos itens de passagem/ingresso).
- `baseCurrency`: `"BRL"` por padrão, salvo indicação contrária.

### Roteiro (`itineraryVersions`)

#### Slots por dia (`itinerarySlotsPerDay`)
- Contar o número de **colunas de atividade** na planilha (excluindo metadata: data, dia, título).
- Incluir as colunas de noite e pernoite na contagem.
- Exemplo: planilha com cols E–M (manhã E–H, tarde I–K, noite L, pernoite M) → 9 colunas → `itinerarySlotsPerDay: 9`.

#### Duração das atividades (`durationSlots`)
- Usar `openpyxl` para ler as **merged cell ranges** da aba de roteiro.
- `durationSlots` = número de colunas que a célula mescla (max_col - min_col + 1).
- Atividades sem merge = `durationSlots: 1`.
- `startSlot` = posição da coluna de início menos a coluna da primeira atividade (base 0).
- Exemplo: Complexo de Couros ocupa E4:I4 (5 colunas, começa em E=slot 0) → `startSlot: 0, durationSlots: 5`.

#### Título e Resumo do dia
- `title` ← coluna **"dia"** da planilha (ex: `"Dia 1"`, `"Dia 2"`).
- `summary` ← coluna **"título"** da planilha (ex: `"IDA"`, `"Complexo de Couros"`).

#### Atividades
Mapear cada célula preenchida para uma `ItineraryActivity`, distribuindo os slots
de forma que não haja sobreposição:

| Posição na planilha | Slot sugerido (base 8) | Tipo padrão |
|---|---|---|
| Manhã 1–3 | 0–2 | Trilha / Passeio / Transporte |
| Almoço / meio-dia | 3 | Refeição |
| Tarde 1–2 | 4–5 | Passeio / Transporte |
| Noite / jantar | 6 | Refeição |
| Pernoite | 7 | Hospedagem |

#### Bloco de Pernoite
- Sempre criar um bloco no **último slot** com o nome do hotel/pousada e cidade.
- Formato do título: `"Cidade — Nome da Pousada"` (ex: `"Cavalcante — Vila Nômade"`).
- Cor: extrair da célula da coluna pernoite (M) via openpyxl.
- Se não houver pousada identificada, usar apenas a cidade.
- Dia de retorno (sem pernoite): omitir o bloco.

#### Cores das atividades
Usar as **cores reais das células** da planilha, extraídas via `openpyxl` (`cell.fill.fgColor`).
Cores de tema OOXML devem ser convertidas para hex usando HLS com a fórmula:
- tint > 0: `L_new = L * (1 - tint) + tint`
- tint < 0: `L_new = L * (1 + tint)`

As planilhas costumam usar **codificação geográfica** (cor por região/destino), não por tipo:

| Cor da célula | Significado típico |
|---|---|
| `#000000` (preto) | Transporte, refeições, atividades genéricas |
| Cor clara (tint ~0.80) | Atividades principais da região |
| Cor média (tint ~0.40) | Bloco de pernoite da região |

Exemplo — Chapada dos Veadeiros:
| Região | Atividades | Pernoite |
|---|---|---|
| Alto Paraíso | `#EBF1DE` (verde claro) | `#C3D69B` (verde médio) |
| Cavalcante | `#DBEEF4` (azul claro) | `#93CDDD` (azul médio) |
| São Jorge | `#F2DCDB` (salmão claro) | `#D99694` (salmão médio) |

> **Nota:** Se a planilha usar cores por tipo de atividade em vez de por região, mapear
> conforme a lógica encontrada. Inspecionar os fills com openpyxl antes de importar.

### Dicas (`links`)
- Cada linha da aba Dicas → um `LinkItem` (`title` + `url`).
- Remover duplicatas.
- Usar o conteúdo da primeira coluna como título (ex: `"Roteiro"`, `"Mapa"`).
  Se o título for genérico (ex: múltiplos "Roteiro"), complementar com o domínio
  do link (ex: `"Roteiro — Em Algum Lugar do Mundo"`).

### Gastos (`expenses`)
Mapeamento coluna a coluna da aba Gastos:

| Coluna planilha | Campo `ExpenseItem` |
|---|---|
| `►` (marcador) | `isActive` (true se presente) |
| Nome | `title` |
| Tipo | `type` |
| Empresa / fornecedor | `company` |
| Link | `link` |
| Obs + Detalhe | `notes` (concatenar se ambos preenchidos) |
| Preço | `price` |
| Taxas | `taxes` |
| Pessoas | `people` |
| Qtd | `quantity` |
| Moeda (fator) | `currency` + `exchangeRateToBase` (ver tabela abaixo) |
| Pago | `paidAmount` |

#### Conversão de moeda
| Valor na coluna Moeda | `currency` | `exchangeRateToBase` |
|---|---|---|
| `1` ou vazio | `"BRL"` | `1.0` |
| `~5.90` (USD) | `"USD"` | valor exato da célula |
| `~0.006` (CLP) | `"CLP"` | valor exato da célula |

#### Validação
Confirmar que `(price + taxes) × people × quantity` = subtotal da planilha para
cada linha. Se não bater, investigar antes de salvar.

### Anexos (`attachments`)
- Registrar **todos os arquivos** da pasta de origem (PDFs, XLSXs, imagens, etc.).
- Usar o nome de arquivo exatamente como está no sistema operacional.
- Os arquivos ficam na mesma pasta que o `trip.json` — o app os encontra pelo nome.
