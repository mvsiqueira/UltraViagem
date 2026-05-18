# Fluxo De Trabalho

Este documento registra os combinados operacionais do projeto.

## Build E Execucao

- Durante desenvolvimento, use build normal em Debug:

```powershell
dotnet build .\UltraViagem.slnx
```

- Apos um build bem-sucedido, execute o app Debug para teste:

```powershell
.\UltraViagem.App\bin\Debug\net10.0-windows\UltraViagem.App.exe
```

- Se o build, publish ou execucao falhar porque o UltraViagem esta aberto e travando arquivos, pode fechar o processo do app e repetir o comando.

## Publish

- A pasta padrao para builds publicados e `publish/`.
- Nao publique a cada build de desenvolvimento.
- Gere `publish/` sempre antes de um commit solicitado.
- Tambem gere `publish/` quando for pedido explicitamente um build publicado, release ou versao final para teste.

Comando padrao:

```powershell
dotnet publish .\UltraViagem.App\UltraViagem.App.csproj -c Release -o .\publish
```

## Commit E Push

- Quando for pedido um commit, antes de commitar:
  - gere o publish atualizado em `publish/`;
  - confira o status do Git;
  - inclua as mudancas de codigo, documentacao e `publish/` no commit.
- Sempre que fizer commit, faca tambem push para o remote `origin`.

## Remote

- O remote principal do projeto e:

```text
https://github.com/mvsiqueira/UltraViagem.git
```
