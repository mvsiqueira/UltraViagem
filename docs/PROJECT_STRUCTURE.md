# Estrutura Do Projeto

## Raiz

```text
UltraViagem/
  .gitignore
  README.md
  UltraViagem.slnx
  config.json
  Viagem Carretera Austral.xlsx
  docs/
  scripts/
  UltraViagem.Core/
  UltraViagem.App/
```

## Arquivos Da Raiz

- `.gitignore`: ignora `bin/`, `obj/`, saídas locais auxiliares e arquivos temporários. A pasta `publish/` é versionada como build publicado do app.
- `README.md`: entrada principal para retomar o projeto.
- `UltraViagem.slnx`: solução .NET.
- `config.json`: exemplo de configuração do repositório de viagens.
- `Viagem Carretera Austral.xlsx`: planilha de referência usada como exemplo funcional.

## Scripts De Importação

Scripts Python para converter planilhas de viagem em `trip.json`. Executar a partir da raiz do projeto:

```bash
python scripts/import_chapada.py
```

```text
scripts/
  import_utils.py          ← utilitários compartilhados (openpyxl: build_merge_map, make_activity_fn, cell_color)
  import_<viagem>.py       ← um script por viagem (.xlsx via openpyxl, .xls via xlrd — standalone)
  inspect_*.py             ← scripts de inspeção de planilhas (uso pontual)
```

- `import_utils.py` é importado diretamente pelos scripts `.xlsx`; scripts `.xls` são standalone.
- Todos os scripts gravam o `trip.json` dentro da pasta de viagem no Google Drive.

## UltraViagem.Core

Projeto de domínio e persistência. Não depende de WPF.

```text
UltraViagem.Core/
  AppConfig.cs
  Trip.cs
  TripRepository.cs
  UltraViagem.Core.csproj
```

- `AppConfig.cs`: configuração da raiz de viagens, moeda padrão, viagens recentes e favoritas.
- `Trip.cs`: modelo de dados principal da viagem.
- `TripRepository.cs`: leitura e gravação de `config.json` e `trip.json`.

## UltraViagem.App

Projeto Windows/WPF.

```text
UltraViagem.App/
  App.xaml
  App.xaml.cs
  AppViewModel.cs
  AboutWindow.xaml
  AboutWindow.xaml.cs
  MainWindow.xaml
  MainWindow.xaml.cs
  TripDetailsWindow.xaml
  TripDetailsWindow.xaml.cs
  TripSelectionWindow.xaml
  TripSelectionWindow.xaml.cs
  Assets/
```

- `App.xaml`: estilos globais, paleta, templates de botões, navegação e scrollbars.
- `AppViewModel.cs`: estado da UI, coleções editáveis e conversão entre modelo e tela.
- `MainWindow.xaml`: layout principal, sidebar, visão geral, tarefas, dicas, mapa e arquivos.
- `MainWindow.xaml.cs`: navegação, comandos, salvamento, WebView2 do mapa e integração com o repositório.
- `AboutWindow.*`: janela `Sobre`, com descrição, autor, versão e data do build.
- `TripDetailsWindow.*`: modal usado no fluxo de criação de nova viagem.
- `TripSelectionWindow.*`: modal de seleção de viagem com favoritos.
- `Assets/Menu`: ícones PNG do menu lateral.
- `Assets/AppIcon`: ícone do aplicativo.

## Dados De Viagem

Cada viagem deve ficar em uma subpasta, contendo `trip.json` e seus anexos:

```text
2026-03-carretera-austral/
  trip.json
  passagem-aerea.pdf
  reserva-hotel-1.pdf
  roteiro.kml
```

O app deve evitar espalhar dados da viagem em múltiplos JSONs. A regra atual é: uma viagem, uma pasta no formato `yyyy-mm-nome-da-viagem`, um `trip.json`.
