# Guia de Importação de Planilhas

Como as planilhas de viagem não seguem um padrão fixo, a importação é feita
manualmente via Claude. Este documento registra os dados e regras utilizados.

## Repositório de Viagens

- Caminho padrão: `G:\Meu Drive\Viagens\`
- As pastas de viagem já estão no repositório com todos os arquivos no lugar certo.
- Claude **não copia arquivos** — apenas cria ou atualiza o `trip.json` dentro da pasta.

## Modos de Importação

### Importação simples (`"importar"`)
Cria o `trip.json` apenas com os dados básicos da viagem e a lista de anexos.
Útil para registrar rapidamente uma viagem sem mapear todos os detalhes.

Campos preenchidos:
- `schemaVersion`, `id`, `title`, `startDate`, `endDate`, `people`, `baseCurrency`
- `attachments`: todos os arquivos encontrados na pasta
- Estrutura mínima do roteiro: `itineraryVersions` com versão vazia, `itinerarySlotsPerDay` padrão

Campos **não** preenchidos (ficam vazios): itinerário, gastos, dicas, mapa.

### Importação completa (`"importação completa"`)
Importa todos os dados da planilha: roteiro com atividades e banco, gastos, dicas, mapa.
Seguir todas as regras de mapeamento detalhadas nas seções abaixo.

## Fluxo de Importação

1. Informar ao Claude o caminho da pasta (e da planilha, se for importação completa).
2. Claude lista os arquivos da pasta para montar os `attachments`.
3. Claude lê a planilha com `openpyxl` (se importação completa) e cria/atualiza o `trip.json`.

## Regras de Mapeamento

### Nome da Viagem (`trip.title`)
- Usar o **nome da pasta** como título, não o nome do arquivo xlsx.
- Exemplo: pasta `2026-04 Chapada dos Veadeiros` → título `"2026-04 Chapada dos Veadeiros"`.

### Dados básicos
- `id`: **igual ao nome da pasta** (o app usa `trip.Id` para montar o caminho dos arquivos).
- `title`: nome da pasta.
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
- `durationSlots` = número de colunas que a célula mescla (`max_col - min_col + 1`).
- Atividades sem merge = `durationSlots: 1`.
- `startSlot` = posição da coluna de início menos a coluna da primeira atividade (base 0).
- Exemplo: Complexo de Couros ocupa E4:I4 (5 colunas, começa em E=slot 0) → `startSlot: 0, durationSlots: 5`.

#### Título e Resumo do dia
- `title` ← coluna **"dia"** da planilha (ex: `"Dia 1"`, `"Dia 2"`).
- `summary` ← coluna **"título"** da planilha (ex: `"IDA"`, `"Complexo de Couros"`).

#### Atividades
Mapear cada célula preenchida para uma `ItineraryActivity`. O slot de cada atividade
é determinado pela sua coluna na planilha (base 0 a partir da primeira coluna de atividade).

Referência para a planilha Chapada dos Veadeiros (cols E–M, 9 slots):

| Coluna | Slot | Período típico |
|---|---|---|
| E | 0 | Manhã 1 |
| F | 1 | Manhã 2 |
| G | 2 | Manhã 3 |
| H | 3 | Manhã 4 / Almoço |
| I | 4 | Tarde 1 |
| J | 5 | Tarde 2 |
| K | 6 | Tarde 3 |
| L | 7 | Noite / Jantar |
| M | 8 | Pernoite |

> **Nota:** o número e a posição das colunas variam por planilha. Sempre inspecionar
> a estrutura real antes de mapear os slots.

#### Bloco de Noite
- Coluna L (slot 7, penúltimo): nome do restaurante/bar ou "iFood" — tipo Refeição ou Hospedagem.
- Cor: extrair da célula via openpyxl.

#### Bloco de Pernoite
- Coluna M (slot 8, último): nome da cidade e/ou pousada — tipo Hospedagem.
- Formato do título: somente a **cidade** (ex: `"Alto Paraíso"`, `"Cavalcante"`).
  O nome da pousada fica no bloco de noite ou nos gastos, não no pernoite.
- Cor: extrair da célula da coluna pernoite via openpyxl.
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

> **Nota:** se a planilha usar cores por tipo de atividade em vez de por região, mapear
> conforme a lógica encontrada. Inspecionar os fills com openpyxl antes de importar.

### Banco de Atividades (`bankActivities` / `bankRows`)

O banco contém atividades alternativas que não entraram no roteiro principal. Na
planilha ficam em linhas **abaixo** das linhas do roteiro, separadas por uma linha
em branco ou divisória.

#### Como identificar o banco
- Localizar as linhas após o último dia do roteiro que contenham células coloridas
  com atividades (mesma estrutura visual das linhas do roteiro).
- Ignorar linhas em branco ou de divisória.

#### Mapeamento
- `bankRows` = número de linhas de atividades no banco (atualizar no `itineraryVersion`).
- `bankRow` = índice da linha no banco, base 0 (primeira linha do banco = 0).
- `startSlot` e `durationSlots`: mesmas regras das atividades do roteiro (merged cells).
- `color`: extrair da célula via openpyxl, mesma lógica das atividades do roteiro.
- `type`: inferir pelo conteúdo e cor (Trilha, Passeio, Refeição, etc.).

Exemplo — Chapada dos Veadeiros (5 linhas de banco):

| bankRow | Atividades |
|---|---|
| 0 | Fazenda Loquinhas (slot 0, dur 4) · Complexo Cachoeira dos Cristais (slot 4, dur 4) |
| 1 | Cachoeira do Label (slot 0, dur 4) |
| 2 | PN Chapada — Trilha dos Cânions (slot 0, dur 6) |
| 3 | Cachoeira do Cordovil (slot 0, dur 3) · Trilha do Mirante da Janela (slot 3, dur 3) · Morada do Sol (slot 6, dur 2) |
| 4 | Complexo do Prata (slot 0, dur 3) · Cachoeira Candaru (slot 3, dur 2) · Trilha aquática (slot 5, dur 2) |

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
