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

Na tela de tarefas:

- a lista é única, sem painel lateral de edição;
- é possível filtrar todas, pendentes e concluídas;
- o topo da tela mantém apenas a ação global `+ Nova Tarefa`;
- cada card mostra checkbox de conclusão, título e notas (notas só aparecem se preenchidas);
- a checkbox alterna o status entre `pending` e `done` (não há campo de status em edição);
- cada card tem ações por ícone para editar e excluir, no mesmo padrão da tela de gastos;
- ao acionar editar, o card expande mostrando os campos `Título` e `Notas` inline;
- os botões de ação trocam para aceitar (✓) e rejeitar (×) enquanto a edição está aberta;
- `Enter` aceita a edição; `Shift+Enter` quebra linha no campo `Notas`; `Esc` descarta;
- duplo clique no card também alterna entre editar e descartar;
- aceitar persiste imediatamente e fecha o editor;
- rejeitar restaura os valores anteriores (ou descarta a tarefa se ela acabou de ser criada);
- excluir remove a tarefa imediatamente e salva;
- é possível reordenar as tarefas com drag-and-drop dentro da lista;
- botão `Salvar Tarefas` no rodapé da tela.

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
- é possível reordenar as dicas com drag-and-drop dentro da lista;
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
- a lista visual mostra cada arquivo como um card compacto com ícone do tipo de arquivo configurado no Windows, nome, tipo e tamanho;
- clicar no card seleciona o arquivo;
- o topo da tela mantém apenas a ação global `+ Anexar arquivos`;
- duplo clique no card abre o arquivo no aplicativo padrão do Windows;
- cada card tem ações por ícone para renomear e excluir;
- os ícones de ação têm tooltip;
- o botão de renomear permite mudar o nome do arquivo inline;
- a tecla `F2` renomeia inline o arquivo selecionado;
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
- navegar por abas de itens, resumo e cotações;
- visualizar gastos como cards compactos agrupados por categoria, como transporte, hospedagem, passeios e outros;
- reordenar gastos dentro da mesma categoria com drag-and-drop;
- reordenar categorias inteiras com drag-and-drop pelo cabeçalho do grupo;
- selecionar um card alterando o fundo do próprio card;
- ativar/desativar um gasto por um controle visual de status no card;
- editar ou criar gastos expandindo o próprio card, sem abrir janela modal;
- editar descrição, empresa, link, observações, preço unitário, taxas unitárias, pessoas, quantidade, moeda, cotação do item e valor pago;
- acompanhar o subtotal convertido para BRL;
- consultar totais e distribuição por categoria na aba de resumo;
- cadastrar as moedas usadas na viagem por código ISO e símbolo informado pelo usuário;
- editar nome e símbolo de cada moeda, com nome padrão igual ao código;
- excluir cotações que não sejam a moeda de referência e que não estejam em uso por gastos;
- visualizar cotações por moeda com código, nome, símbolo, taxa para a moeda de referência da viagem e data de atualização;
- editar manualmente cotações por moeda;
- atualizar automaticamente cotações públicas para BRL usando a AwesomeAPI quando houver internet.

Quando um item já tem valor pago, a cotação do item não é sobrescrita pela atualização automática das cotações gerais, para preservar a taxa efetivamente usada no pagamento.
