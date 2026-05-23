# Funcionalidades

## Menu Lateral (Sidebar)

A sidebar é colapsável: clicar no logo + nome do app alterna entre o modo expandido (220 px) e o modo colapsado (68 px) com animação suave.

- **Modo expandido**: ícone + label de cada item, logo com nome "UltraViagem" e painel de repositório visível.
- **Modo colapsado**: apenas os ícones, centralizados horizontalmente; o painel de repositório é substituído por um ícone de pasta que abre a pasta diretamente.
- O estado (expandido/colapsado) é salvo em `%APPDATA%\UltraViagem\settings.json` e restaurado na próxima abertura.

## Seleção De Repositório

O usuário escolhe uma pasta raiz onde ficam as viagens. O caminho é salvo em:

```text
%APPDATA%\UltraViagem\settings.json
```

Na sidebar expandida, o caminho do repositório aparece como link clicável para abrir a pasta.

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
- moeda base;
- casas decimais para cotações (controla a precisão das taxas exibidas na aba Moedas);
- caminho completo da pasta da viagem.

As datas são editadas por controle de calendário e exibidas no formato do sistema. A tela permite renomear apenas a pasta da viagem dentro do repositório atual, sem alterar a pasta raiz escolhida.

A tela segue o mesmo padrão dos demais painéis internos, com ícone, título, botão `<` para voltar e, no rodapé:

- botão `Salvar Dados` (direita);
- botão `Copiar Viagem` (direita) — abre modal pré-preenchido com "Cópia de [Nome]"; ao confirmar, cria nova pasta com cópia integral do conteúdo e abre a nova viagem;
- botão `Excluir Viagem` (esquerda, destrutivo) — exige confirmação; remove a pasta da viagem e volta à tela de seleção.

Ao trocar de viagem, o painel de dados é atualizado automaticamente para refletir a viagem carregada.

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

### Card de roteiro na visão geral

O card de roteiro exibe um scroll horizontal com um card por dia do itinerário ativo. Cada card mostra:

- **Círculo numerado** (número do dia, destaque em cor de acento) no canto superior esquerdo.
- **Título** do dia e **data** (se preenchida) na primeira linha.
- **OvernightLabel**: título da primeira atividade do tipo `Pernoite`, ou o resumo do dia — exibido abaixo de um ícone de mapa, indicando acomodação/destino.
- **Contagem de atividades** com ícone de marcador 📍 (ex.: `📍 5 atividades`).
- **Lista resumida** das primeiras 4 atividades, cada uma mostrando ícone (`OverviewIcon`), título e horário calculado (`TimeLabel`).
  - Ícones padrão: 🍴 para `Refeição`, 🛏 para `Pernoite`, 📍 para demais tipos.
  - Horário calculado com base em 08:00 como início do dia e faixa de 16 horas, proporcional ao slot de início da atividade.
- **"Ver detalhes ›"** — link clicável que navega para o painel de roteiro.

O scroll horizontal responde ao rolar do mouse (wheel vertical é convertido em scroll horizontal).
Clicar em qualquer área do card também navega para o painel de roteiro.

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

## Roteiro

O roteiro (itinerário) é uma linha do tempo visual baseada em blocos por dia. Cada viagem tem um número configurável de blocos por dia (`ItinerarySlotsPerDay`, padrão 16), editável nos Dados da Viagem. O campo `ShowItineraryGrid` (padrão `false`) ativa linhas verticais divisórias de slot no canvas de cada dia e do banco; pode ser ativado ou desativado em Dados da Viagem.

### Versões de roteiro

Cada viagem pode ter múltiplas versões independentes do roteiro, armazenadas em `itineraryVersions` no `trip.json`. Cada versão possui dias, atividades e banco próprios.

