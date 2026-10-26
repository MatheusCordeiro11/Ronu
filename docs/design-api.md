# Design da API — Ronu (MVP)

Lista de endpoints da API, definidos a partir das user stories (`docs/user-stories.md`) e do modelo de dados (`docs/der.png` / `docs/diagrama-classes.md`).

**Convenção geral:** todas as rotas exigem autenticação via token JWT (header `Authorization: Bearer <token>`), exceto `POST /api/auth/cadastro`, `POST /api/auth/login`, `POST /api/auth/google`, as três rotas de redefinição de senha (`POST /api/auth/esqueci-senha`, `POST /api/auth/redefinir-senha/verificar` e `POST /api/auth/redefinir-senha`) e `GET /api/modalidades`. O `usuarioId` nunca é enviado pelo cliente — é sempre extraído do token, para evitar que um usuário manipule dados de outra conta. Sem token (ou com token expirado), a resposta é **401** — também quando o token foi emitido antes da última troca de senha da conta (claim `iat` contra `Usuario.SenhaAlteradaEm`; tokens sem `iat`, anteriores a essa regra, valem até expirar) ou quando a conta não existe mais.

**Erros:** todos os erros vêm com corpo `{ "mensagem": "<texto para o usuário>" }`, em português. Isso vale também para os da validação automática do modelo (**400**): campo obrigatório vazio ou nulo, `[MinLength]`, `[Range]` devolvem a **primeira** mensagem de erro, escrita no atributo do DTO (ex.: `{ "mensagem": "A senha deve ter pelo menos 8 caracteres." }`). Erros que não vêm de um atributo — JSON malformado ou com tipo errado, campo obrigatório ausente do JSON, corpo vazio, parâmetro de rota inválido — devolvem a genérica `{ "mensagem": "Requisição inválida. Confira os dados enviados." }`. Nenhuma mensagem padrão do ASP.NET (em inglês) chega ao cliente (`Validacao/RespostaValidacao.cs`). Exceções não tratadas viram **500** com `{ "mensagem" }` genérica em produção.

---

## Autenticação

**Email:** sempre gravado e comparado normalizado — sem espaços nas pontas e em minúsculas (`Services/NormalizacaoEmail.cs`) —, no cadastro, no login, no login com Google e na redefinição de senha. Há um índice único em `Usuarios.Email`: uma conta por email.

**Senha:** as mesmas regras no cadastro e na redefinição (`Validacao/RegrasSenha.cs`): de 8 a 64 caracteres, e no máximo 72 bytes (o limite do BCrypt; acentos e símbolos ocupam mais de 1 byte).

### `POST /api/auth/cadastro`
**Recebe:** `nome`, `email`, `senha` (8 a 64 caracteres), `estado` (UF)
**Devolve:** `id`, `nome`, `email` (normalizado; a senha nunca é retornada, mesmo em hash)
**Erros:** 409 se o email já está cadastrado (sem diferenciar maiúsculas); 400 se a senha for curta ou longa demais ou se algum campo vier vazio.

### `POST /api/auth/login`
**Recebe:** `email` (sem diferenciar maiúsculas), `senha`
**Devolve:** `token` (JWT, válido por 2 horas, com a claim `iat`), `usuario { id, nome }`, `precisaInformarEstado` (true quando a conta ainda não tem UF — contas via Google ou anteriores ao campo)
**Erros:** 401 com "Email ou senha inválidos." ou, se a conta foi criada pelo Google e não tem senha, com orientação para entrar com o Google.

### `POST /api/auth/google`
**Recebe:** `idToken` (o credential devolvido pelo botão do Google)
**Devolve:** o mesmo formato do `POST /api/auth/login`
*(a API valida a assinatura do token junto ao Google antes de usar qualquer dado dele. Se não existe conta com o email, cria uma sem senha; se existe, vincula o login com Google a ela. Por isso exige o email verificado pelo Google — `EmailVerified`)*
**Erros:** 401 se o token do Google for inválido ou se o email da conta Google não estiver verificado.

