# Funcionalidades

## Seleção De Repositório

O usuário escolhe uma pasta raiz onde ficam as viagens. O caminho é salvo em:

```text
%APPDATA%\UltraViagem\settings.json
```

Na sidebar, o caminho do repositório aparece como link clicável para abrir a pasta.

## Seleção De Viagem

O botão superior com a viagem atual abre a janela de seleção. A janela:

- mostra viagens favoritas no topo;
- agrupa demais viagens por ano;
- permite marcar/desmarcar favoritas;
- permite iniciar criação de nova viagem.

## Dados Da Viagem

O botão `Dados da viagem` abre o modal de edição com:

- nome;
- data inicial;
- data final;
- pessoas;
- moeda;
- link do Google My Maps.

## Visão Geral

A visão geral resume:

- roteiro por dia;
- tarefas pendentes/concluídas;
- gastos estimados, pagos e pendentes;
- mapa/lugares;
- dicas;
- arquivos.

## Tarefas

Tarefas têm:

- título;
- status: `pending` ou `done`;
- notas.

Na visão geral, tarefa concluída aparece marcada, cinza e riscada.

Na tela de tarefas, é possível:

- filtrar todas, pendentes e concluídas;
- criar tarefa;
- excluir tarefa;
- editar título, status e notas;
- salvar manualmente ou por autosave.

## Dicas

Dicas usam a lista `links` do `trip.json`.

Na visão geral:

- aparece um card compacto com as primeiras dicas;
- o botão `+` cria uma dica;
- o botão `Ver todas as dicas` abre a tela completa.

Na tela de dicas:

- tabela simples com `Nome` e `Link`;
- botão `Nova`;
- botão `Abrir link`;
- botão `Excluir`;
- botão `Salvar Dicas`;
- autosave em alterações.

## Arquivos

Arquivos são representados em `attachments` no `trip.json`. A visão geral já mostra uma lista compacta, mas ainda falta tela completa de gerenciamento.

## Mapa

O modelo tem `MyMapsUrl` nos dados da viagem e `places` no `trip.json`. A visão geral exibe um preview visual simples, mas ainda não há integração real com Google My Maps nem exportação KML funcional.

## Orçamento

Gastos ficam em `expenses`. A visão geral calcula:

- total estimado;
- total pago;
- total pendente;
- totais por categoria.

Ainda falta tela de edição completa dos gastos.
