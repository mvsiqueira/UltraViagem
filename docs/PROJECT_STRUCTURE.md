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
  UltraViagem.Core/
  UltraViagem.App/
```

## Arquivos Da Raiz

- `.gitignore`: ignora `bin/`, `obj/`, saídas locais de build/publicação e arquivos temporários.
- `README.md`: entrada principal para retomar o projeto.
- `UltraViagem.slnx`: solução .NET.
- `config.json`: exemplo de configuração do repositório de viagens.
- `Viagem Carretera Austral.xlsx`: planilha de referência usada como exemplo funcional.

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
- `TripDetailsWindow.*`: modal de criação/edição dos dados da viagem.
- `TripSelectionWindow.*`: modal de seleção de viagem com favoritos.
- `Assets/Menu`: ícones PNG do menu lateral.
- `Assets/AppIcon`: ícone do aplicativo.

## Dados De Viagem

Cada viagem deve ficar em uma subpasta, contendo `trip.json` e seus anexos:

```text
2025-carretera-austral/
  trip.json
  passagem-aerea.pdf
  reserva-hotel-1.pdf
  roteiro.kml
```

O app deve evitar espalhar dados da viagem em múltiplos JSONs. A regra atual é: uma viagem, uma pasta, um `trip.json`.