### `POST /api/auth/esqueci-senha`
**Recebe:** `email`
**Devolve:** **202** `{ mensagem }`, sempre com o mesmo texto — exista ou não a conta, tenha ela senha ou só login com Google, e também acima do limite por conta. Quem decide o que acontece é o `ServicoRedefinicaoSenha`:
- conta com senha: email com o link `{Frontend:UrlBase}/redefinir-senha.html#token=…`, válido por **30 minutos** e de uso único. O token vai no **fragmento** (`#`), que não chega a servidor nenhum nem vai no cabeçalho Referer; o banco guarda só o SHA-256 dele (tabela `PedidosRedefinicaoSenha`). Um pedido novo invalida os links anteriores;
- conta só com Google: email avisando que a conta usa o login com Google, sem link;
- email sem conta: nada é enviado nem gravado.

O email sai em segundo plano (a resposta não espera o SMTP). Limite por conta: 3 pedidos por hora e 10 por dia (acima disso, nada é enviado, mas a resposta não muda).
**Erros:** 400 se o email vier vazio; **429** `{ mensagem }` acima de 10 pedidos por hora do mesmo IP.

### `POST /api/auth/redefinir-senha/verificar`
**Recebe:** `token` (o do fragmento do link; no corpo, nunca na URL da API)
**Devolve:** 200 `{ mensagem, email }` se o link ainda vale — para a tela mostrar "link expirado" antes de a pessoa digitar a senha nova. O `email` é o da conta: a tela o coloca num campo escondido (`autocomplete="username"`) para o gerenciador de senhas do navegador associar a senha nova à conta certa. Só quem tem o token, que foi enviado para esse mesmo email, chega a essa resposta.
**Erros:** 400 `{ mensagem, motivo }`, com `motivo` = `invalido` (não existe, ou foi substituído por um pedido mais novo), `expirado` ou `usado`; **429** acima de 20 tentativas por hora do mesmo IP (somadas com o `POST /api/auth/redefinir-senha`).

### `POST /api/auth/redefinir-senha`
**Recebe:** `token`, `novaSenha` (8 a 64 caracteres)
**Devolve:** 200 `{ mensagem }`. A senha é trocada, o link fica usado, os outros links da conta são invalidados e **todos os tokens JWT emitidos antes da troca deixam de valer** (401). Não faz login automático: a pessoa entra com a senha nova.
**Erros:** 400 `{ mensagem, motivo }` como no `verificar`; 400 `{ mensagem }` se a senha nova for curta ou longa demais; **429** como no `verificar`.

---

## Perfil (dados pessoais)

### `GET /api/perfil`
**Devolve:** `altura` (cm), `sexo`, `dataNascimento`, `rotinaDiaria`, `orcamentoSemanal`, `nivelAtividade` — todos podem vir nulos enquanto o onboarding não os preencheu — e os dados da conta: `nome`, `email`, `estado` (UF, `null` se ainda não informado), `temSenha` e `temGoogle` (se a conta tem senha própria e se tem login com Google vinculado; a senha e o hash nunca vêm)

### `PUT /api/perfil`
**Recebe:** `altura` (cm, de 100 a 250), `sexo` (`Masculino` ou `Feminino`), `dataNascimento` (idade de 18 a 100 anos) (obrigatórios); `rotinaDiaria`, `orcamentoSemanal` e `nivelAtividade` (opcionais; `orcamentoSemanal` aceita `economico`, `moderado` ou `sem_restricao`; `nivelAtividade` aceita `sentado`, `em_pe`, `andando` ou `bracal`)
**Atenção, comportamento diferente dos outros campos:** `nivelAtividade` **ausente, nulo ou vazio NÃO apaga** o valor salvo, ele é mantido. Os outros campos opcionais são sobrescritos (ausente = apagado). A razão: não existe opção de voltar a "sem resposta" na tela, e as telas que reenviam o perfil sem o campo (Alimentação, por exemplo) não podem zerar a resposta sem querer. Não há como limpar o nível pela API
**Devolve:** o mesmo formato do `GET /api/perfil`
*(sobrescreve os valores atuais; não é histórico)*
**Erros:** 400 `{ mensagem }` se o `orcamentoSemanal` não for um dos valores aceitos, se o `nivelAtividade` não for um dos quatro ("Nível de atividade inválido."), se o `sexo` não for um dos dois, se a altura estiver fora da faixa, ou se a data de nascimento for no futuro, der menos de 18 anos ("O Ronu é para maiores de 18 anos.") ou mais de 100. As regras ficam em `Validacao/RegrasDadosCorporais.cs`, as mesmas do `POST /objetivos` e da simulação.

