# UltraViagem

UltraViagem é um app Windows local-first para planejar viagens. Cada viagem fica em uma pasta própria dentro de um repositório de arquivos escolhido pelo usuário, com um `trip.json` e os anexos da viagem no mesmo diretório.

O objetivo é permitir que a pasta raiz fique em Google Drive, OneDrive ou outro serviço de sincronização, mantendo os dados fáceis de copiar, versionar e acessar de outros computadores.

## Estado Atual

- App desktop Windows em WPF.
- Persistência local em JSON.
- Seleção de repositório local.
- Seleção de viagem por janela modal, com favoritos.
- Edição de dados gerais da viagem.
- Visão geral com roteiro, tarefas, orçamento, mapa, dicas e arquivos.
- Tela de tarefas com status `pendente` e `concluída`.
- Tela de dicas com tabela simples de `nome` e `link`.

## Como Rodar

Pré-requisitos:

- Windows.
- .NET SDK compatível com `net10.0-windows`.

Comandos:

```powershell
dotnet build .\UltraViagem.slnx
dotnet run --project .\UltraViagem.App\UltraViagem.App.csproj
```

O app abre maximizado por padrão.

## Estrutura Da Pasta De Viagens

Exemplo da pasta de dados escolhida no app:

```text
UltraViagens/
  config.json
  2025-carretera-austral/
    trip.json
    nova-york.kml
    reserva-hotel-1.pdf
    passagem-aerea.pdf
    mapa-do-metro.png
```

## Documentação

- [Estrutura do projeto](docs/PROJECT_STRUCTURE.md)
- [Arquitetura](docs/ARCHITECTURE.md)
- [Funcionalidades](docs/FUNCTIONAL.md)
- [Todo / Roadmap](docs/TODO.md)
- [Checklist de commit](docs/COMMIT_CHECKLIST.md)

## Regra De Continuidade

Antes de todo commit, revise se a documentação precisa ser atualizada. Se a mudança altera estrutura, arquitetura, comportamento funcional, modelo de dados, fluxo de uso ou prioridades, atualize os arquivos em `docs/` no mesmo commit.
