# Todo / Roadmap

## Prioridade Alta

- Implementar tela completa de roteiro. Inclui as funcionalidades:

    - banco de atividades
    - versões de roteiro
- Implementar o card de roteiro na tela principal.
- Criar exportação para PDF.

## Prioridade Média

- Importação XLSX da planilha de referência.
- Definir estratégia de migração de schema para `trip.json`.
- Importar arquivos da pasta ao abrir viagem

## Prioridade Baixa
- Publicação empacotada do app Windows.

## Futuro Android/Web

- Separar regras de domínio em biblioteca compartilhável.
- Definir contrato estável do `trip.json`.
- Avaliar MAUI, Avalonia, React Native ou web app local-first.
- Definir sincronização/conflito para uso em múltiplos dispositivos.

## Dívidas Técnicas

- `MainWindow.xaml.cs` concentra navegação e comandos demais.
- `AppViewModel.cs` concentra muitas coleções e conversões.
- Falta suite de testes para serialização e regras de persistência.
- Falta validação formal do JSON.
- Falta documentação de schema com exemplos completos.
