# Checklist De Commit

Antes de todo commit:

1. Revise se a mudança altera estrutura, arquitetura, funcionalidade, modelo de dados, fluxo de uso ou prioridades.
2. Se alterar, atualize `README.md` e/ou os arquivos em `docs/`.
3. Rode build quando a mudança tocar código:

```powershell
dotnet build .\UltraViagem.slnx
```

4. Confira o status:

```powershell
git status --short
```

5. Evite commitar:

- `bin/`
- `obj/`
- screenshots temporários;
- arquivos de usuário;
- dados pessoais reais de viagens.

6. Faça commit local com mensagem objetiva em português.

Antes de push:

1. Gere o publish atualizado:

```powershell
dotnet publish .\UltraViagem.App\UltraViagem.App.csproj -c Release -o .\publish
```

2. Confira o status:

```powershell
git status --short
```

3. Se houver mudanças pendentes, faça commit incluindo `publish/`.
4. Faça push para `origin`.

Regra do projeto: documentação deve acompanhar a mudança no mesmo commit sempre que a mudança afetar o entendimento futuro do projeto.

Veja tambem o fluxo completo em `docs/WORKFLOW.md`.
