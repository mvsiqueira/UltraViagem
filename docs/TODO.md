# Todo / Roadmap

# Pendências

## Prioridade Alta

- Importação de viagens antigas.
- **App Android — Rever gastos (casas decimais e foco nos campos)**: revisar a formatação de casas decimais (valores) e o comportamento de foco/teclado ao editar os campos no `ExpenseEditPage` (ordem de foco, tipo de teclado numérico, seleção do conteúdo ao focar).
- **App Android — Sincronizar My Maps**: hoje o bloco Mapa só abre o link do My Maps no navegador. Avaliar sincronizar/refletir o mapa (ex.: exibir embutido numa WebView como no desktop e/ou manter a URL/versão em dia).

## Demais pendências do app Android

O que falta para o app ficar completo como ferramenta de viagem:

- **Edição que ainda falta** (Gastos, Dicas e Tarefas já têm criar/editar/excluir; Roteiro já tem editar atividade):
  - Roteiro: adicionar/excluir/reordenar atividades, editar resumo do dia e adicionar/excluir dias (envolve slots/posição — mais complexo).
  - Anexar novos arquivos na tela de Arquivos (hoje lista/abre/baixa/exclui).
- **Moedas / Cotações**: não há a aba de cotações do desktop (cadastro de moedas + atualização automática de câmbio via AwesomeAPI). Gastos mostram o câmbio salvo, mas não há gestão/atualização de taxas.
- **Versões de roteiro**: a UI usa só a versão ativa; falta poder trocar entre versões (o PDF já exporta todas).
- **Diversos / qualidade**: favoritos de viagens (`favoriteTrips` do config); filtro/ordenação em Tarefas e Gastos; estados de erro/vazio mais consistentes.
- **Gestão de viagens** — prioridade menor (pode nem ser implementado no celular); editar metadados já está feito:
  - Criar viagem nova do zero (equivalente ao fluxo de criação do desktop).
  - Excluir viagem.

## Transversais

- Separar Core em biblioteca compartilhada.
- Avaliar sincronização multi-dispositivo (edição offline com sincronização posterior ficou fora do escopo do uso offline do Android, por decisão).

## Dívidas Técnicas

- `MainWindow.xaml.cs` concentra navegação e comandos demais.
- `AppViewModel.cs` concentra muitas coleções e conversões.
- Falta suite de testes para serialização e regras de persistência.

---

# Concluído

## App Android — melhorias pós-uso real (levantadas em 16/09/2026)

Itens surgidos após usar o app numa viagem de verdade:

- ✅ **Resolver armazenamento OneDrive e Google Drive**: no Galaxy S24 (Android 16) a lista de locais do seletor de pasta do sistema (`ACTION_OPEN_DOCUMENT_TREE`) parou de abrir, deixando só o armazenamento interno acessível. Depois voltou a funcionar (provavelmente após resetar o seletor com `pm clear com.google.android.documentsui`, ou por atualização do Drive/Arquivos) e o **Google Drive voltou a aparecer no seletor**; o **OneDrive continua não aparecendo**. Solução adotada mesmo assim: acesso **via API nativa**, com backends plugáveis atrás de `ITripStorage` — mais rápido (scan paralelo) e independente das instabilidades do seletor. O caminho SAF para o Drive fica como plano B.
  - ✅ **Google Drive — feito e validado no aparelho** (listar, abrir, editar e salvar no Drive). Ver "Armazenamento na nuvem via API" abaixo.
  - ✅ **OneDrive — feito e validado no aparelho** (Microsoft Graph; listar, abrir, editar e salvar).
  - ✅ **Sessões mantidas**: trocar de provedor reaproveita o login salvo de cada um (sem logar de novo).
  - ✅ **Anexos na nuvem**: abrir, baixar e excluir anexos funcionam em viagens do Drive e do OneDrive (validado no aparelho).
  - ✅ **Uso offline (somente leitura)**: viagens da nuvem abrem sem internet pela cópia no celular; anexos baixados pelo botão "Baixar para uso offline" abrem sem conexão; edições ficam bloqueadas com aviso (validado no aparelho). Edição offline com sincronização posterior ficou fora de escopo por decisão.
- ✅ **Abrir na última viagem**: ao abrir, o app vai direto para a última viagem do repositório atual (pula a lista); o voltar na Visão Geral retorna à lista. A abertura não espera a varredura da nuvem — `TripsViewModel.PrepareRepo()` resolve o repositório e a última viagem pelo cache/prefs (rápido), abre na hora, e a lista é varrida em segundo plano (`RescanAsync`). Guardado por `_initialized` (abre só uma vez por processo, sem loop ao voltar).

## App Android (`UltraViagem.Android`, .NET MAUI) — funcionalidades implementadas