### `PUT /api/perfil/estado`
**Recebe:** `estado` (UF; a API ignora espaços e maiúsculas/minúsculas)
**Devolve:** `estado` (a sigla normalizada)
**Erros:** 400 se não for uma UF válida.

---

## Objetivo / dados corporais (histórico)

### `POST /api/objetivos`
**Recebe:** `peso` (kg, de 20 a 300), `objetivo` (`perder peso`, `manter peso` ou `ganhar peso`), `aderencia` (opcional: `seguiu`, `comeu_mais`, `comeu_menos` ou `nao_seguiu`; nulo ou vazio = sem resposta)
**Devolve:** `id`, `peso`, `objetivo`, `dataRegistro`, `aderencia`, `registradoHoje`, `temDieta`, `progressoAjuste` (mesmo formato do `GET /api/objetivos/atual`)
*(`dataRegistro` é preenchida automaticamente pela API, em UTC, não vem do cliente. Se já existe um registro do usuário no mesmo dia — o dia no fuso do estado (UF) do usuário; sem estado, horário de Brasília —, ele é atualizado em vez de criar outro, inclusive a `aderencia`, que volta a nulo se não for enviada)*
**Erros:** 400 `{ mensagem }` se a `aderencia` ou o `objetivo` não forem um dos valores aceitos, ou se o peso estiver fora da faixa.

### `GET /api/objetivos/atual`
**Devolve:** o registro mais recente de `ObjetivoUsuario` do usuário logado (`id`, `peso`, `objetivo`, `dataRegistro`, `aderencia`), mais `registradoHoje` (se esse registro é o de hoje, no fuso do estado do usuário, que o próximo POST vai sobrescrever — a mesma regra de "hoje" do POST) e `temDieta` (se o usuário já tem alguma dieta gerada). `progressoAjuste` diz quanto da janela da meta adaptativa já está preenchido agora, pela mesma regra da geração da dieta: `pesagens` (as que contariam: últimos 28 dias, desde a primeira dieta, no objetivo atual, sem "não deu pra seguir"), `diasCobertos` (entre a primeira e a última delas, arredondado para baixo), `suficiente` (se a próxima dieta já teria dados para ajustar) e os limites `minimoPesagens` (5), `minimoDias` (14) e `janelaDias` (28). Sem dieta gerada, tudo zero.
**Erros:** 404 se o usuário ainda não registrou nenhum objetivo.

### `GET /api/objetivos/tendencia`
**Devolve:** lista de pontos do gráfico de peso, em ordem de data, cada um com `data`, `pesoBruto`, `pesoTendencia` (peso suavizado) e, do registro de origem, `id`, `objetivo` e `aderencia` (nula quando não respondida)

### `DELETE /api/objetivos/{id}`
**Devolve:** 204 sem corpo
**Erros:** 404 se o registro não existe ou é de outro usuário; 400 se for o único registro restante (o usuário precisa manter pelo menos um objetivo).

---

## Modalidades

### `GET /api/modalidades`
**Devolve:** lista de todas as modalidades cadastradas no sistema (`id`, `nome`, `metReferencia`)
*(rota pública — é um catálogo do sistema, não dado de usuário)*

### `POST /api/usuarios/modalidades`
**Recebe:** `modalidadeId`, `diasSemana` (lista de dias, de 1 = segunda a 7 = domingo, sem repetição, pelo menos um), `duracaoMediaHoras` (de 0,25 a 5)
**Devolve:** `id`, `modalidadeId`, `diasSemana`, `duracaoMediaHoras`
*(se o usuário já pratica a modalidade, atualiza dias e duração em vez de duplicar o vínculo)*
**Erros:** 404 se a modalidade não existe; 400 se um dia estiver fora de 1–7, se houver dia repetido, se a lista vier vazia ou se a duração estiver fora da faixa.

### `GET /api/usuarios/modalidades`
**Devolve:** lista das modalidades que o usuário logado pratica, cada uma com `id`, `modalidade { id, nome, metReferencia }`, `diasSemana`, `duracaoMediaHoras` e `frequenciaSemanal` (calculada a partir da quantidade de `diasSemana`, nunca armazenada)

### `DELETE /api/usuarios/modalidades/{id}`
**Devolve:** 204 sem corpo
**Erros:** 404 se o vínculo não existe ou é de outro usuário.

