# Todo / Roadmap

## Prioridade Alta

- Melhorar acabamento visual dos cards da visão geral.
- Implementar tela completa de roteiro.
- Definir estratégia de migração de schema para `trip.json`.
- Melhorar validações e listas de categorias/moedas na tela de gastos.

## Prioridade Média

- Importação XLSX da planilha de referência.
- Criar exportação para PDF.
- Criar funcionalidade de cópia de viagem
- Melhorar validações de renomeação de arquivos.
- Melhorar seleção de ícones e estados de hover/foco/acessibilidade.
- Publicação empacotada do app Windows.

## Prioridade Baixa

- Associar dicas a dias do roteiro ou lugares.
- Associar tarefas a roteiro, gasto, lugar ou arquivo.
- Busca global dentro da viagem.
- Tema claro/escuro.
- Criar dados de exemplo dentro de uma pasta de viagens dedicada.

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