- Base: navegação Shell → TripsPage → TripPage.
- Bugs de build corrigidos: bindings compilados em `ItineraryPage` ajustados via `NumberedDay` wrapper.
- Bug crítico de inicialização corrigido: `MainApplication.cs` (subclasse de `MauiApplication`) estava ausente; sem ela o Android nunca chama `CreateMauiApp()` e o app exibe tela branca.

- **Acesso à pasta via SAF (Storage Access Framework)**: a pasta de viagens é escolhida pelo usuário e a permissão é persistida (`TakePersistableUriPermission`). `TripFileService.ScanRepositoryAsync` varre as subpastas procurando `trip.json`.
  - Perda de acesso (`SecurityException` após reinstalar o APK ou reiniciar) é detectada (`ScanPermissionDenied`) e a TripsPage exibe um card vermelho pedindo para reautorizar via "Trocar pasta".
  - O rótulo da pasta resolve o nome real mesmo em provedores de nuvem (Google Drive usa docIds opacos): cai para consulta de `_display_name` via `ContentResolver` quando o docId não é hierárquico.

- **TripPage com drawer lateral (hambúrguer)**: troca de seção sem recriar a página; o conteúdo de cada seção é injetado em `ContentArea.Content`. Evento `TripViewModel.SectionRequested` permite que páginas-filho disparem a troca de seção.
  - Botão voltar (hardware): numa seção interna volta para a Visão Geral; só na Visão Geral fecha a viagem e retorna à lista.
  - Botão da barra superior (direita): na Visão Geral é **✕** (fecha a viagem → lista); nas seções internas vira uma **seta de voltar** (ícone Tabler `arrow-left` como `Path`) que retorna à Visão Geral.

- **Visual**: barra superior da viagem em teal (`Accent`), com ícones e título brancos; a área da barra de status do Android também fica teal, com ícones brancos, enquanto a viagem está aberta (`WindowCompat...AppearanceLightStatusBars`). Fundo geral cinza-esverdeado (`AppBackground` `#EEF2F1`), inclusive na lista de viagens e na Visão Geral.

- **Visão Geral**: faixa teal logo abaixo da barra, com o nome e as datas da viagem (a barra não mostra título nessa tela). Abaixo, grade de blocos coloridos com ícone (Tabler outline embutido como `Path` SVG), título e resumo; fundos com saturação moderada para contrastar com o fundo cinza-esverdeado. O primeiro bloco é **Detalhes** (rosa) — abre o editor de metadados; os demais são as seções (Roteiro, Tarefas, Mapa, Gastos, Dicas, Arquivos), cada um com uma cor. O bloco Mapa abre o Google My Maps direto.
  - **Editar metadados** (`TripDetailsEditPage`, modal): nome, datas (início/fim via `DatePicker`), nº de pessoas, moeda base e URL do mapa. Salvo via `TripViewModel.UpdateTripDetailsAsync`, que recalcula tudo que depende desses campos (datas do roteiro, totais, resumos) e dispara `TripUpdated` para atualizar o nome no drawer.

- **Roteiro** (`ItineraryPage`): cada dia é um card (badge Dx + resumo + data) com as atividades em lista vertical ordenada por `StartSlot` (`ActivityRow`). Cada atividade mostra acento colorido (cor da atividade), título e tipo; toque expande os detalhes/notas quando houver (chevron só aparece em atividades com detalhes).
  - **Editar atividade** (`ActivityEditPage`, modal): toque longo abre o editor com título, tipo, **cor** (paleta) e detalhes. Salvo via `TripViewModel.UpdateActivityAsync`; preserva `StartSlot`/`DurationSlots` (não bagunça a linha do tempo do desktop). Adicionar/excluir/reordenar atividades e editar o resumo do dia estão nas pendências.
  - **Rolar até hoje**: ao abrir o Roteiro, se a data de hoje for um dos dias, a lista rola automaticamente até ele (`NumberedDay.Date` + `CollectionView.ScrollTo` no `Loaded`). Se hoje não estiver na viagem, abre no topo normalmente.

- **Permissão de escrita SAF** (corrigido): as edições (Gastos, Detalhes, Roteiro) precisam de permissão de **escrita** persistida na pasta — antes só se pegava leitura, então salvar no `trip.json` falhava silenciosamente. O `SaveRepoUri` agora faz `TakePersistableUriPermission` com **leitura + escrita** e, se o provedor recusar a escrita (provedores somente-leitura), **cai para somente leitura** (a pasta ainda funciona como visualizador). O intent do `FolderPickerService` pede **apenas leitura** de propósito: exigir escrita no seletor faz o DocumentsUI da Samsung **desabilitar o "USAR ESTA PASTA"** em provedores somente-leitura. **Usuários existentes precisam usar "Trocar pasta" uma vez** para reautorizar com escrita.
  - **Instabilidade do seletor no Android 16 (Galaxy S24)**: a lista de locais do seletor SAF (`ACTION_OPEN_DOCUMENT_TREE`) chegou a não abrir, deixando só o armazenamento interno. A causa não era o Drive negar acesso a pastas: depois de resetar o seletor (`pm clear com.google.android.documentsui`) ou de alguma atualização, o **Google Drive voltou a aparecer** e pode ser escolhido pela opção "Armazenamento interno". O **OneDrive segue ausente** do seletor. Por isso o acesso à nuvem usa **APIs nativas** (ver abaixo), com o SAF do Drive como plano B.

