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
- salvar uma viagem.

O formato de dados atual fica em:

```text
<raiz-das-viagens>/config.json
<raiz-das-viagens>/<id-da-viagem>/trip.json
```

## Modelo De Dados

`Trip` contém:

- dados gerais: `id`, `title`, datas, moeda e pessoas;
- `myMapsUrl`: URL pública do Google My Maps exibida pela tela de mapa;
- `itinerary`: roteiro por dia;
- `tasks`: tarefas;
- `places`: lugares;
- `links`: dicas;
- `expenses`: gastos, com flag de ativação, categoria, moeda, taxa de conversão e valor pago;
- `currencyRates`: cotações por moeda para conversão para a moeda base da viagem;
- `attachments`: arquivos anexos.

`LinkItem` é usado para o quadro de dicas:

```json
{
  "id": "dica-...",
  "title": "Guia Carretera Austral",
  "url": "https://..."
}
```

## Navegação Atual

A janela principal alterna painéis:

- `OverviewPanel`: visão geral.
- `TripDetailsPanel`: edição dos dados gerais da viagem.
- `TasksPanel`: edição de tarefas.
- `TipsPanel`: edição de dicas.
- `MapPanel`: visualização do Google My Maps.
- `FilesPanel`: arquivos anexos.

Os botões ativos do menu lateral são estilizados no code-behind por `SetActiveNav`.

## Salvamento

Tarefas, dicas e arquivos possuem salvamento automático via eventos do ViewModel:

- `TasksChanged` chama `SaveTasksInternal`.
- `TipsChanged` chama `SaveTipsInternal`.
- `AttachmentsChanged` chama `SaveAttachmentsInternal`.

O link do mapa é salvo a partir da tela `Mapa`, diretamente em `Trip.MyMapsUrl`.
O mapa é exibido com `Microsoft.Web.WebView2`, usando o formato público embutido do Google My Maps (`/maps/d/embed?mid=...`).

Arquivos anexados são copiados para a pasta da viagem. A lista de anexos salva apenas o nome do arquivo relativo à pasta da viagem.
Na UI WPF, `AttachmentIconConverter` consulta o Shell do Windows para exibir o ícone associado à extensão configurada no sistema operacional.

Também existem botões explícitos de salvar para cada tela.

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
