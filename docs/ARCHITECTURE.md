# Arquitetura

## Princípios

- Local-first: o app funciona sobre arquivos locais.
- Portável: a pasta de dados pode ser sincronizada por Google Drive, OneDrive ou similar.
- Simples de inspecionar: dados em JSON legível e anexos no sistema de arquivos.
- Evolutivo: Windows primeiro, com caminho futuro para Android e web.
- Baixo acoplamento: domínio/persistência ficam em `UltraViagem.Core`; WPF fica em `UltraViagem.App`.

## Visão Em Camadas

```text
UltraViagem.App
  WPF, telas, estilos, comandos, ViewModels

UltraViagem.Core
  modelos de domínio, AppConfig, TripRepository

Sistema de arquivos
  config.json, pastas de viagem, trip.json, anexos
```

## Persistência

`TripRepository` é o ponto central para:

- carregar ou criar `config.json`;
- listar viagens procurando subpastas com `trip.json`;
- carregar uma viagem;
- salvar uma viagem;
- copiar uma viagem (`CopyTripFolder`): duplica o conteúdo integral da pasta de origem para uma nova pasta de destino.

O formato de dados atual fica em:

```text
<raiz-das-viagens>/config.json
<raiz-das-viagens>/<id-da-viagem>/trip.json
```

## Modelo De Dados

`Trip` contém:

- dados gerais: `id`, `title`, datas, `baseCurrency` (moeda base), `people`, `rateDecimalDigits` (casas decimais para exibição de taxas na aba Moedas) e `itinerarySlotsPerDay` (blocos por dia no roteiro, padrão 16);
- `myMapsUrl`: URL pública do Google My Maps exibida pela tela de mapa;
- `itinerary`: roteiro por dia (lista de `ItineraryDay`);
- `tasks`: tarefas;
- `places`: lugares;
- `links`: dicas;
- `expenses`: gastos, com flag de ativação, categoria, moeda, `exchangeRateToBase`, `useFixedRate` (cotação fixa ou automática) e valor pago;
- `currencyRates`: moedas com código, nome, símbolo, casas decimais de exibição e taxa para a moeda base;
- `attachments`: arquivos anexos.

`ItineraryDay` contém `id`, `title`, `date` (string `yyyy-MM-dd` ou vazia), `overnight` (bool) e `activities` (lista de `ItineraryActivity`).

`ItineraryActivity` contém:

```json
{
  "id": "act-...",
  "title": "Vôo GRU→SCL",
  "typeId": "flight",
  "startSlot": 2,
  "durationSlots": 4,
  "description": null,
  "distance": null
}
```

`useFixedRate` em `ExpenseItem`: quando `false` (padrão), a taxa do gasto é sincronizada automaticamente com a aba Moedas ao carregar a viagem, ao trocar a moeda do gasto ou ao atualizar cotações; quando `true`, a taxa é fixa e ignorada nas atualizações automáticas.

`LinkItem` é usado para o quadro de dicas:

```json
{
  "id": "dica-...",
  "title": "Guia Carretera Austral",
  "url": "https://..."
}
```

`AppConfig` inclui:

- `itinerarySlotWidth` (int, padrão 44): largura em pixels de cada slot no canvas do roteiro; salvo no `config.json` do repositório e restaurado ao abrir;
- `activityTypes` (lista de `ActivityType`): cadastro global de tipos de atividade com `id`, `name`, `color` e `textColor` (hexadecimais). Os 10 tipos padrão são criados automaticamente se a lista estiver vazia.

## Navegação Atual

A janela principal alterna painéis:

- `OverviewPanel`: visão geral.
- `TripDetailsPanel`: edição dos dados gerais da viagem.
- `TasksPanel`: edição de tarefas.
- `TipsPanel`: edição de dicas.
- `MapPanel`: visualização do Google My Maps.
- `FilesPanel`: arquivos anexos.
- `ItineraryPanel`: roteiro visual por dia.

Os botões ativos do menu lateral são estilizados no code-behind por `SetActiveNav`.

## Salvamento

Tarefas, dicas, arquivos e roteiro possuem salvamento automático via eventos do ViewModel:

- `TasksChanged` chama `SaveTasksInternal`.
- `TipsChanged` chama `SaveTipsInternal`.
- `AttachmentsChanged` chama `SaveAttachmentsInternal`.
- `ItineraryChanged` chama `SaveItineraryInternal`.

O link do mapa é salvo a partir da tela `Mapa`, diretamente em `Trip.MyMapsUrl`.
O mapa é exibido com `Microsoft.Web.WebView2`, usando o formato público embutido do Google My Maps (`/maps/d/embed?mid=...`).

Arquivos anexados são copiados para a pasta da viagem. A lista de anexos salva apenas o nome do arquivo relativo à pasta da viagem.
Na UI WPF, `AttachmentIconConverter` consulta o Shell do Windows para exibir o ícone associado à extensão configurada no sistema operacional.
`StringToBrushConverter` converte strings hexadecimais (ex.: `"#DBEAFE"`) em `SolidColorBrush` para uso no binding de cores das atividades do roteiro.

Também existem botões explícitos de salvar para cada tela.

## ViewModels do Roteiro

`ItineraryDayViewModel` e `ItineraryActivityViewModel` herdam de `NotifyObject` e usam um padrão de configuração estática (`Configure(...)`) para receber `slotsPerDay`, `slotWidth` e `activityTypes` — que são comuns a todas as instâncias de uma viagem e atualizados ao carregar ou ao alterar o zoom.

- **`ItineraryDayViewModel`**: gerencia a lista `Activities` (`ObservableCollection<ItineraryActivityViewModel>`) e calcula propriedades de layout do canvas (`CanvasWidth`, posições e larguras das bandas de período).
- **`ItineraryActivityViewModel`**: calcula `CanvasLeft = StartSlot * slotWidth` e `BlockWidth = DurationSlots * slotWidth - 2`; resolve `TypeColor` e `TypeTextColor` via lookup no cadastro global de tipos.

O drag e resize são implementados no code-behind (`ActivityBlock_MouseDown/Move/Up`) com captura de mouse via `CaptureMouse()`. A detecção de resize ocorre quando `pos.X >= grid.ActualWidth - 10` no `MouseDown`.

## Caminho Futuro Android/Web

Para preparar versões futuras:

- manter regras de domínio em `UltraViagem.Core`;
- evitar lógica de negócio presa ao WPF;
- manter `trip.json` como contrato principal;
- documentar toda alteração de schema;
- se necessário, criar futuramente uma biblioteca compartilhável para validação, migração e sincronização.

## Decisões Atuais

- Sem banco de dados local por enquanto.
- Sem múltiplos JSONs por viagem.
- Sem categorias de tarefas por enquanto.
- Tarefas têm apenas `pendente` e `concluída`, com notas.
- Dicas são apenas `nome` e `link`, sem categorias.
- Arquivos têm apenas nome de arquivo, sem texto, categoria ou associação por enquanto.
- A edição do mapa acontece no Google My Maps; o app apenas guarda o link público e exibe o mapa.
- Favoritos ficam em `config.json`, na lista `favoriteTrips`; a estrela da visão geral e a janela de seleção usam o mesmo estado.
- A janela `Sobre` lê a data do build a partir do arquivo do assembly em execução.
- O roteiro usa slots abstratos (não horas do relógio) para posicionamento; `ItinerarySlotsPerDay` é por viagem e `ItinerarySlotWidth` é preferência de exibição por repositório.
- O cadastro de tipos de atividade é global (por repositório, em `config.json`); não há edição de tipos na UI por enquanto — apenas os 10 padrões são criados automaticamente.
