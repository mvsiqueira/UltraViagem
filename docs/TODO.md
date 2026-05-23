# Todo / Roadmap

## Prioridade Alta

- Publicação empacotada do app Windows.

## Prioridade Média

- Importação de pasta.
- Importação XLSX da planilha de referência.
- PDF: melhorar layout do Roteiro Detalhado quando há muitos dias (texto cortado em blocos estreitos).

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

## Entregue

- ✅ Exportação PDF completa (QuestPDF): Roteiro, Roteiro Detalhado landscape, Dicas, Gastos, Orçamento Detalhado landscape, Tarefas.
- ✅ Fix: imagens do roteiro recarregadas ao trocar versão (`SwitchToVersion`).
- ✅ Fix: mapa sempre recarregado ao salvar URL.
- ✅ Ocultar header nativo do Google My Maps via CSS injection.