---

## Preferências alimentares

### `POST /api/preferencias-alimentares`
**Recebe:** `alimento`, `tipo` (`preferido` ou `evitar`)
**Devolve:** `id`, `alimento`, `tipo`
*(se o usuário já tem uma preferência para o mesmo alimento, sem diferenciar maiúsculas/minúsculas, atualiza o `tipo` em vez de duplicar)*
**Erros:** 400 com `{ "mensagem": "Tipo de preferência inválido." }` se o `tipo` não for exatamente `preferido` ou `evitar` (sem normalizar maiúsculas nem espaços: `Evitar` é recusado).

### `GET /api/preferencias-alimentares`
**Devolve:** lista de preferências alimentares do usuário logado

### `DELETE /api/preferencias-alimentares/{id}`
**Devolve:** 204 sem corpo
**Erros:** 404 se a preferência não existe ou é de outro usuário.

---

## Dietas (geração via IA)

As três rotas devolvem a dieta no mesmo formato, `DietaResponse`:

- `dataGeracao`
- `dieta`
  - `dias`: os 7 dias da semana, cada um com `diaSemana`, `refeicoes` (cada refeição com `nome`, `horario` opcional, `alimentos [{ nome, quantidade, unidade }]` e `macros`), `totalDoDia`, `metaCalculada`, `metaElevadaPeloPiso` e `composicaoMeta`
  - `composicaoMeta` (nulo nas dietas geradas antes do campo): as partes da meta daquele dia, gravadas na geração, em kcal inteiras que somam exatamente `meta` (o arredondamento de `metaCalculada.calorias`): `gastoRepouso`, `atividadeForaDoTreino`, `nivelAtividade` e `fatorAtividade` (o nível e o fator usados; `fatorAtividade` é nulo em dietas anteriores à pergunta, que a tela mostra como ×1,4, e `nivelAtividade` é nulo quando a pessoa ainda não tinha respondido), `treino [{ nome, horas, kcal }]`, `objetivo`, `ajusteProgresso`, `pisoCarboidrato`, `meta`. A diferença de arredondamento cai na última parte diferente de zero
  - `ajusteAdaptativo` (pode ser nulo): `percentual`, `motivo`, `aplicado`, `ritmoRealKgSemana`, `ritmoEsperadoKgSemana`, `pontosUsados`
- `macros`, `totalDoDia` e `metaCalculada` têm `calorias`, `proteinasG`, `carboidratosG` e `gordurasG`

### `POST /api/dietas/gerar`
**Recebe:** nada — a API já busca internamente o perfil, o objetivo/peso mais recente, as modalidades e as preferências do usuário logado
**Devolve:** `dataGeracao`, `dieta` (formato acima)
*(regra de negócio: o usuário fica com no máximo 3 dietas salvas — ao salvar uma nova, as mais antigas que passarem desse limite são removidas)*

**Respostas de erro** (corpo sempre `{ "mensagem": "<texto para o usuário>" }`):

- **400 Bad Request** — cadastro incompleto para gerar a dieta (nenhuma chamada à IA é feita):
  - perfil sem altura, sexo ou data de nascimento:
    `{ "mensagem": "Complete seu perfil (altura, sexo e data de nascimento) antes de gerar uma dieta." }`
  - nenhum objetivo registrado:
    `{ "mensagem": "Registre um objetivo antes de gerar uma dieta." }`
- **429 Too Many Requests** — limite de 3 gerações por usuário numa janela móvel de 1 hora (contado nas dietas salvas; nenhuma chamada à IA é feita). É a primeira checagem, antes das de 400. A mensagem diz quando a próxima vaga libera:
  `{ "mensagem": "Você já gerou 3 dietas na última hora, o limite por hora. Tente novamente em 12 minutos." }`
- **503 Service Unavailable** — a IA não gerou uma dieta utilizável: falha de comunicação com o Gemini (rede, status de erro, timeout) ou resposta inválida mesmo depois de uma nova tentativa (bloqueada, cortada, JSON inválido, semana sem exatamente 7 dias únicos com os nomes esperados). Nada é salvo e o detalhe vai só para o log do servidor:
  `{ "mensagem": "Não foi possível gerar sua dieta agora. Tente novamente em alguns instantes." }`

