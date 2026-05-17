# Todo / Roadmap

## Prioridade Alta

- Melhorar acabamento visual dos cards da visão geral.
- Implementar tela completa de roteiro.
- Implementar tela completa de gastos.
- Implementar tela completa de mapa/lugares.
- Padronizar navegação para todos os painéis.
- Adicionar validações de URL em dicas e Google My Maps.
- Definir estratégia de migração de schema para `trip.json`.

## Prioridade Média

- Abrir links de dicas diretamente pelo clique na visão geral.
- Associar dicas a dias do roteiro ou lugares.
- Associar tarefas a roteiro, gasto, lugar ou arquivo.
- Melhorar validações de renomeação de arquivos.
- Exportar KML para Google My Maps.
- Melhorar seleção de ícones e estados de hover/foco/acessibilidade.
- Criar dados de exemplo dentro de uma pasta de viagens dedicada.

## Prioridade Baixa

- Importação XLSX da planilha de referência.
- Favoritos com ordenação manual.
- Marcar dicas como `ver depois`, `útil` ou `descartada`.
- Busca global dentro da viagem.
- Tema claro/escuro.
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
