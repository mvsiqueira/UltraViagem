# Funcionalidades

## Seleção De Repositório

O usuário escolhe uma pasta raiz onde ficam as viagens. O caminho é salvo em:

```text
%APPDATA%\UltraViagem\settings.json
```

Na sidebar, o caminho do repositório aparece como link clicável para abrir a pasta.

## Seleção De Viagem

O botão superior com a viagem atual abre a janela de seleção. Ele exibe ícone de mala, nome da viagem, período com duração em dias e um indicador de abertura.
A janela:

- mostra viagens favoritas no topo;
- agrupa demais viagens por ano;
- permite marcar/desmarcar favoritas com estrela amarela quando marcada;
- permite iniciar criação de nova viagem.

Ao criar uma viagem, a pasta é nomeada com o ano e mês da data inicial, seguidos pelo nome normalizado da viagem, no formato `yyyy-mm-nome-da-viagem`.

## Dados Da Viagem

O botão `Dados da viagem` abre uma tela interna com:

- nome;
- data inicial;
- data final;
- pessoas;
- moeda;
- caminho completo da pasta da viagem.

As datas são editadas por controle de calendário e exibidas no formato do sistema. A tela permite renomear apenas a pasta da viagem dentro do repositório atual, sem alterar a pasta raiz escolhida.

A tela segue o mesmo padrão dos demais painéis internos, com ícone, título, botão `<` para voltar e botão `Salvar Dados` no rodapé do formulário.

## Sobre

O item `! Sobre` no menu lateral abre uma janela modal com:

- nome do aplicativo;
- versão;
- data do build lida do assembly compilado;
- autor: `Marcus Siqueira via ChatGPT Codex`;
- descrição curta do app.

## Visão Geral

A visão geral resume:

- roteiro por dia;
- tarefas pendentes/concluídas;
- gastos estimados, pagos e pendentes;
- mapa do Google My Maps;
- dicas;
- arquivos.

O cabeçalho da visão geral exibe o título da viagem, uma estrela de favorito clicável e o período com duração inclusiva em dias.
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
- links válidos aparecem clicáveis diretamente no card;
- o botão `>` abre a tela completa.

Na tela de dicas:

- lista compacta de cards com nome e texto/link;
- botão `+ Nova Dica`;
- links válidos aparecem clicáveis;
- o lápis coloca o card em edição inline;
- `F2` edita o card selecionado;
- `Enter` aplica a edição;
- `Esc` descarta a edição;
- sair do foco aplica a edição;
- duplo clique no espaço do nome ou texto/link entra em edição;
- cada card tem ação para excluir;
- botão `Salvar Dicas`;
- autosave em alterações.

## Arquivos

Arquivos são representados em `attachments` no `trip.json`.

Na visão geral:

- aparece um card compacto com os primeiros arquivos;
- cada arquivo exibe ícone, nome, tipo e tamanho;
- o nome do arquivo é clicável para abrir o arquivo;
- o botão `>` abre a tela completa.

Na tela de arquivos:

- é possível anexar arquivos por botão;
- é possível anexar arquivos por drag-and-drop em qualquer área da seção;
- os arquivos anexados são copiados para a pasta da viagem;
- a lista visual mostra cada arquivo como um card compacto com ícone do tipo de arquivo configurado no Windows, nome clicável, tipo e tamanho;
- clicar no card seleciona o arquivo;
- o topo da tela mantém apenas a ação global `+ Anexar arquivos`;
- clicar no nome do arquivo abre o arquivo;
- cada card tem ações por ícone para renomear e excluir;
- os ícones de ação têm tooltip;
- o botão de renomear permite mudar o nome do arquivo inline;
- a tecla `F2` renomeia inline o arquivo selecionado;
- duplo clique no card ou no nome entra em edição inline;
- `Enter` aplica a edição;
- `Esc` descarta a edição;
- sair do foco aplica a edição;
- é possível reordenar os arquivos com drag-and-drop dentro da lista;
- ao salvar, o arquivo físico também é renomeado na pasta da viagem;
- é possível excluir o arquivo selecionado;
- ao excluir, o arquivo é removido da lista e apagado fisicamente da pasta da viagem;
- a remoção exige confirmação;
- a tecla `Delete` exclui o arquivo selecionado, também com confirmação.

## Mapa

O app usa `MyMapsUrl` no `trip.json` para exibir um mapa público do Google My Maps.

Na tela de mapa:

- o usuário cola ou edita o link público do Google My Maps;
- o link é salvo ao pressionar `Enter`, sair do campo ou clicar em `Salvar URL`;
- o app transforma links de edição/visualização do My Maps em URL pública embutida (`/maps/d/embed?mid=...`);
- o mapa é exibido dentro do app usando WebView2;
- o botão `Abrir no navegador` abre o link original no navegador padrão.

Na visão geral:

- o card de mapa exibe o mesmo mapa embutido em formato compacto;
- o botão `>` abre a tela completa de mapa.

A edição do mapa, rotas, camadas e pontos é feita no próprio Google My Maps. O app não edita o conteúdo do mapa nem exporta KML.

## Orçamento

Gastos ficam em `expenses`. A visão geral calcula apenas itens ativos:

- total estimado;
- total pago;
- total pendente;
- totais por categoria.

A tela de gastos permite:

- criar e excluir itens;
- ativar/desativar itens para simular opções sem apagar alternativas;
- visualizar gastos como cards compactos agrupados por categoria, como transporte, hospedagem, passeios e outros;
- selecionar um card alterando o fundo do próprio card;
- ativar/desativar um gasto por um controle visual de status no card;
- editar o gasto selecionado em uma janela dedicada com todos os campos da planilha de referência;
- editar descrição, empresa, link, observações, preço unitário, taxas unitárias, pessoas, quantidade, moeda, cotação do item e valor pago;
- acompanhar o subtotal convertido para BRL;
- editar manualmente cotações por moeda;
- atualizar automaticamente cotações públicas para BRL usando a AwesomeAPI quando houver internet.

Quando um item já tem valor pago, a cotação do item não é sobrescrita pela atualização automática das cotações gerais, para preservar a taxa efetivamente usada no pagamento.