### `GET /api/dietas/atual`
**Devolve:** a dieta mais recente do usuário logado (`dataGeracao`, `dieta`)
**Erros:** 404 se o usuário ainda não gerou nenhuma dieta.

### `GET /api/dietas/historico`
**Devolve:** lista das dietas salvas do usuário (até 3), da mais recente para a mais antiga, cada uma com `dataGeracao` e `dieta`

---

## Lista de compras

A lista é sempre da dieta atual (a mais recente do usuário do token, a mesma regra do `GET /api/dietas/atual`). Ela é calculada na hora a partir da dieta salva e da conversão de `Data/ConversaoCompras.cs` (fontes e método em `docs/compras/README.md` e em `docs/fontes-nutricionais.md`). Só as marcações "já tenho" são gravadas. Nenhuma das duas rotas recebe nem devolve dado de saúde (peso, meta, kcal, macros, objetivo): é o que a tela mostra e o que o "Compartilhar" manda.

### `GET /api/dietas/atual/lista-compras`
**Recebe:** nada
**Devolve:**
- `dietaId` (o id da dieta de onde a lista saiu, que o `PUT` manda de volta), `dataGeracao` (UTC; a tela mostra no fuso da UF), `versaoConversao` (informativa: a lista é recalculada a cada consulta, nada dela é gravado)
- `disponivel`: falso nas dietas geradas antes da lista fechada da base nutricional (algum alimento sem `AlimentoId`) ou com um item que a base não tem mais. Nesse caso vem `motivo: "dieta-antiga"` e `secoes` vazio: a lista não adivinha o item pelo nome
- `avisoOleo`: a dieta tem refogado ou ovo frito (a TACO já conta o óleo do preparo no prato, e ele não vira item da lista)
- `secoes`, na ordem do mercado (`hortifruti`, `acougue-e-peixaria`, `padaria`, `frios-laticinios-e-ovos`, `mercearia`, `bebidas`, `congelados`), só as que têm item, cada uma com `chave`, `nome` e `itens`. Cada item:
  - `chave` (o identificador estável do item de compra, ex.: `arroz-branco`, `ovos`), `nome` (com o nome regional pela UF da conta: Aipim no RJ, Macaxeira no Norte e Nordeste)
  - `quantidade` e `unidade` (`g`, `ml`, `unidades`, `fatias` ou `saches`), já arredondadas **para cima**: contáveis na próxima unidade inteira; gramas e mililitros em passos de 10 até 100 e de 50 acima disso, nunca zero
  - `texto`: a quantidade pronta para mostrar ("7 bananas", "550 g", "1,25 kg", "1,05 L", "14 sachês")
  - `pronto` (a quantidade é do prato pronto, sem fonte para converter: refogados e suco de laranja), `nota` (aviso que vai na própria linha, ex.: "de carne, sem osso e sem pele · compre um pouco mais"), `tenho` (marcado como "já tenho" nesta dieta)
  - `detalhe`: `quantidadeExata` (antes do arredondamento, 2 casas), `origens` (cada item da dieta que entra na linha: ovo cozido, ovo frito e clara entram em "Ovos"), com `alimentoId`, `nome`, `quantidadeNaDieta` (soma da semana), `medida`, `fator`, `convertido` e `passos [{ tipo, descricao, fator }]` (`tipo` = `par-cru-cozido`, `descarte`, `unidade`, `receita`, `calibracao` ou `derivacao`; a `descricao` traz o código e a página da fonte), e `observacoes` (ex.: "Sobram as gemas.")
**Erros:** 404 `{ "mensagem": "Nenhuma dieta gerada ainda." }` sem dieta. Dieta antiga não é erro: 200 com `disponivel: false`.

