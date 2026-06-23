# Todo / Roadmap

## Prioridade Alta

- Importação de viagens antigas.

## Prioridade Baixa

- App Android (`UltraViagem.Android` — MAUI, fase 1 viewer)

   - Viewer básico implementado e funcionando: navegação Shell → TripsPage → TripPage.
   - Bugs de build corrigidos: bindings compilados em `ItineraryPage` ajustados via `NumberedDay` wrapper.
   - Bug crítico de inicialização corrigido: `MainApplication.cs` (subclasse de `MauiApplication`) estava ausente; sem ela o Android nunca chama `CreateMauiApp()` e o app exibe tela branca.

   - **Acesso à pasta via SAF (Storage Access Framework)**: a pasta de viagens é escolhida pelo usuário e a permissão é persistida (`TakePersistableUriPermission`). `TripFileService.ScanRepositoryAsync` varre as subpastas procurando `trip.json`.
     - Perda de acesso (`SecurityException` após reinstalar o APK ou reiniciar) é detectada (`ScanPermissionDenied`) e a TripsPage exibe um card vermelho pedindo para reautorizar via "Trocar pasta".
     - O rótulo da pasta resolve o nome real mesmo em provedores de nuvem (Google Drive usa docIds opacos): cai para consulta de `_display_name` via `ContentResolver` quando o docId não é hierárquico.

   - **TripPage com drawer lateral (hambúrguer)**: troca de seção sem recriar a página; o conteúdo de cada seção é injetado em `ContentArea.Content`. Evento `TripViewModel.SectionRequested` permite que páginas-filho disparem a troca de seção.
     - Botão voltar (hardware): numa seção interna volta para a Visão Geral; só na Visão Geral fecha a viagem e retorna à lista.

   - **Visão Geral**: grade 2×3 de blocos coloridos (pastel), um por seção (Roteiro, Tarefas, Mapa, Gastos, Dicas, Arquivos), cada bloco com ícone (Tabler outline embutido como `Path` SVG), título e resumo, clicável para a respectiva seção. O bloco Mapa abre o Google My Maps direto.

   - **Arquivos** (`FilesPage`): lista os anexos de `Trip.Attachments` (não varre a pasta). Toque abre o arquivo; toque longo entra em modo de seleção com checkboxes e barra de ação Baixar/Excluir.
     - Abertura/cópia/exclusão funcionam tanto em armazenamento local quanto no Google Drive: `BuildSiblingUri` (manipula docId hierárquico) com fallback para `FindSiblingInFolder` (enumera filhos da pasta pelo nome, para docIds opacos). O `FolderUri` da viagem é capturado no scan e propagado até o `TripViewModel`.
     - Download copia para `Download/UltraViagem/` via `MediaStore`.

   - Próximos passos: edição de itens no Android, separar Core em biblioteca compartilhada, avaliar sincronização multi-dispositivo.

## Dívidas Técnicas

- `MainWindow.xaml.cs` concentra navegação e comandos demais.
- `AppViewModel.cs` concentra muitas coleções e conversões.
- Falta suite de testes para serialização e regras de persistência.
