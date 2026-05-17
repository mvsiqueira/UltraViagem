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

6. Faça commit com mensagem objetiva em português.

Regra do projeto: documentação deve acompanhar a mudança no mesmo commit sempre que a mudança afetar o entendimento futuro do projeto.