### `PUT /api/dietas/atual/lista-compras/itens/{chave}`
Marca (`tenho: true`) ou desmarca (`tenho: false`) um item como "já tenho" na dieta atual, salvo na conta (tabela `ListaComprasMarcacoes`) para aparecer em qualquer aparelho. Idempotente: marcar o que já está marcado, ou desmarcar o que não está, dá 204 do mesmo jeito. Dieta nova começa sem marcações, e as marcações de uma dieta saem junto com ela quando ela sai do histórico (limite de 3) ou quando a conta é apagada (exclusão em cascata, e o `RepositorioDietaIA` também apaga explicitamente).
**Recebe:** `dietaId` (a dieta da lista que estava aberta na tela), `tenho` (booleano)
**Devolve:** 204 sem corpo
**Erros** (corpo sempre `{ "mensagem" }`):
- **400** `"Item inválido."`: a `chave` não está no formato `[a-z0-9-]{1,64}`
- **400** `"Este item não está na sua lista de compras."`: a `chave` é válida mas não está na lista desta dieta (inclusive na dieta antiga, que não tem lista)
- **400** `"Informe a dieta da lista."` ou `"Informe se você já tem o item."`: falta `dietaId` ou `tenho` no corpo
- **404** `"Nenhuma dieta gerada ainda."`: o usuário não tem dieta
- **409** `"Sua dieta mudou desde que a lista foi aberta. Atualize a lista e tente de novo."`: o `dietaId` não é o da dieta atual (outra dieta foi gerada com a lista aberta, por exemplo em outro aparelho). O `dietaId` só é comparado com a dieta atual do usuário do token, então mandar o id da dieta de outra pessoa também cai aqui, e nada é gravado

Dois toques ao mesmo tempo no mesmo item (PC e celular) são barrados pelo índice único (`DietaIAId`, `Chave`): só essa violação de chave única do PostgreSQL (SQLSTATE 23505) é tratada como "já marcado" (204); qualquer outro erro do banco vira 500.

---

## Metas (simulação)

### `POST /api/metas/simulacao`
Calcula a meta da fórmula sem gravar nada e sem chamar a IA, com as mesmas classes da geração da dieta. Sem o ajuste adaptativo (que depende do histórico).
**Recebe (tudo opcional; o que não vier usa o dado salvo):** `sexo` (`Masculino` ou `Feminino`), `altura` (cm, 100 a 250), `dataNascimento` (idade de 18 a 100 anos), `peso` (kg, 20 a 300; ausente = peso de tendência salvo), `objetivo` (`perder peso`, `manter peso` ou `ganhar peso`; ausente = o do registro mais recente), `nivelAtividade` (`sentado`, `em_pe`, `andando` ou `bracal`; ausente = o salvo, e sem nível salvo o fator 1,4 de antes da pergunta), `modalidades [{ modalidadeId, diasSemana, duracaoMediaHoras }]` (mesmas regras do `POST /api/usuarios/modalidades`, sem repetir modalidade; se vier, mesmo vazia, substitui a lista salva)
**Devolve:** `versaoFormula`; `entradas { sexo, altura, dataNascimento, idade, peso, pesoOrigem ("informado" ou "tendencia"), objetivo }`; `gastoRepouso { total, peso, altura, idade, sexo }` (as parcelas de Mifflin-St Jeor, que somam o total pelo maior resto); `atividadeForaDoTreino { fator, nivel, kcal }` (`nivel` nulo = sem resposta, fator 1,4); `ritmoEsperadoKgSemana`; `modalidades [{ modalidadeId, nome, metReferencia, duracaoHoras, diasSemana, kcalSessao }]`; `dias` (sempre 7, de 1 = segunda a 7 = domingo) com `diaSemana`, `treinoKcal`, `manutencao`, `objetivoKcal`, `tetoDeficitAplicado` e `metaFormula`. Valores em kcal inteiras: cada parte é arredondada uma vez e todo total é a soma das partes mostradas
**Erros:** 400 `{ mensagem }` para valor fora do conjunto ou da faixa e quando falta um dado que também não está salvo ("Informe seu peso." etc.); 404 se uma modalidade não existe.

---

## Resumo de convenções aplicadas

- URLs representam **recursos** (substantivos, no plural), não ações
- Verbos HTTP seguem o padrão REST: `GET` para buscar, `POST` para criar, `PUT` para sobrescrever, `DELETE` para remover. Alguns `POST` atualizam o registro existente em vez de duplicar (objetivo do mesmo dia, mesma modalidade, mesmo alimento)
- Dados sensíveis (senha, token) nunca trafegam via query string / URL da API. A única exceção é o link de redefinição de senha, que leva o token no **fragmento** da URL do frontend (`#token=…`): o fragmento não é enviado a servidor nenhum, e a página manda o token à API no corpo de um POST
- `usuarioId` é sempre extraído do token JWT, nunca enviado pelo cliente
- Catálogos do sistema (ex: modalidades) são públicos; dados pessoais exigem autenticação
