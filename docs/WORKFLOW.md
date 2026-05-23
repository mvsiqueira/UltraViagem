# Fluxo De Trabalho

Este documento registra os combinados operacionais do projeto.

## Build E Execucao

- Durante desenvolvimento, use build normal em Debug:

```powershell
dotnet build .\UltraViagem.slnx
```

- Nao execute o app automaticamente apos build; o teste manual fica por conta do usuario.

- Se o build, publish ou execucao falhar porque o UltraViagem esta aberto e travando arquivos, pode fechar o processo do app e repetir o comando.

## Publish

- A pasta padrao para builds publicados e `publish/`.
- Nao publique a cada build de desenvolvimento.
- Gere `publish/` quando for pedido push, build publicado, release ou versao final para teste.
- Nao gere `publish/` quando for pedido apenas commit.

Comando padrao (single-file, self-contained, win-x64):

```powershell
dotnet publish .\UltraViagem.App\UltraViagem.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

O resultado e um unico `UltraViagem.exe` em `publish/`. Na primeira execucao, DLLs nativas (WebView2, QuestPDF) sao extraidas automaticamente para `%TEMP%` pelo runtime do .NET.

> **Nota:** `publish/UltraViagem.exe` nao e commitado no repositorio (excede 100 MB, limite do GitHub). O executavel fica apenas localmente apos o publish.

## Commit

- Quando for pedido commit, faca apenas o commit local.
- Antes de commitar:
  - confira o status do Git;
  - inclua as mudancas de codigo e documentacao pertinentes.
- Nao faca push automaticamente quando o pedido for apenas commit.

## Push

- Quando for pedido push:
  - gere o publish atualizado em `publish/`;
  - confira o status do Git;
  - inclua as mudancas de codigo, documentacao e `publish/` em um commit, se houver mudancas pendentes;
  - faca push para o remote `origin`.

## Remote

- O remote principal do projeto e:

```text
https://github.com/mvsiqueira/UltraViagem.git
```
