# Todo / Roadmap

## Prioridade Alta

- Importação de viagens antigas.

### App Android — melhorias pós-uso real (levantadas em 16/09/2026)

Itens surgidos após usar o app numa viagem de verdade:

- **Resolver armazenamento OneDrive e Google Drive**: no Android 16 (testado no Galaxy S24) o seletor de pasta do sistema (`ACTION_OPEN_DOCUMENT_TREE`) só oferece armazenamento **local** — Google Drive e OneDrive não expõem mais acesso a pasta/árvore via SAF, então a tela de seleção de locais nem abre. Não há conserto no seletor. Solução adotada: acesso **via API nativa** (sem SAF), com backends plugáveis atrás de `ITripStorage`.
  - ✅ **Google Drive — feito e validado no aparelho** (listar, abrir, editar e salvar no Drive). Ver "Armazenamento na nuvem via API" na seção do Android abaixo.
  - ⬜ **OneDrive** — replicar o mesmo padrão com Microsoft Graph (`ITripStorage` já pronto; falta o backend + auth MSAL).
  - Futuro opcional: cache offline (baixar `trip.json` + anexos) para uso sem internet na viagem; anexos no Drive (abrir/baixar/excluir) — ainda usam o caminho SAF, faltam no backend do Drive.
- **Rever gastos (casas decimais e foco nos campos)**: revisar a formatação de casas decimais (valores) e o comportamento de foco/teclado ao editar os campos no `ExpenseEditPage` (ordem de foco, tipo de teclado numérico, seleção do conteúdo ao focar).
- **Abrir na última viagem**: ao abrir o app, ir direto para a última viagem aberta (pular a lista), com um caminho claro de voltar para a lista de viagens. Hoje já existe `GetLastTrip`/`LastTrip`; falta a navegação automática na inicialização.
- **Sincronizar My Maps**: hoje o bloco Mapa só abre o link do My Maps no navegador. Avaliar sincronizar/refletir o mapa (ex.: exibir embutido numa WebView como no desktop e/ou manter a URL/versão em dia).

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

   - **Visão Geral**: grade de blocos coloridos (pastel) com ícone (Tabler outline embutido como `Path` SVG), título e resumo. O primeiro bloco é **Detalhes** (slate) — abre o editor de metadados; os demais são as seções (Roteiro, Tarefas, Mapa, Gastos, Dicas, Arquivos). O bloco Mapa abre o Google My Maps direto.
     - **Editar metadados** (`TripDetailsEditPage`, modal): nome, datas (início/fim via `DatePicker`), nº de pessoas, moeda base e URL do mapa. Salvo via `TripViewModel.UpdateTripDetailsAsync`, que recalcula tudo que depende desses campos (datas do roteiro, totais, resumos) e dispara `TripUpdated` para atualizar o nome no drawer.

   - **Roteiro** (`ItineraryPage`): cada dia é um card (badge Dx + resumo + data) com as atividades em lista vertical ordenada por `StartSlot` (`ActivityRow`). Cada atividade mostra acento colorido (cor da atividade), título e tipo; toque expande os detalhes/notas quando houver (chevron só aparece em atividades com detalhes).
     - **Editar atividade** (`ActivityEditPage`, modal): toque longo abre o editor com título, tipo, **cor** (paleta) e detalhes. Salvo via `TripViewModel.UpdateActivityAsync`; preserva `StartSlot`/`DurationSlots` (não bagunça a linha do tempo do desktop). Ainda não há adicionar/excluir/reordenar atividades nem editar o resumo do dia.

   - **Permissão de escrita SAF** (corrigido): as edições (Gastos, Detalhes, Roteiro) precisam de permissão de **escrita** persistida na pasta — antes só se pegava leitura, então salvar no `trip.json` falhava silenciosamente. O `SaveRepoUri` agora faz `TakePersistableUriPermission` com **leitura + escrita** e, se o provedor recusar a escrita (ex.: OneDrive é somente-leitura via SAF), **cai para somente leitura** (a pasta ainda funciona como visualizador). O intent do `FolderPickerService` pede **apenas leitura** de propósito: exigir escrita no seletor faz o DocumentsUI da Samsung **desabilitar o "USAR ESTA PASTA"** em provedores somente-leitura. **Usuários existentes precisam usar "Trocar pasta" uma vez** para reautorizar com escrita.
     - **Limite descoberto no Android 16 (Galaxy S24)**: o seletor SAF (`ACTION_OPEN_DOCUMENT_TREE`) passou a oferecer **só o armazenamento interno** — Google Drive e OneDrive não expõem mais acesso a pasta/árvore, e a lista de locais nem abre. Por isso o acesso a pastas na nuvem passou a usar **APIs nativas** (ver abaixo).

   - **Armazenamento na nuvem via API (backend plugável)**: o armazenamento foi abstraído em `ITripStorage` (listar viagens, carregar/salvar `trip.json`, abrir/excluir anexos). Implementações:
     - `TripFileService` — SAF/armazenamento interno (o de sempre).
     - `GoogleDriveStorage` — **Google Drive API v3** (feito e validado no aparelho): varre a pasta de viagens (paralelizado, até 6 simultâneas, com token pré-aquecido), carrega/salva o `trip.json`. Anexos ainda não implementados neste backend.
     - Auth: `GoogleAuthService` — OAuth 2.0 + PKCE via `WebAuthenticator`, refresh token no `SecureStorage`. Cliente OAuth do tipo **iOS** no Google Cloud (o esquema de redirect reverso `com.googleusercontent.apps.<id>` é interceptado no Android; clientes Android não suportam mais esquema personalizado). App em modo **"Testando"** — a conta precisa estar na lista de usuários de teste do consent screen, senão o Google bloqueia o login.
     - Seleção de provedor na `TripsPage` (action sheet **Google Drive / Armazenamento interno**); pasta do Drive escolhida via `DriveFolderPickerPage` (navegador de pastas). O repositório ativo (provedor + ref + rótulo) é persistido; `TripsViewModel`/`TripViewModel` roteiam scan/load/**save** pelo backend ativo.
     - A **"última viagem"** é vinculada ao repositório atual (`last_trip_repo`): não mostra o atalho se pertence a outro provedor/pasta (evitava tentar abrir uma ref SAF pelo Drive → "acesso perdido").
     - Manifesto: intent-filter da `WebAuthenticationCallbackActivity` com o esquema do redirect + permissão `INTERNET`.
     - Pendente: backend OneDrive (Microsoft Graph + MSAL) no mesmo padrão; anexos no Drive; cache offline opcional.

   - **Exportação PDF** (`AndroidPdfExporter` + `CalibriFontResolver`): o QuestPDF usado no desktop **não roda no Android** (recusa runtimes não-suportados, sem binário nativo `QuestPdfSkia` para Android). Por isso o Android reimplementa o mesmo layout com **MigraDoc/PDFsharp** (`PDFsharp-MigraDoc`), que roda no Android, reproduzindo de perto a saída do `TripPdfExporter`: mesmas 6 seções (Roteiro, Roteiro Detalhado por versão em landscape, Dicas, Gastos, Orçamento Detalhado em landscape, Tarefas), cores, tamanhos e o diagrama de slots (tabela com células mescladas).
     - A fonte **Calibri** fica embutida em `UltraViagem.Core/Fonts` (resource) e é fornecida ao PDFsharp via `IFontResolver` (o Android não tem Calibri no sistema) — sem isso a quebra/paginação divergiria.
     - Gatilho: item "Exportar PDF" no drawer da `TripPage` → gera em `FileSystem.CacheDirectory` (`TripViewModel.ExportPdfAsync`) e abre a folha de compartilhamento (`Share`).
     - Observação: por ser outro motor de layout, não é byte-a-byte idêntico ao desktop, mas visualmente equivalente.

   - **Arquivos** (`FilesPage`): lista os anexos de `Trip.Attachments` (não varre a pasta). Toque abre o arquivo; toque longo entra em modo de seleção com checkboxes e barra de ação Baixar/Excluir.
     - Abertura/cópia/exclusão funcionam tanto em armazenamento local quanto no Google Drive: `BuildSiblingUri` (manipula docId hierárquico) com fallback para `FindSiblingInFolder` (enumera filhos da pasta pelo nome, para docIds opacos). O `FolderUri` da viagem é capturado no scan e propagado até o `TripViewModel`.
     - Download copia para `Download/UltraViagem/` via `MediaStore`.

   - **Cache da lista de viagens**: o resultado do scan é persistido em `trips_cache.json` (arquivo privado em `FileSystem.AppDataDirectory`, contendo `repoUri` + entries). Na abertura, a lista do cache é exibida imediatamente e o scan roda em segundo plano (`ScanAsync(silent: true)`), atualizando a lista só se mudou — evita a espera do scan do Drive, que é lento com muitas viagens.
     - O cache só é usado se o `repoUri` salvo bate com a pasta selecionada; trocar de pasta o ignora e sobrescreve.
     - Se o acesso for perdido num rescan silencioso, o cache permanece visível e o card de erro aparece (em vez de esvaziar a tela).

   - **Gastos** (`ExpensesPage`): lista agrupada por categoria (`CollectionView` com `IsGrouped`), construída em `TripViewModel.BuildExpenseGroups`.
     - Cada grupo tem cabeçalho com ícone Tabler + cor por categoria (`ExpenseCategoryStyle`, com mapa fixo de categorias conhecidas — Hospedagem/Transporte/Passeios/Refeição/Compras — e fallback genérico cinza para categorias livres; normaliza acentos e maiúsculas) e o subtotal da categoria.
     - Itens (`ExpenseRow`) mostram título, status (✓ pago / pendente, inativos esmaecidos) e valor na moeda base. Toque expande o item exibindo Fornecedor, Preço unit. (na moeda do item), Pessoas × Qtd, Câmbio (só quando a moeda ≠ base), Pago, notas e link "Abrir reserva".
     - Cartão-resumo no rodapé: Estimado / Pago / A pagar (este em vermelho) + `ProgressBar` (`PaidFraction`). Totais consideram apenas itens ativos.
     - **Edição** (`ExpenseEditPage`, modal): toque longo no item abre o editor com formulário completo (título, categoria, fornecedor, link, observações, preço unit., taxas, pessoas, quantidade, moeda, câmbio, valor pago e toggle ativo); botão Excluir (com confirmação). FAB "+" cria um gasto novo com defaults da viagem. Persistido via `TripViewModel.AddExpenseAsync` / `UpdateExpenseAsync` / `DeleteExpenseAsync`, que reconstroem grupos e totais e salvam o `trip.json`.

   - **Pendências do app Android** (o que falta para ficar completo como ferramenta de viagem):

     - **Gestão de viagens** (hoje só abre viagens existentes; editar metadados já feito — ver acima):
       - Criar viagem nova do zero (equivalente ao fluxo de criação do desktop).
       - Excluir viagem.
     - **Edição que ainda falta** (Gastos, Dicas e Tarefas já têm criar/editar/excluir; Roteiro já tem editar atividade):
       - Roteiro: adicionar/excluir/reordenar atividades, editar resumo do dia e adicionar/excluir dias (envolve slots/posição — mais complexo).
       - Anexar novos arquivos na tela de Arquivos (hoje só lista/abre/baixa/exclui).
     - **Moedas / Cotações**: não há a aba de cotações do desktop (cadastro de moedas + atualização automática de câmbio via AwesomeAPI). Gastos mostram o câmbio salvo, mas não há gestão/atualização de taxas.
     - **Versões de roteiro**: a UI usa só a versão ativa; falta poder trocar entre versões (o PDF já exporta todas).
     - **Mapa**: hoje só abre o link no navegador; opcionalmente exibir o My Maps embutido numa WebView, como o desktop.
     - **Diversos / qualidade**: favoritos de viagens (`favoriteTrips` do config); filtro/ordenação em Tarefas e Gastos; estados de erro/vazio mais consistentes.

     Prioridade sugerida para "fechar" o app: (1) criar/editar viagem, (2) anexar arquivos, (3) editar roteiro.

   - Próximos passos transversais: separar Core em biblioteca compartilhada, avaliar sincronização multi-dispositivo.

## Dívidas Técnicas

- `MainWindow.xaml.cs` concentra navegação e comandos demais.
- `AppViewModel.cs` concentra muitas coleções e conversões.
- Falta suite de testes para serialização e regras de persistência.