- **Armazenamento na nuvem via API (backend plugável)**: o armazenamento foi abstraído em `ITripStorage` (listar viagens, carregar/salvar `trip.json`, abrir/excluir anexos). Implementações:
  - `TripFileService` — SAF/armazenamento interno (o de sempre).
  - `GoogleDriveStorage` — **Google Drive API v3**: varre a pasta de viagens (paralelizado, até 6 simultâneas, com token pré-aquecido), carrega/salva o `trip.json`.
  - `OneDriveStorage` — **Microsoft Graph** (`/me/drive`): mesmo padrão de varredura paralela; localiza o `trip.json` por endereçamento de caminho (`items/{pasta}:/trip.json`) e baixa pelo link pré-autenticado `@microsoft.graph.downloadUrl` (evita o redirecionamento de `/content` para outro host); salva com `PUT .../content`.
  - Os dois backends também implementam abrir/excluir anexo (`OpenAttachmentAsync`/`DeleteAttachmentAsync`), usados pela tela de Arquivos.
  - Auth: base comum `OAuthPkceService` — OAuth 2.0 + PKCE via `WebAuthenticator`, refresh token no `SecureStorage` (a Microsoft rotaciona o refresh token a cada renovação; o novo é salvo). `EnsureSignedInAsync` reaproveita a sessão salva e só abre o login interativo se não houver sessão válida — trocar de provedor não pede login de novo.
    - `GoogleAuthService`: cliente OAuth do tipo **iOS** no Google Cloud (esquema de redirect reverso `com.googleusercontent.apps.<id>`, interceptado no Android; clientes Android não suportam mais esquema personalizado). Em modo **"Testando"** a conta precisa estar na lista de usuários de teste e o Google **expira o acesso em 7 dias** — publicar o app ("Em produção") remove esse prazo, sem exigir verificação (limite de 100 usuários + aviso de app não verificado).
    - `OneDriveAuthService`: app registrado no **Azure (Entra ID)** como cliente público "Aplicativos móveis e da área de trabalho", tipo de conta "qualquer diretório + contas pessoais" (endpoint `common`), redirect `msal<clientId>://auth`, permissões delegadas `Files.ReadWrite` + `offline_access`. Contas pessoais não registram mais apps fora de um diretório: foi preciso criar a conta gratuita do Azure (que cria o diretório).
  - Navegação de pastas genérica: `ICloudFolderBrowser` (implementada pelos dois backends) usada pela `DriveFolderPickerPage`.
  - Seleção de provedor na `TripsPage` (action sheet **Google Drive / OneDrive / Armazenamento interno**). O repositório ativo (provedor + ref + rótulo) é persistido; `TripsViewModel`/`TripViewModel` roteiam scan/load/**save** pelo backend ativo.
  - A **"última viagem"** é vinculada ao repositório atual (`last_trip_repo`): não mostra o atalho se pertence a outro provedor/pasta (evitava tentar abrir uma ref SAF pelo Drive → "acesso perdido").
  - Manifesto: `WebAuthenticationCallbackActivity` com um intent-filter por esquema de redirect (Google e Microsoft) + permissão `INTERNET`.
  - **Uso offline** (`OfflineStore`, em `AppDataDirectory/offline`, que o Android não limpa):
    - Cópia do `trip.json` de cada viagem da nuvem, atualizada na varredura (sem tráfego extra), ao abrir e a cada salvamento. Sem internet, `TripsViewModel.OpenTripAsync` abre pela cópia e marca a viagem como `IsOfflineCopy` (somente leitura).
    - Varredura: nuvem sem internet não varre; mantém a lista salva e mostra "Sem internet: mostrando a lista salva". Falha de token por falta de rede não marca mais "acesso perdido" (`AccessDenied` só com internet).
    - Anexos: item **"Baixar para uso offline"** no menu lateral da viagem (só nuvem) baixa todos os anexos com progresso e mostra o status (ex.: "3 de 7 anexos offline"). Abrir e baixar usam a cópia local quando existe.
    - Edição: `TripViewModel.CanEditAsync()` bloqueia com aviso as edições em viagem da nuvem sem internet ou aberta pela cópia (tarefas, dicas, gastos, detalhes, atividades, excluir anexo). Faixa amarela de "somente leitura" na `TripPage`, atualizada quando a conectividade muda.
    - `SaveAsync` avisa quando o salvamento falha (antes era silencioso).
    - Permissão `ACCESS_NETWORK_STATE` para checar a conectividade.

- **Exportação PDF** (`AndroidPdfExporter` + `CalibriFontResolver`): o QuestPDF usado no desktop **não roda no Android** (recusa runtimes não-suportados, sem binário nativo `QuestPdfSkia` para Android). Por isso o Android reimplementa o mesmo layout com **MigraDoc/PDFsharp** (`PDFsharp-MigraDoc`), que roda no Android, reproduzindo de perto a saída do `TripPdfExporter`: mesmas 6 seções (Roteiro, Roteiro Detalhado por versão em landscape, Dicas, Gastos, Orçamento Detalhado em landscape, Tarefas), cores, tamanhos e o diagrama de slots (tabela com células mescladas).
  - A fonte **Calibri** fica embutida em `UltraViagem.Core/Fonts` (resource) e é fornecida ao PDFsharp via `IFontResolver` (o Android não tem Calibri no sistema) — sem isso a quebra/paginação divergiria.
  - Gatilho: item "Exportar PDF" no drawer da `TripPage` → gera em `FileSystem.CacheDirectory` (`TripViewModel.ExportPdfAsync`) e abre a folha de compartilhamento (`Share`).
  - Observação: por ser outro motor de layout, não é byte-a-byte idêntico ao desktop, mas visualmente equivalente.

- **Arquivos** (`FilesPage`): lista os anexos de `Trip.Attachments` (não varre a pasta). Toque abre o arquivo; toque longo entra em modo de seleção com checkboxes e barra de ação Baixar/Excluir.
  - Abrir/baixar/excluir passam pelo backend ativo (`ITripStorage`), então funcionam no armazenamento local, no Google Drive e no OneDrive. O `FolderUri` da viagem (id da pasta) é capturado no scan e propagado até o `TripViewModel`.
  - Abrir: no SAF abre a URI direto (`BuildSiblingUri`, com fallback `FindSiblingInFolder` para docIds opacos); na nuvem baixa o anexo para `CacheDirectory/attachments/<hash da pasta>/` e abre com `Launcher.OpenAsync(OpenFileRequest)` (o MAUI cuida do FileProvider).
  - Baixar: lê o anexo pelo backend e grava em `Download/UltraViagem/` via `MediaStore`. Excluir apaga o arquivo no provedor (no Drive/OneDrive vai para a lixeira).
  - Sobreposição de progresso ("Abrindo…/Baixando…/Excluindo…") durante as operações, bloqueando toques repetidos.

- **Cache da lista de viagens**: o resultado do scan é persistido em `trips_cache.json` (arquivo privado em `FileSystem.AppDataDirectory`, contendo `repoUri` + entries). Na abertura, a lista do cache é exibida imediatamente e o scan roda em segundo plano, atualizando a lista só se mudou — evita a espera do scan, que é lento com muitas viagens.
  - O cache só é usado se o `repoUri` salvo bate com a pasta selecionada; trocar de pasta o ignora e sobrescreve.
  - Se o acesso for perdido num rescan silencioso, o cache permanece visível e o card de erro aparece (em vez de esvaziar a tela).

- **Gastos** (`ExpensesPage`): lista agrupada por categoria (`CollectionView` com `IsGrouped`), construída em `TripViewModel.BuildExpenseGroups`.
  - Cada grupo tem cabeçalho com ícone Tabler + cor por categoria (`ExpenseCategoryStyle`, com mapa fixo de categorias conhecidas — Hospedagem/Transporte/Passeios/Refeição/Compras — e fallback genérico cinza para categorias livres; normaliza acentos e maiúsculas) e o subtotal da categoria.
  - Itens (`ExpenseRow`) mostram título, status (✓ pago / pendente, inativos esmaecidos) e valor na moeda base. Toque expande o item exibindo Fornecedor, Preço unit. (na moeda do item), Pessoas × Qtd, Câmbio (só quando a moeda ≠ base), Pago, notas e link "Abrir reserva".
  - Cartão-resumo no rodapé: Estimado / Pago / A pagar (este em vermelho) + `ProgressBar` (`PaidFraction`). Totais consideram apenas itens ativos.
  - **Edição** (`ExpenseEditPage`, modal): toque longo no item abre o editor com formulário completo (título, categoria, fornecedor, link, observações, preço unit., taxas, pessoas, quantidade, moeda, câmbio, valor pago e toggle ativo); botão Excluir (com confirmação). FAB "+" cria um gasto novo com defaults da viagem. Persistido via `TripViewModel.AddExpenseAsync` / `UpdateExpenseAsync` / `DeleteExpenseAsync`, que reconstroem grupos e totais e salvam o `trip.json`.
