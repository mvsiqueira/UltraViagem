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

As telas internas de cards ativos, como `Tarefas`, `Dicas` e `Arquivos`, têm um botão `<` no cabeçalho para voltar à visão geral.
Na visão geral, cards com tela interna usam um botão `>` no canto superior direito para navegação. Criação de novos itens fica nas telas internas, não nos cards de resumo.

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
- o botão `>` abre a tela completa.

Na tela de dicas:

- tabela simples com `Nome` e `Link`;
- botão `Nova`;
- botão `Abrir link`;
- botão `Excluir`;
- botão `Salvar Dicas`;
- autosave em alterações.

## Arquivos

Arquivos são representados em `attachments` no `trip.json`.

Na visão geral:

- aparece um card compacto com os primeiros arquivos;
- o botão `>` abre a tela completa.

Na tela de arquivos:

- é possível anexar arquivos por botão;
- é possível anexar arquivos por drag-and-drop em qualquer área da seção;
- os arquivos anexados são copiados para a pasta da viagem;
- a lista visual mostra cada arquivo como um card com ícone do tipo de arquivo configurado no Windows e texto;
- clicar no card seleciona o arquivo;
- o topo da tela mantém apenas a ação global `+ Anexar arquivos`;
- cada card tem ações por ícone para abrir, renomear e excluir;
- os ícones de ação têm tooltip;
- o botão de renomear permite mudar o nome do arquivo;
- a tecla `F2` renomeia o arquivo selecionado;
- é possível reordenar os arquivos com drag-and-drop dentro da lista;
- ao salvar, o arquivo físico também é renomeado na pasta da viagem;
- é possível abrir o arquivo selecionado;
- duplo clique no card abre o arquivo;
- é possível excluir o arquivo selecionado;
- ao excluir, o arquivo é removido da lista e apagado fisicamente da pasta da viagem;
- a remoção exige confirmação;
- a tecla `Delete` exclui o arquivo selecionado, também com confirmação.

## Mapa

O modelo tem `MyMapsUrl` nos dados da viagem e `places` no `trip.json`. A visão geral exibe um preview visual simples, mas ainda não há integração real com Google My Maps nem exportação KML funcional.

## Orçamento

Gastos ficam em `expenses`. A visão geral calcula:

- total estimado;
- total pago;
- total pendente;
- totais por categoria.

Ainda falta tela de edição completa dos gastos.