- Uma barra de abas exibida abaixo do cabeçalho do painel lista as versões existentes.
- A versão ativa tem fundo branco (`PanelBackground`) e borda de destaque (`AccentBrush`); as demais ficam com fundo `SidebarPanelBackground`.
- Clicar em uma aba inativa salva a versão atual e carrega a versão selecionada.
- Duplo-clique na aba inicia renomeação inline; `Enter` ou perda de foco confirma; `Esc` cancela.
- O botão `+ Nova versão` cria uma versão vazia.
- O botão `⧉ Duplicar` cria uma cópia independente da versão ativa com todos os dias, atividades e banco.
- O botão de lixeira em cada aba exclui a versão com confirmação; a última versão não pode ser excluída.
- Ao trocar de versão, qualquer edição aberta de dia ou atividade é descartada.
- Viagens criadas antes da funcionalidade são migradas automaticamente: o roteiro existente vira "Versão 1".
- `activeVersionId` no `trip.json` indica a versão ativa; os campos legados (`itinerary`, `bankRows`, `bankActivities`) espelham a versão ativa após cada salvamento para compatibilidade com versões antigas do app.

### Dias

- Cada dia tem um título livre, uma data opcional e um resumo de texto livre.
- O botão `+ Dia` adiciona um novo dia ao final.
- Clicar duas vezes no painel do nome do dia entra em modo de edição: campos `Nome`, `Data` e `Resumo` ficam editáveis. Enter confirma; Esc cancela. Um segundo duplo-clique cancela a edição.
- O botão `🗑️ Excluir Dia` aparece na própria forma de edição; a exclusão pede confirmação.
- O dia em edição recebe borda de destaque (AccentBrush) e os demais dias ficam esmaecidos (opacidade 0,35).

### Atividades

Cada atividade tem:

- **Título**;
- **Tipo** (`Atividade`, `Refeição` ou `Pernoite`): determina o estilo visual do bloco;
- **Ícone** (emoji livre, opcional);
- **Cor** (cor de fundo do bloco, hex);
- **Slot de início** (`StartSlot`): posição horizontal no canvas do dia;
- **Duração em slots** (`DurationSlots`): largura do bloco;
- **Detalhes** (texto livre, opcional).

Atividades do tipo `Pernoite` são exibidas com bordas arredondadas (pílula) e padding vertical reduzido para indicar visualmente a hospedagem.

### Canvas do dia

O canvas de cada dia representa visualmente todos os slots do dia em uma faixa horizontal. Três bandas decorativas mostram os períodos do dia:

- **Manhã** (slots 0 a 5): fundo azul-claro;
- **Tarde** (slots 6 a 11): fundo laranja-claro;
- **Noite** (slots 12 em diante): fundo roxo-claro.

As atividades são blocos coloridos absolutos sobre o canvas. A cor de fundo é definida individualmente por atividade; a cor do texto é calculada automaticamente (branco ou escuro) para contraste.

### Drag e redimensionamento

- **Mover**: clicar no centro do bloco e arrastar altera o `StartSlot`; o bloco encaixa no slot mais próximo durante o arraste.
- **Redimensionar**: clicar e arrastar na borda direita do bloco (área de ~10 px) altera o `DurationSlots`.
- **Mover entre dias**: arrastar um bloco sobre outro dia (destaque em azul) o transfere para aquele dia com recálculo do slot de destino.
- **Mover entre banco e dia**: arrastar um bloco do banco para um dia (ou vice-versa) o transfere entre os contextos.
- Os valores são clamped para não ultrapassar os limites do dia.
- Ao soltar o botão do mouse, o roteiro é salvo automaticamente.

### Banco de atividades

O banco é uma seção colapsável exibida abaixo de todos os dias. Ele armazena atividades extras que ainda não foram encaixadas em um dia da programação (reserva para uso posterior ou substituições).

