# Ronu

Projeto de portfólio. App web que gera dietas semanais com IA (Gemini) para quem treina
artes marciais, esportes e condicionamento (26 modalidades), a partir do gasto calórico real de cada modalidade.

## Stack e hospedagem

- **Backend:** ASP.NET Core (.NET 10) + PostgreSQL (EF Core). Código em `src/Ronu.Api`, testes em `tests/Ronu.Api.Tests`.
- **Produção do backend:** Azure App Service F1 (app `ronu-api`, grupo `ronu-rg`) + Azure PostgreSQL Flexible Server.
- **Frontend:** HTML/CSS/JS puro, sem build, em `frontend/`. Hospedado na Vercel com deploy automático da `main`.
- ⚠️ **Push na `main` publica o frontend em produção.**

## Acordo de trabalho (regras rígidas)

- Responder sempre em português (pt-BR).
- O usuário não escreve código: ele decide e testa.
- Nunca editar um arquivo sem antes ler o conteúdo atual.
- Mudança visual nova: rodar `$impeccable shape` seguindo o design system "Dojo Noturno" (`DESIGN.md`, com os modos escuro e Washi) e ter o shape aprovado antes de escrever código. Toda cor, sombra e fonte sai das variáveis de `frontend/css/base/tokens.css`, e cada tela é conferida nos dois modos. O `DESIGN.md` já descreve o redesign de nov/2026 (os protótipos de referência estão listados nele), mas o código ainda está no visual antigo: a migração vai em etapas, registrada em `docs/lacunas-conhecidas.md`.
- Mudança não visual complexa: investigar e apresentar uma proposta antes de escrever código.
- Nunca fazer commit, push ou deploy sem pedido explícito. Todo commit vem só depois do teste manual do usuário.
- Problemas encontrados no caminho devem ser sinalizados, mas não resolvidos sem aprovação.
- Trabalho grande vai em etapas pequenas, cada uma com seu commit.
- **Qualquer acesso ao banco de produção, leitura ou escrita, por qualquer caminho (User Secrets, `az`, psql, script), só com confirmação do usuário pedida antes, a cada vez.** Uma autorização para "fazer uma consulta" não dispensa pedir a confirmação no momento do acesso.
- Toda mudança na fórmula de manutenção (`CalculadoraManutencao`, `CalculadoraGastoCalorico`, `RitmoObjetivo`, METs das modalidades) exige incrementar `CalculadoraManutencao.VersaoFormula`: a meta adaptativa só compara a manutenção de dietas da mesma versão.

## Teste local

1. Confirmar que os User Secrets do `src/Ronu.Api` apontam para o banco **localhost** antes de qualquer teste.
2. Subir a API (`dotnet run --project src/Ronu.Api`), que escuta em `http://localhost:5011`.
3. Servir o frontend pelo Live Server na porta 5500. O `API_BASE` em `frontend/js/auth.js` é escolhido
   sozinho pelo endereço da página: em `localhost` ou `127.0.0.1` usa `http://localhost:5011/api`; em
   qualquer outro endereço, a API de produção. Não há troca manual. O CORS da API libera
   `http://localhost:5500`, `http://127.0.0.1:5500` e a Vercel.
4. Abrir pelo celular via IP da rede (`192.168.x.x`) usa a API de **produção**, não a local: a API local
   não escuta na rede e o CORS não libera essa origem. Para testar mobile contra a API local, usar o
   modo responsivo do navegador no próprio PC.
5. Testes do backend: `dotnet test tests/Ronu.Api.Tests` (não há solution na raiz, então `dotnet test` sozinho falha).
6. Para testar com os cabeçalhos de produção (CSP e os outros do `frontend/vercel.json`), que o Live Server não envia:
   parar o Live Server e rodar `node tools/servidor-teste.mjs` (porta 5500, só em `127.0.0.1`), abrindo
   `http://127.0.0.1:5500/`. Ele lê os cabeçalhos e redirecionamentos do próprio `vercel.json` e só acrescenta
   `http://localhost:5011` ao `connect-src`. Violação de CSP aparece no console do DevTools. Testes do próprio
   servidor: `node --test tools/servidor-teste.test.mjs`.

Verificação automática com jsdom e screenshots headless (desktop e mobile 390 px) é bem-vinda
como complemento, mas nunca substitui o teste manual do usuário.

## Dados de teste

