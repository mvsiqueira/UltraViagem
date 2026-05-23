# Todo / Roadmap

## Prioridade Alta

- Criar exportação para PDF.
- Publicação empacotada do app Windows.

## Prioridade Média

- Importação de pasta.
- Importação XLSX da planilha de referência.

## Prioridade Baixa
- App Android/Web

   - Separar regras de domínio em biblioteca compartilhável.
   - Definir contrato estável do `trip.json`.
   - Avaliar MAUI, Avalonia, React Native ou web app local-first.
   - Definir sincronização/conflito para uso em múltiplos dispositivos.

## Dívidas Técnicas

- `MainWindow.xaml.cs` concentra navegação e comandos demais.
- `AppViewModel.cs` concentra muitas coleções e conversões.
- Falta suite de testes para serialização e regras de persistência.
