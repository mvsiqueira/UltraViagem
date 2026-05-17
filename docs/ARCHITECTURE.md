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

- dados gerais: `id`, `title`, datas, moeda, pessoas, URL do Google My Maps;
- `itinerary`: roteiro por dia;
- `tasks`: tarefas;
- `places`: lugares;
- `links`: dicas;
- `expenses`: gastos;
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
- `TasksPanel`: edição de tarefas.
- `TipsPanel`: edição de dicas.

Os botões ativos do menu lateral são estilizados no code-behind por `SetActiveNav`.

## Salvamento

Tarefas e dicas possuem salvamento automático via eventos do ViewModel:

- `TasksChanged` chama `SaveTasksInternal`.
- `TipsChanged` chama `SaveTipsInternal`.

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
