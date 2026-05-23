# UltraViagem — Contexto para Claude Code

## O que é

Aplicativo **Windows desktop (WPF/.NET 10)** para planejamento de viagens. Filosofia **local-first**: todos os dados ficam em pastas escolhidas pelo usuário (Google Drive, OneDrive etc.) como JSON + arquivos anexos — sem backend, sem banco de dados.

## Stack

| Camada | Tecnologia |
|--------|-----------|
| Linguagem | C# 12, .NET 10.0 (net10.0-windows) |
| UI | WPF (XAML + code-behind) |
| Mapa | Microsoft WebView2 (Chromium embutido) |
| Serialização | System.Text.Json (camelCase, ignora nulos) |
| Build | dotnet publish, self-contained, win-x64 |

## Arquitetura

```
UltraViagem.Core/     ← modelos de domínio + persistência (sem dependência de UI)
  Trip.cs             ← todos os data models (Trip, TaskItem, ExpenseItem, etc.)
  TripRepository.cs   ← leitura/escrita de JSON no sistema de arquivos
  AppConfig.cs        ← config do app (viagens recentes, favoritos, caminho do repo)

UltraViagem.App/      ← camada WPF (MVVM)
  AppViewModel.cs     ← estado central observável (~1400 linhas)
  MainWindow.xaml     ← janela principal, navegação lateral, todos os painéis
  MainWindow.xaml.cs  ← lógica de seleção de repo, troca de viagem, drag-drop
  TripSelectionWindow.* ← modal de seleção/criação de viagem
  TripDetailsWindow.* ← modal de edição de metadados da viagem
```

**Padrão MVVM:** `NotifyObject` base com `INotifyPropertyChanged`. `ObservableCollection` para binding. Auto-save via eventos de coleção.

## Painéis da janela principal

| Painel | Função |
|--------|--------|
| Overview | Cards resumo (itinerário, tarefas, orçamento, mapa, dicas, arquivos) |
| Trip Details | Editar nome, datas, pessoas, moeda base |
| Tasks | Lista de tarefas com status (pending/done), notas inline, filtro |
| Tips | Links úteis com título + URL; edição inline |
| Map | URL de Google My Maps embed; renderiza via WebView2 |
| Files | Anexar arquivos; drag-drop para reordenar; renomear (F2); ícones do Windows Shell |
| Budget | Abas: Itens / Resumo / Cotações; conversão de moedas via AwesomeAPI |

## Persistência

```
<repo-path>/
  config.json                        ← AppConfig (favoritos, recentes)
  <yyyy-mm-nome-da-viagem>/
    trip.json                        ← Trip completa
    *.pdf, *.png, ...                ← arquivos anexados
```

## Convenções importantes

- **Toda alteração de código deve atualizar os docs em `docs/` no mesmo commit**
- Workflow de build/publish em `docs/WORKFLOW.md`
- Checklist de commit em `docs/COMMIT_CHECKLIST.md`
- Roadmap e dívida técnica em `docs/TODO.md`

## Dívida técnica conhecida

- `MainWindow.xaml.cs` concentra lógica de navegação demais — refatorar para commands
- `AppViewModel.cs` está crescendo — considerar split por domínio
- Falta testes unitários para serialização e persistência
- Sem validação formal de schema JSON

## Docs de referência

- `docs/ARCHITECTURE.md` — design em camadas, estratégia de persistência
- `docs/FUNCTIONAL.md` — especificação funcional completa por tela
- `docs/PROJECT_STRUCTURE.md` — layout de diretórios
- `docs/WORKFLOW.md` — build, publish, commit, push (em português)
- `docs/TODO.md` — roadmap priorizado