- O banco tem uma ou mais **linhas** configuráveis via botões `−` e `+` no cabeçalho.
- Cada linha usa o mesmo canvas de slots que os dias do roteiro.
- Um único rótulo **"Banco"** aparece na coluna esquerda, abrangendo todas as linhas.
- O botão `+ Atividade` no cabeçalho cria uma nova atividade na primeira linha do banco.
- As atividades do banco têm os mesmos campos que as atividades dos dias (título, tipo, ícone, cor, detalhes, etc.) e o mesmo editor inline.
- Para **mover** uma atividade entre banco e dia (ou vice-versa), basta arrasta-la; não há botão de transferência.
- A quantidade de linhas e as atividades do banco são persistidas em `trip.json` (`bankRows`, `bankActivities`).

### Seleção e editor inline

Clicar em uma atividade a seleciona (destaque azul na borda). Um painel de edição aparece abaixo do canvas da respectiva linha, mostrando:

- campo `Título`;
- campo `Duração (blocos)`;
- combobox `Tipo` (`Atividade`, `Refeição`, `Pernoite`);
- campo `Detalhes` (texto livre);
- seletor de ícone (emoji) e de cor de fundo;
- botão `Copiar atividade` (duplica a atividade no mesmo dia);
- botão excluir.

Enter confirma a edição; Esc cancela. Clicar fora de qualquer atividade (área vazia do canvas) desmarca a seleção e cancela edições pendentes.

### Zoom

Os botões `−` e `+` no cabeçalho do painel alteram a largura em pixels de cada slot (`ItinerarySlotWidth`) em passos de 8 px. O valor é salvo em `config.json` e restaurado ao abrir o mesmo repositório.

### Salvamento

- O botão `Salvar Roteiro` salva explicitamente.
- O roteiro também é salvo automaticamente ao soltar o mouse após mover ou redimensionar uma atividade.

## Orçamento

Gastos ficam em `expenses`. A visão geral calcula apenas itens ativos:

- total estimado;
- total pago;
- total pendente;
- totais por categoria.

A tela de gastos permite:

- criar e excluir itens;
- ativar/desativar itens para simular opções sem apagar alternativas;
- navegar por abas de itens, resumo e Moedas;
- visualizar gastos como cards compactos agrupados por categoria, como transporte, hospedagem, passeios e outros;
- reordenar gastos dentro da mesma categoria com drag-and-drop;
- reordenar categorias inteiras com drag-and-drop pelo cabeçalho do grupo;
- selecionar um card alterando o fundo do próprio card;
- ativar/desativar um gasto por um controle visual de status no card;
- editar ou criar gastos expandindo o próprio card, sem abrir janela modal;
- editar descrição, empresa, link, observações, preço unitário, taxas unitárias, pessoas, quantidade, moeda, cotação do item e valor pago;
- acompanhar o subtotal convertido para a moeda base da viagem;
- consultar totais e distribuição por categoria na aba de resumo;
- cadastrar as moedas usadas na viagem por código ISO, símbolo e casas decimais (controla a precisão dos valores exibidos nessa moeda);
- editar nome e símbolo de cada moeda, com nome padrão igual ao código;
- excluir moedas que não sejam a moeda de referência e que não estejam em uso por gastos;
- visualizar moedas com código, nome, símbolo, casas decimais, taxa para a moeda de referência da viagem e data de atualização;
- editar manualmente a taxa de cada moeda;
- atualizar automaticamente as taxas públicas usando a AwesomeAPI quando houver internet;
- definir a precisão das taxas exibidas na aba Moedas pelo parâmetro "casas decimais para cotações" nos dados da viagem.

**Cotação automática ou fixa por gasto:** o campo Cotação do card de edição possui um checkbox "Automática". Quando marcado (padrão), a cotação segue a taxa cadastrada na aba Moedas e é atualizada automaticamente; o campo é exibido em cinza e bloqueado para edição. Quando desmarcado, a cotação fica fixa no valor informado manualmente e não é afetada por atualizações da aba Moedas. Ao marcar novamente como automática, o campo sincroniza imediatamente com a taxa atual.

Itens com valor pago ou com cotação fixa não têm a taxa sobrescrita pela atualização automática de cotações gerais, preservando a taxa efetivamente usada.