- Usar sempre uma conta descartável.
- Todo e-mail fictício (contas de teste, medições, testes automáticos novos) usa o domínio `@exemplo.test`: o `.test` é reservado e não existe na internet, ao contrário de `@exemplo.com`.
- Existe o `DELETE /api/conta` (LGPD base), que apaga a conta logada e tudo dela, mas a limpeza das contas de teste continua sendo feita direto no banco: dentro de uma transação, filtrando por id **e** email juntos. Antes de apagar, listar o que vai ser apagado.
- A limpeza inclui a tabela `PedidosRedefinicaoSenha` (apaga em cascata junto com o `Usuario`, mas convém listar e apagar explicitamente).
- A limpeza inclui a tabela `MetasDieta`. Ela apaga em cascata junto com o `Usuario`, mas não tem chave estrangeira para a `DietaIA` (de propósito: guarda todas as dietas, não só as 3 da tela). Ao apagar só as dietas de alguém, apagar também as `MetasDieta` do mesmo usuário, senão a meta adaptativa continua lendo essas metas.
- A limpeza inclui a tabela `ListaComprasMarcacoes` (o "já tenho" da lista de compras). Ela apaga em cascata junto com o `Usuario` e com a `DietaIA`, mas convém listar e apagar explicitamente, antes das `DietasIA`.
- A limpeza inclui a tabela `AlergiasUsuario` (os chips de alergia, dado de saúde) e os `Consentimentos` do tipo `alergias`. As duas apagam em cascata junto com o `Usuario`, mas convém listar e apagar explicitamente.
- A limpeza inclui o plano família, antes do `Usuario` e listando antes, nesta ordem: `ConvitesCasa` (os gerados pela conta de teste e os das casas que vão ser apagadas), `PedidosCasa` (os das casas que vão ser apagadas: não têm `UsuarioId` nem autor, filtrar pelo `CasaId`), `ListaCasaMarcacoes` (o "já tenho" da lista da casa, das casas que vão ser apagadas: também sem `UsuarioId` nem autor, filtrar pelo `CasaId`; antes das `SemanasCasa`, de que depende), `SemanasCasa` (as das casas que vão ser apagadas: não têm `UsuarioId`, filtrar pelo `CasaId`), `MembrosCasa` (as linhas da conta de teste), `Casas` (só as casas cujos membros são todos contas de teste: se sobrar membro de verdade numa casa, parar e avisar) e os `Consentimentos` do tipo `casa`. Tudo apaga em cascata junto com o `Usuario` ou com a `Casa`, mas convém listar e apagar explicitamente. As `DietasIA` com `Origem = casa` são da pessoa (saem com as outras dietas dela); o `SemanaCasaId` delas não tem chave estrangeira.

## Migrations em produção

A connection string de produção **nunca é impressa nem gravada em arquivo** (nem nos User Secrets, nem em
script, log ou saída de comando). Ela é obtida pelo `az` direto numa variável de ambiente da sessão e
passada ao `dotnet ef` por essa variável (`ConnectionStrings__DefaultConnection`), que é descartada no fim.

O firewall do Postgres (`ronu-db`) não libera o IP local, e esse IP muda. Todo acesso daqui ao banco de produção
(migration ou consulta) abre uma **regra temporária**, só para o IP atual, e a apaga **na mesma janela**, mesmo
se algo falhar no meio.

1. Só com confirmação do usuário, pedida no momento: carregar a connection string pelo `az` numa variável de ambiente da sessão.
2. Descobrir o IP público atual e criar a regra temporária pelo `az`, com um nome que diga que é temporária (ex.:
   `az postgres flexible-server firewall-rule create --resource-group ronu-rg --server-name ronu-db --name temp-migration-AAAAMMDD-HHMM --start-ip-address <ip> --end-ip-address <ip>`).
   No PowerShell 5.1, SQL passado ao `psql` por `-c` perde as aspas duplas: passar sempre por arquivo (`-f`).
3. Rodar `dotnet ef migrations list` para conferir o que está pendente. Se houver algo além do esperado, parar (e ir direto ao passo 5).
4. Aplicar a migration (e as consultas de checagem que a etapa pedir, antes dela).
5. **Apagar a regra temporária** (`az postgres flexible-server firewall-rule delete --resource-group ronu-rg --server-name ronu-db --name <regra> --yes`) e listar as regras (`firewall-rule list --resource-group ronu-rg --server-name ronu-db`) para conferir que ela sumiu.
6. Descartar a variável e conferir que nenhum arquivo ficou com a connection string.

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

- `docs/`: `design-api.md`, `lacunas-conhecidas.md` (status de features e pendências ficam **só** aqui), `user-stories.md`, `diagrama-classes.md`, `fluxogramas.md`, DER e fluxos em PNG, `seed-modalidades.sql`, `fontes-nutricionais.md` (fontes, licenças e método da base nutricional própria e da conversão para a lista de compras), `compras/` (linhas da TACO e do USDA usadas na lista de compras, com o script de extração), `icones-3d.md` (onde fica cada ícone 3D e ilustração de `frontend/img/`, o prompt do Figma para gerar um novo, o recorte com `tools/recorta-icone-3d.py` e as camadas do envelope).
- `DESIGN.md`: design system "Dojo Noturno", com os modos escuro e Washi, já no visual do redesign de nov/2026 (com a tabela de migração dos tokens).
- `PRODUCT.md`: contexto de produto.
