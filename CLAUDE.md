# Ronu

Projeto de portfólio. App web que gera dietas semanais com IA (Gemini) para quem treina
artes marciais e esportes de combate, a partir do gasto calórico real de cada modalidade.

## Stack e hospedagem

- **Backend:** ASP.NET Core (.NET 10) + PostgreSQL (EF Core). Código em `src/Ronu.Api`, testes em `tests/Ronu.Api.Tests`.
- **Produção do backend:** Azure App Service F1 (app `ronu-api`, grupo `ronu-rg`) + Azure PostgreSQL Flexible Server.
- **Frontend:** HTML/CSS/JS puro, sem build, em `frontend/`. Hospedado na Vercel com deploy automático da `main`.
- ⚠️ **Push na `main` publica o frontend em produção.**

## Acordo de trabalho (regras rígidas)

- Responder sempre em português (pt-BR).
- O usuário não escreve código: ele decide e testa.
- Nunca editar um arquivo sem antes ler o conteúdo atual.
- Mudança visual nova: rodar `$impeccable shape` seguindo o design system "Súmula do Tatame" (`DESIGN.md`) e ter o shape aprovado antes de escrever código.
- Mudança não visual complexa: investigar e apresentar uma proposta antes de escrever código.
- Nunca fazer commit, push ou deploy sem pedido explícito. Todo commit vem só depois do teste manual do usuário.
- Problemas encontrados no caminho devem ser sinalizados, mas não resolvidos sem aprovação.
- Trabalho grande vai em etapas pequenas, cada uma com seu commit.

## Teste local

1. Confirmar que os User Secrets do `src/Ronu.Api` apontam para o banco **localhost** antes de qualquer teste.
2. Subir a API (`dotnet run --project src/Ronu.Api`), que escuta em `http://localhost:5011`.
3. Trocar temporariamente o `API_BASE` em `frontend/js/auth.js` para `http://localhost:5011/api`. **Nunca commitar essa troca.**
4. Servir o frontend pelo Live Server na porta 5500. O CORS da API só libera a 5500 e a Vercel.
5. Testes do backend: `dotnet test`.

Verificação automática com jsdom e screenshots headless (desktop e mobile 390 px) é bem-vinda
como complemento, mas nunca substitui o teste manual do usuário.

## Dados de teste

- Usar sempre uma conta descartável.
- Não existe endpoint de exclusão: a limpeza é feita direto no banco, dentro de uma transação, filtrando por id **e** email juntos. Antes de apagar, listar o que vai ser apagado.

## Migrations em produção

1. Só com confirmação do usuário, trocar os User Secrets para o banco do Azure.
2. Rodar `dotnet ef migrations list` para conferir o que está pendente.
3. Aplicar a migration.
4. Voltar os User Secrets para o localhost.

A migration vai sempre **antes** do deploy do backend que depende dela.

## Deploy do backend (só com autorização explícita, a cada vez)

1. `dotnet publish src/Ronu.Api -c Release -o <pasta de publish limpa>`.
2. Zipar com o .NET `System.IO.Compression.ZipArchive` (PowerShell), com o **conteúdo** da pasta de publish na raiz do zip. **Nunca** usar `Compress-Archive`: ele pode gravar caminhos com `\`, que quebram no App Service Linux.
3. Validar o zip: listar as entradas e confirmar que nenhuma contém `\`. Se alguma contiver, parar e mostrar ao usuário.
4. Chamar o `az` pelo caminho completo, porque ele não está no PATH: `C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd` ou `C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd`. Comando: `az webapp deploy --resource-group ronu-rg --name ronu-api --src-path <caminho completo do zip> --type zip`. Se pedir login ou a sessão tiver expirado, **parar e avisar**: o login é feito pelo usuário. Se o `az` não estiver em nenhum dos dois caminhos, parar e dizer onde procurou.
5. Se aparecer "Site failed to start", **não tentar de novo**: buscar o log do App Service e mostrar.
6. Mostrar a saída completa de cada passo.

## Ordem de deploy

Migration (se houver) → backend no Azure → push do frontend que depende dele.

## Documentação

- `docs/`: `design-api.md`, `lacunas-conhecidas.md` (status de features e pendências ficam **só** aqui), `user-stories.md`, `diagrama-classes.md`, `fluxogramas.md`, DER e fluxos em PNG, `seed-modalidades.sql`.
- `DESIGN.md`: design system "Súmula do Tatame".
- `PRODUCT.md`: contexto de produto.
