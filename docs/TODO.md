# Todo / Roadmap

## Prioridade Alta

- Importação de viagens antigas.

## Prioridade Baixa

- App Android (`UltraViagem.Android` — MAUI, fase 1 viewer)

   - Viewer básico implementado: navegação Shell → TripsPage → TripPage (tabs).
   - Bugs de build corrigidos: bindings compilados em `ItineraryPage` ajustados via `NumberedDay` wrapper.
   - Abrir recentes via `File.OpenRead` pode falhar em Android 10+ se o cache do picker foi limpo — usar URI persistida futuramente.
   - Próximos passos: separar Core em biblioteca compartilhada, definir contrato estável do `trip.json`, avaliar sincronização multi-dispositivo.

## Dívidas Técnicas

- `MainWindow.xaml.cs` concentra navegação e comandos demais.
- `AppViewModel.cs` concentra muitas coleções e conversões.
- Falta suite de testes para serialização e regras de persistência.
