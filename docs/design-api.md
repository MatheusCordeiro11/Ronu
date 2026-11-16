# Design da API — Ronu (MVP)

Lista de endpoints da API, definidos a partir das user stories (`docs/user-stories.md`) e do modelo de dados (`docs/der.png` / `docs/diagrama-classes.md`).

**Convenção geral:** todas as rotas exigem autenticação via token JWT (header `Authorization: Bearer <token>`), exceto `POST /api/auth/cadastro`, `POST /api/auth/login`, `POST /api/auth/google`, as três rotas de redefinição de senha (`POST /api/auth/esqueci-senha`, `POST /api/auth/redefinir-senha/verificar` e `POST /api/auth/redefinir-senha`), `GET /api/modalidades` e `GET /api/consentimentos/vigente/{tipo}`. O `usuarioId` nunca é enviado pelo cliente — é sempre extraído do token, para evitar que um usuário manipule dados de outra conta. Sem token (ou com token expirado), a resposta é **401** — também quando o token foi emitido antes da última troca de senha da conta (claim `iat` contra `Usuario.SenhaAlteradaEm`; tokens sem `iat`, anteriores a essa regra, valem até expirar) ou quando a conta não existe mais.

**Consentimento:** toda rota com login exige que a conta tenha aceitado a versão vigente do consentimento geral (dados de saúde; LGPD, art. 11, I). Sem esse aceite, a resposta é **403** `{ "mensagem": "Para continuar, autorize o uso dos seus dados de saúde.", "motivo": "consentimento" }`, e o frontend leva a pessoa à tela de consentimento pelo `motivo`. Ficam de fora as rotas de `/api/consentimentos` (é por elas que se aceita) e as de `/api/conta` (apagar a conta e baixar os dados são direitos de quem nunca aceitou ou quer revogar), além de `DELETE /api/perfil/rotina` e `DELETE /api/perfil/alergias/{codigo}` (apagar o próprio dado). Quem decide é o `FiltroConsentimento`, global; uma rota se dispensa com `[DispensaConsentimento]`. A regra fica ligada pela configuração `Consentimento:Exigir`, que vale `true` quando falta: só um `false` explícito a desliga, durante a publicação (passo a passo em `docs/privacidade.md`).

**Erros:** todos os erros vêm com corpo `{ "mensagem": "<texto para o usuário>" }`, em português. Isso vale também para os da validação automática do modelo (**400**): campo obrigatório vazio ou nulo, `[MinLength]`, `[Range]` devolvem a **primeira** mensagem de erro, escrita no atributo do DTO (ex.: `{ "mensagem": "A senha deve ter pelo menos 8 caracteres." }`). Erros que não vêm de um atributo — JSON malformado ou com tipo errado, campo obrigatório ausente do JSON, corpo vazio, parâmetro de rota inválido — devolvem a genérica `{ "mensagem": "Requisição inválida. Confira os dados enviados." }`. Nenhuma mensagem padrão do ASP.NET (em inglês) chega ao cliente (`Validacao/RespostaValidacao.cs`). Exceções não tratadas viram **500** com `{ "mensagem" }` genérica em produção.

---

## Autenticação

**Email:** sempre gravado e comparado normalizado — sem espaços nas pontas e em minúsculas (`Services/NormalizacaoEmail.cs`) —, no cadastro, no login, no login com Google e na redefinição de senha. Há um índice único em `Usuarios.Email`: uma conta por email.

**Senha:** as mesmas regras no cadastro e na redefinição (`Validacao/RegrasSenha.cs`): de 8 a 64 caracteres, e no máximo 72 bytes (o limite do BCrypt; acentos e símbolos ocupam mais de 1 byte).

**Limites por IP** (`Services/LimitesAutenticacao.cs`, em memória: zeram quando o App Service reinicia). O login, contra força bruta de senha; o Google e o cadastro, contra conta criada em massa. Contam toda requisição, certa ou errada, e as folgas cobrem IP compartilhado (CGNAT do celular, rede de faculdade). O IP é o do cliente, pelos forwarded headers:
- `POST /api/auth/login` e `POST /api/auth/google`: 20 tentativas a cada 15 minutos, cada rota com a sua contagem (quem errou a senha ainda entra com o Google). Acima disso, **429** `{ "mensagem": "Muitas tentativas de entrar. Espere 15 minutos e tente de novo." }`;
- `POST /api/auth/cadastro`: 5 cadastros por hora. Acima disso, **429** `{ "mensagem": "Muitas contas criadas a partir desta conexão. Tente de novo em até uma hora." }`.

### `POST /api/auth/cadastro`
**Recebe:** `nome`, `email`, `senha` (8 a 64 caracteres), `estado` (UF), `consentimentoGeral { versao }` (a versão do texto que a caixa do cadastro mostrou, lida do `GET /api/consentimentos/vigente/geral`)
**Devolve:** `id`, `nome`, `email` (normalizado; a senha nunca é retornada, mesmo em hash)
*(a conta e o aceite são gravados juntos, na mesma transação: com `Consentimento:Exigir` ligado, nenhuma conta nasce por aqui sem o aceite. A conta grava `CriadoEm`)*
**Erros:** 400 "Para criar a conta, autorize o uso dos seus dados de saúde." sem `consentimentoGeral`; 409 `{ mensagem, versaoVigente }` se a versão enviada não é a vigente (o texto mudou com a tela aberta); 409 `{ mensagem }` sem `versaoVigente` se o email já está cadastrado (sem diferenciar maiúsculas); 400 se a senha for curta ou longa demais ou se algum campo vier vazio. O frontend separa os dois 409 pela presença de `versaoVigente`. **429** acima do limite por IP (acima).

### `POST /api/auth/login`
**Recebe:** `email` (sem diferenciar maiúsculas), `senha`
**Devolve:** `token` (JWT, válido por 2 horas, com a claim `iat`), `usuario { id, nome }`, `precisaInformarEstado` (true quando a conta ainda não tem UF — contas via Google ou anteriores ao campo), `precisaConsentimento` (true quando a conta não tem aceite da versão vigente do consentimento geral — conta Google nova, conta anterior ao consentimento ou texto novo; o frontend vai para a tela de consentimento antes de tudo)
**Erros:** 401 com "Email ou senha inválidos." ou, se a conta foi criada pelo Google e não tem senha, com orientação para entrar com o Google; **429** acima do limite por IP (acima).

### `POST /api/auth/google`
**Recebe:** `idToken` (o credential devolvido pelo botão do Google)
**Devolve:** o mesmo formato do `POST /api/auth/login`
*(a API valida a assinatura do token junto ao Google antes de usar qualquer dado dele. Se não existe conta com o email, cria uma sem senha; se existe, vincula o login com Google a ela. Por isso exige o email verificado pelo Google — `EmailVerified`. A conta criada aqui nasce sem aceite do consentimento, e a resposta vem com `precisaConsentimento: true`)*
**Erros:** 401 se o token do Google for inválido ou se o email da conta Google não estiver verificado; **429** acima do limite por IP (acima).

### `POST /api/auth/esqueci-senha`
**Recebe:** `email`
**Devolve:** **202** `{ mensagem }`, sempre com o mesmo texto — exista ou não a conta, tenha ela senha ou só login com Google, e também acima do limite por conta. Quem decide o que acontece é o `ServicoRedefinicaoSenha`:
- conta com senha: email com o link `{Frontend:UrlBase}/nova-senha.html#token=…`, válido por **30 minutos** e de uso único. O token vai no **fragmento** (`#`), que não chega a servidor nenhum nem vai no cabeçalho Referer; o banco guarda só o SHA-256 dele (tabela `PedidosRedefinicaoSenha`). Um pedido novo invalida os links anteriores;
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
**Devolve:** `altura` (cm), `sexo`, `dataNascimento`, `rotinaDiaria`, `orcamentoSemanal`, `nivelAtividade`, `horarioAcordar` e `horarioDormir` ("HH:mm") — todos podem vir nulos enquanto o onboarding não os preencheu — e os dados da conta: `nome`, `email`, `estado` (UF, `null` se ainda não informado), `temSenha` e `temGoogle` (se a conta tem senha própria e se tem login com Google vinculado; a senha e o hash nunca vêm)

### `PUT /api/perfil`
**Recebe:** `altura` (cm, de 100 a 250), `sexo` (`Masculino` ou `Feminino`), `dataNascimento` (idade de 18 a 100 anos) (obrigatórios); `orcamentoSemanal`, `nivelAtividade`, `horarioAcordar` e `horarioDormir` (opcionais; `orcamentoSemanal` aceita `economico`, `moderado` ou `sem_restricao`; `nivelAtividade` aceita `sentado`, `em_pe`, `andando` ou `bracal`; os horários vão em "HH:mm", de 00:00 a 23:59, exatamente nesse formato)
**Atenção, comportamento diferente dos outros campos:** `nivelAtividade` **ausente, nulo ou vazio NÃO apaga** o valor salvo, ele é mantido. Os horários seguem quase a mesma regra (`Validacao/RegrasHorario.cs`): ausente ou nulo mantém o salvo, mas **texto vazio (`""`) apaga**. Os outros campos opcionais são sobrescritos (ausente = apagado). A razão: não existe opção de voltar a "sem resposta" na tela, e as telas que reenviam o perfil sem o campo (Alimentação, por exemplo) não podem zerar a resposta sem querer. Não há como limpar o nível pela API
**Devolve:** o mesmo formato do `GET /api/perfil`
*(sobrescreve os valores atuais; não é histórico)*
A `rotinaDiaria` (texto livre antigo) **não é mais gravada pelo PUT**: se um frontend antigo a mandar, ela é ignorada, sem erro e sem apagar o texto já salvo. Ela também não vai mais para o Gemini desde o prompt sem dado pessoal: no lugar dela vão os horários (seção Dietas). O texto já salvo continua no `GET` e na exportação até a pessoa apagá-lo (`DELETE /api/perfil/rotina`) ou até a limpeza dos prazos de guarda, 60 dias depois da publicação (`docs/privacidade.md`, seção 3).
**Erros:** 400 `{ mensagem }` se um horário não estiver em "HH:mm" ("Horário inválido. Use o formato HH:mm, de 00:00 a 23:59."), se o `orcamentoSemanal` não for um dos valores aceitos, se o `nivelAtividade` não for um dos quatro ("Nível de atividade inválido."), se o `sexo` não for um dos dois, se a altura estiver fora da faixa, ou se a data de nascimento for no futuro, der menos de 18 anos ("O Ronu é para maiores de 18 anos.") ou mais de 100. As regras ficam em `Validacao/RegrasDadosCorporais.cs`, as mesmas do `POST /objetivos` e da simulação.

### `DELETE /api/perfil/rotina`
Apaga a rotina em texto livre antiga (`rotinaDiaria`), que não é mais usada.
**Devolve:** 204, com ou sem texto salvo (repetir não é erro).

### Alergias (TACO 2c)

Os 8 chips, cada um um código fixo: `leite`, `gluten`, `ovo`, `amendoim`, `castanhas`, `soja`, `peixe` e `crustaceos` (`Services/IA/CodigosAlergia.cs`; o Anexo III da RDC ANVISA 727/2022 no que existe na base). Cada chip é uma linha da tabela `AlergiasUsuario`, gravada ou apagada sozinha: rotas próprias, fora do `PUT /api/perfil`, para duas abas não sobrescreverem os chips uma da outra. Alergia é dado de saúde (LGPD, art. 11, I) com consentimento próprio, o tipo `alergias` (seção Consentimento). Os chips são filtro duro na geração, na troca e na conferência das preferências (pelo ponto único `ListaDaPessoa`), marcam os itens da lista de compras e o aviso da dieta atual, e nunca vão para o Gemini nem para o log. Amendoim e Castanhas tiram as duas famílias (`RegrasAlergias`). Marcar um chip não muda a dieta já gerada. As três rotas têm limite por usuário (do token, em memória): 60 pedidos por minuto, somados. Acima disso, **429** `{ "mensagem": "Muitas mudanças nas alergias em pouco tempo. Espere um minuto e tente de novo." }`.

#### `GET /api/perfil/alergias`
**Devolve:** `alergias` (os códigos marcados, na ordem dos chips), `aceiteVigente` (há aceite da versão vigente do tipo `alergias`, não revogado) e `aceitoEm` (a data desse aceite; nulo sem ele)

#### `PUT /api/perfil/alergias/{codigo}`
Marca um chip. **Recebe** (corpo opcional): `versaoConsentimento`, a versão do texto que a caixa mostrou, só quando a pessoa acabou de marcar a caixa. Com a versão vigente e sem aceite, o aceite e o chip são gravados juntos; com aceite vigente, o corpo pode ir vazio. Marcar de novo o mesmo chip não duplica.
**Devolve:** 200 com o mesmo formato do `GET` (a tela redesenha com o que está gravado)
**Erros:** **400** `"Alergia inválida."` (código fora dos 8, sem normalizar maiúsculas nem espaços); **409** `{ mensagem: "Para salvar suas alergias, autorize o uso delas para montar o cardápio.", motivo: "consentimento_alergias", versaoVigente }` sem aceite vigente e sem versão no corpo (a tela abre a caixa); **409** `{ mensagem: "O texto da autorização mudou. Leia de novo e aceite a versão nova.", motivo: "consentimento_alergias", versaoVigente }` se a versão do corpo não é a vigente. Nada é gravado nos 409.

#### `DELETE /api/perfil/alergias/{codigo}`
Desmarca um chip. Quando não sobra nenhum, as linhas já saíram e o aceite do tipo `alergias` ganha `RevogadoEm` (a linha fica, para o histórico): marcar de novo pede a caixa outra vez. Funciona sem o consentimento geral, como o `DELETE /api/perfil/rotina`. Desmarcar o que não está marcado responde igual.
**Devolve:** 200 com o mesmo formato do `GET`
**Erros:** **400** `"Alergia inválida."`

### `PUT /api/perfil/estado`
**Recebe:** `estado` (UF; a API ignora espaços e maiúsculas/minúsculas)
**Devolve:** `estado` (a sigla normalizada)
**Erros:** 400 se não for uma UF válida.

---

## Objetivo / dados corporais (histórico)

### `POST /api/objetivos`
**Recebe:** `peso` (kg, de 20 a 300), `objetivo` (`perder peso`, `manter peso` ou `ganhar peso`), `aderencia` (opcional: `seguiu`, `comeu_mais`, `comeu_menos` ou `nao_seguiu`; nulo ou vazio = sem resposta)
**Devolve:** `id`, `peso`, `objetivo`, `dataRegistro`, `aderencia`, `registradoHoje`, `temDieta`, `progressoAjuste`, `refeicoesFeitasDesdePesagem` (mesmo formato do `GET /api/objetivos/atual`)
*(`dataRegistro` é preenchida automaticamente pela API, em UTC, não vem do cliente. Se já existe um registro do usuário no mesmo dia — o dia no fuso do estado (UF) do usuário; sem estado, horário de Brasília —, ele é atualizado em vez de criar outro, inclusive a `aderencia`, que volta a nulo se não for enviada)*
**Erros:** 400 `{ mensagem }` se a `aderencia` ou o `objetivo` não forem um dos valores aceitos, ou se o peso estiver fora da faixa.

### `GET /api/objetivos/atual`
**Devolve:** o registro mais recente de `ObjetivoUsuario` do usuário logado (`id`, `peso`, `objetivo`, `dataRegistro`, `aderencia`), mais `registradoHoje` (se esse registro é o de hoje, no fuso do estado do usuário, que o próximo POST vai sobrescrever — a mesma regra de "hoje" do POST) e `temDieta` (se o usuário já tem alguma dieta gerada). `progressoAjuste` diz quanto da janela da meta adaptativa já está preenchido agora, pela mesma regra da geração da dieta: `pesagens` (as que contariam: últimos 28 dias, desde a primeira dieta, no objetivo atual, sem "não deu pra seguir"), `diasCobertos` (entre a primeira e a última delas, arredondado para baixo), `suficiente` (se a próxima dieta já teria dados para ajustar) e os limites `minimoPesagens` (5), `minimoDias` (14) e `janelaDias` (28). Sem dieta gerada, tudo zero. `refeicoesFeitasDesdePesagem`: quantas refeições a pessoa marcou como feitas (seção Refeições feitas) desde a pesagem a que a pergunta de aderência se refere (a anterior à de hoje, se `registradoHoje`; senão, a mais recente), do dia dela no fuso da UF em diante, inclusive; nulo sem essa pesagem. Só o número, sem total: a tela do Progresso mostra "você marcou X refeições como feitas" sem sugerir resposta. As marcações duram 35 dias, então uma pesagem mais antiga conta só as dos últimos 35.
**Erros:** 404 se o usuário ainda não registrou nenhum objetivo.

### `GET /api/objetivos/tendencia`
**Devolve:** lista de pontos do gráfico de peso, em ordem de data, cada um com `data`, `pesoBruto`, `pesoTendencia` (peso suavizado) e, do registro de origem, `id`, `objetivo` e `aderencia` (nula quando não respondida)

### `DELETE /api/objetivos/{id}`
**Devolve:** 204 sem corpo
**Erros:** 404 se o registro não existe ou é de outro usuário; 400 se for o único registro restante (o usuário precisa manter pelo menos um objetivo).

---

## Modalidades

### `GET /api/modalidades`
**Devolve:** lista de todas as modalidades cadastradas no sistema (`id`, `nome`, `metReferencia`, `categoria`, `metBlocoLeve`, `metBlocoIntenso`). `metReferencia` é o MET do ritmo "normal". `metBlocoLeve` e `metBlocoIntenso` são os METs dos dois blocos da aula, preenchidos só nas 11 modalidades modeladas como aula em blocos (as 10 lutas e o treino funcional); nulos = sem o seletor "Ritmo da aula"
*(rota pública — é um catálogo do sistema, não dado de usuário)*

### `POST /api/usuarios/modalidades`
**Recebe:** `modalidadeId`, `diasSemana` (lista de dias, de 1 = segunda a 7 = domingo, sem repetição, pelo menos um), `duracaoMediaHoras` (de 0,25 a 5), `horarioInicio` (opcional, "HH:mm": a hora de início, a mesma em todos os dias da modalidade), `intensidade` (opcional, o "Ritmo da aula": `tecnica`, `normal` ou `sparring`; as chaves são as mesmas no treino funcional, onde a tela mostra "Mais força" e "Mais circuito")
**Devolve:** `id`, `modalidadeId`, `diasSemana`, `duracaoMediaHoras`, `horarioInicio`, `intensidade`
*(se o usuário já pratica a modalidade, atualiza dias e duração em vez de duplicar o vínculo; o `horarioInicio` ausente ou nulo mantém o salvo e vazio apaga, porque a tela de treino de hoje não manda o campo; a `intensidade` ausente ou nula mantém a salva, e num vínculo novo vira `normal`)*
**Erros:** 404 se a modalidade não existe; 400 se o `horarioInicio` não estiver em "HH:mm", se um dia estiver fora de 1–7, se houver dia repetido, se a lista vier vazia, se a duração estiver fora da faixa, se a `intensidade` não for um dos três valores (vazio também) ou se for diferente de `normal` numa modalidade sem blocos ("Esta modalidade não tem ritmo da aula.").

### `GET /api/usuarios/modalidades`
**Devolve:** lista das modalidades que o usuário logado pratica, cada uma com `id`, `modalidade { id, nome, metReferencia, categoria, metBlocoLeve, metBlocoIntenso }`, `diasSemana`, `duracaoMediaHoras`, `horarioInicio` ("HH:mm" ou nulo), `intensidade` (`tecnica`, `normal` ou `sparring`) e `frequenciaSemanal` (calculada a partir da quantidade de `diasSemana`, nunca armazenada)

### `DELETE /api/usuarios/modalidades/{id}`
**Devolve:** 204 sem corpo
**Erros:** 404 se o vínculo não existe ou é de outro usuário.

---

## Preferências alimentares

**Tipos** (`TiposPreferencia`; restrições em dois níveis, plano família etapa 3, decisão D2):
- `preferido` ("Gosto de comer"): peso a favor no prompt.
- `nao_como` ("Não como"): bloqueio. O item sai da lista fechada e do enum do Gemini, da troca, conta no aviso de lista pobre e pode sugerir chip de alergia. É o comportamento do `evitar` de antes da etapa 3: a migration `PreferenciasEmDoisNiveis` (só dados) trocou todo `evitar` que existia por `nao_como`, e assim nenhuma dieta mudou.
- `evitar` ("Prefiro evitar", leve): o item **continua** na lista e no enum; o prompt pede para usar pouco, no máximo `GeradorDietaGemini.MaximoPorSemanaEvitar` = 2 vezes na semana por chip, somando os itens que ele casa (calibração do projeto; "carne vermelha" conta todos os cortes juntos), e o C# confere depois da resposta (seção Dietas). Na troca, o item leve pode ser oferecido, mas vem depois dos outros.

### `POST /api/preferencias-alimentares`
**Recebe:** `alimento`, `tipo` (`preferido`, `nao_como` ou `evitar`)
**Devolve:** `id`, `alimento`, `tipo`
*(se o usuário já tem uma preferência para o mesmo alimento, sem diferenciar maiúsculas/minúsculas, atualiza o `tipo` em vez de duplicar: mudar de nível move o item, com o mesmo `id`)*
**Erros:** 400 com `{ "mensagem": "Tipo de preferência inválido." }` se o `tipo` não for exatamente um dos três (sem normalizar maiúsculas nem espaços: `Evitar` e `Nao_Como` são recusados).

### `GET /api/preferencias-alimentares`
**Devolve:** lista de preferências alimentares do usuário logado

### `GET /api/preferencias-alimentares/conferencia`
Confere as preferências do usuário logado contra a base nutricional, pela mesma lista da geração (`ListaDaPessoa`, com os chips de alergia).
**Devolve:** `preferencias`, cada uma com `id`, `alimento`, `tipo`, `casou`, `alimentos [{ nome, grupo }]` (os itens que ela casa; no `nao_como`, os que saem; no `evitar` leve, os que o Ronu usa pouco), `semCasamento`, `lidoComo` e `sugereAlergias` (TACO 2c: os códigos dos chips que um `nao_como` parece ser, quando casa com linhas de alérgeno da tabela do `CasamentoPreferencias`, como `["crustaceos"]` para "camarão" e `["ovo", "soja"]` para "soja e ovo", na ordem dos chips; vazia no "gosto", no `evitar` leve e quando o texto fala de lactose ou intolerância, que não são alergia; nunca traz um chip já marcado); e `lista` (`atende`, `vazios`, `abaixoDoMinimo`, `itensPorPapel`, `gruposDeProteina`), que conta também os chips.

### `DELETE /api/preferencias-alimentares/{id}`
**Devolve:** 204 sem corpo
**Erros:** 404 se a preferência não existe ou é de outro usuário.

---

## Dietas (geração via IA)

As três rotas devolvem a dieta no mesmo formato, `DietaResponse`:

- `dataGeracao`
- `dietaId` e `revisao`: o id da dieta e quantas gravações ela já teve (começa em 0 e sobe a cada troca, desfazer ou volta ao original). A troca de alimento manda os dois de volta, e uma revisão velha dá 409. Nulos na resposta do `POST /api/dietas/gerar`
- `semTroca`: por que a dieta não tem troca de alimento, `"dieta-anterior"` (algum alimento sem `alimentoId`, gerada antes da lista fechada) ou `"outra-versao-da-base"` (`versaoBase` diferente da atual); nulo quando tem. Só a dieta atual tem troca: no histórico, a tela não oferece mesmo com `semTroca` nulo
- `alergiasNaDieta` (TACO 2c; só no `GET /api/dietas/atual` e nas respostas da troca, nulo no histórico e no `POST /gerar`): os alimentos da dieta que têm um chip de alergia marcado pela pessoa, `[{ alimentoId, nome, alergias, vezes }]` (`alergias`: os códigos dos chips que o tiram; `vezes`: quantas vezes aparece na semana). Lista vazia quando não há nenhum. Itens sem `alimentoId` (dietas antigas) ficam de fora
- `dieta`
  - `dias`: os 7 dias da semana, cada um com `diaSemana`, `refeicoes` (cada refeição com `nome`, `horario` opcional, `alimentos` e `macros`), `totalDoDia`, `metaCalculada`, `metaElevadaPeloPiso` e `composicaoMeta`
  - cada alimento: `nome` (com a contagem nos contáveis, ex.: "Pão francês (2 unidades)"), `quantidade`, `unidade`, `alimentoId` (o item da base nutricional; nulo nas dietas anteriores à lista fechada), `macros` (calculados pela base; nulos nas dietas antigas) e, só nos itens trocados, `original`: o alimento como veio na dieta antes da primeira troca (mesmo formato, sem `original`), para a marca "trocado" e o "Voltar ao original"
  - `versaoBase`: a versão da base nutricional usada (nula nas dietas anteriores à lista fechada)
  - `avisoListaPobre` (nulo quando a lista de alimentos atendeu aos mínimos): `papeis`, `itensPorPapel` e `gruposDeProteina`
  - `ultimaTroca` (só quando há o que desfazer): `revisao` (a gravação que o "Desfazer" desfaz) e `itens [{ dia, refeicao, item, anterior }]`, cada posição com o alimento como estava antes
  - `composicaoMeta` (nulo nas dietas geradas antes do campo): as partes da meta daquele dia, gravadas na geração, em kcal inteiras que somam exatamente `meta` (o arredondamento de `metaCalculada.calorias`): `gastoRepouso`, `atividadeForaDoTreino`, `nivelAtividade` e `fatorAtividade` (o nível e o fator usados; `fatorAtividade` é nulo em dietas anteriores à pergunta, que a tela mostra como ×1,4, e `nivelAtividade` é nulo quando a pessoa ainda não tinha respondido), `treino [{ nome, horas, intensidade, kcal }]` (`intensidade` é o ritmo da aula da sessão, nulo nas dietas geradas antes do seletor, quando todas eram `normal`), `objetivo`, `ajusteProgresso`, `pisoCarboidrato`, `meta`. A diferença de arredondamento cai na última parte diferente de zero
  - `ajusteAdaptativo` (pode ser nulo): `percentual`, `motivo`, `aplicado`, `ritmoRealKgSemana`, `ritmoEsperadoKgSemana`, `pontosUsados`
- `macros`, `totalDoDia` e `metaCalculada` têm `calorias`, `proteinasG`, `carboidratosG` e `gordurasG`

### `POST /api/dietas/gerar`
**Recebe:** nada — a API já busca internamente o perfil, o objetivo/peso mais recente, as modalidades e as preferências do usuário logado
**Devolve:** `dataGeracao`, `dieta` (formato acima)
*(regra de negócio: o usuário fica com no máximo 3 dietas salvas — ao salvar uma nova, as mais antigas que passarem desse limite são removidas)*

**O que vai para o Gemini** (`GeradorDietaGemini.MontarPrompt`). O Ronu usa o plano gratuito, em que o Google usa o conteúdo e pede para não mandar dado pessoal; por isso o prompt leva só o necessário para montar o cardápio, e as metas já saem calculadas em C#:
- as metas de cada dia (kcal e gramas de proteína, carboidrato e gordura), cada uma marcada como dia de déficit, de manutenção ou de superávit e, para quem treina, de treino (com o horário de início e de fim, se informado) ou de descanso, sem o nome da modalidade;
- os horários de acordar e de dormir, se informados;
- a UF (os nomes regionais e o feijão preto no Sul) e a faixa de orçamento;
- a lista fechada de alimentos (itens da base, sem o `nao_como` e sem o que os chips de alergia tiram), o peso de uma unidade dos contáveis, os "gostos" que casaram com a base (menos os que também são leves) e, do `nao_como`, só os termos que não casaram com nenhum item, sem as palavras sobre a pessoa ou a saúde dela (`TermoParaIA`: "tenho alergia a gergelim" vai como "gergelim"), como restrição dura;
- desde a etapa 3, só para quem tem `evitar` leve, um parágrafo a mais: "Alimentos da lista que a pessoa prefere evitar, em grupos (um por linha): use pouco, no máximo 2 vezes na semana em cada grupo, somando todos os alimentos do grupo, e só quando ajudarem a chegar na meta:" e uma linha "- …" por chip (os itens leves dele, pelo nome da lista; o texto digitado não vai) e "Outras coisas que a pessoa prefere evitar (evite quando possível, sem proibir): …" (os trechos leves sem casamento, também pelo `TermoParaIA`). Sem nenhum leve, o parágrafo some e o prompt fica como antes.

Não vão: nome, e-mail, id da conta, sexo, idade, peso, altura, objetivo, nome das modalidades, a rotina em texto, o `nao_como` e o `evitar` como foram digitados e as alergias (nem os códigos, nem quantas são: a lista já chega sem os itens). A lista sai sempre do `ListaDaPessoa` (UF, preferências e alergias): o gerador recusa um contexto sem ela, e um teste varre o código para o `ListaAlimentosPessoa.Montar` não ser chamado de outro lugar.

**Checagens da resposta**, antes de salvar. Em todas, a 1ª resposta com o problema é recusada e o Gemini é chamado de novo; o que muda é a última tentativa:
- estrutura (JSON, 7 dias únicos com os nomes certos, alimentos da lista): na última, vira 503;
- regra 8, bebida alcoólica fora dos "gostos" (`ValidadorBebidaAlcoolica`): na última, a bebida sai;
- "Prefiro evitar" (etapa 3): um chip leve com mais de `MaximoPorSemanaEvitar` (2) vezes na semana, somando os itens que ele casa (`ListaAlimentos.GruposLeves`, um grupo por chip; um item em dois chips conta nos dois). Na última, as vezes excedentes de cada grupo (as últimas da semana, pela ordem dos dias) viram o melhor substituto da troca (`CalculadoraTroca.MelhorSubstituto`: mesmo grupo, da lista da pessoa, sem outro item leve, dentro da porção e dos 5% do dia), depois do cálculo pela base; sem substituto, o item fica como está. O log registra as trocas e as que ficaram, só com os ids da base e o dia (nunca o texto digitado);
- regra 7, café coado, chá mate, chá preto ou refrigerante de cola no jantar, na ceia ou a partir das 18:00 (`ValidadorCafeina`; lista e fontes em `docs/fontes-nutricionais.md`): na última, o café e o chá viram chá de erva-doce (se ele está na lista da pessoa) e o refrigerante sai;
- regra 15, menos de 5 refeições num dia (6 a partir de 3500 kcal): na última, a dieta é aceita assim;
- regra 16, almoço e jantar sem nenhuma fonte de gordura (óleos e gorduras, oleaginosas e sementes, refogados) e o dia abaixo de 85% da gordura da meta: na última, a dieta é aceita assim.

O que foi trocado, tirado ou aceito na última tentativa vai para o log: o dia, a refeição e o alimento, sem nome, e-mail nem id da conta. Medições que embasaram as regras: `docs/medicoes/perfis-g.md` (g10a a g10d).

**Respostas de erro** (corpo sempre `{ "mensagem": "<texto para o usuário>" }`):

- **400 Bad Request** — cadastro incompleto para gerar a dieta (nenhuma chamada à IA é feita):
  - perfil sem altura, sexo ou data de nascimento:
    `{ "mensagem": "Complete seu perfil (altura, sexo e data de nascimento) antes de gerar uma dieta." }`
  - nenhum objetivo registrado:
    `{ "mensagem": "Registre um objetivo antes de gerar uma dieta." }`
  - algum papel do prato ficou sem nenhum item depois do `nao_como` e dos chips de alergia (o `evitar` leve não tira item) (abaixo do mínimo, sem ficar vazio, a dieta sai com o `avisoListaPobre`; o filtro nunca é relaxado):
    `{ "mensagem": "Com o que você marcou em Não como e as suas alergias, não sobrou nenhuma fonte de proteína entre os alimentos do Ronu. Revise a lista de Não como em Alimentação." }`
- **429 Too Many Requests** — limite de 3 gerações por usuário numa janela móvel de 1 hora (contado nas dietas salvas; nenhuma chamada à IA é feita). É a primeira checagem, antes das de 400. A mensagem diz quando a próxima vaga libera:
  `{ "mensagem": "Você já gerou 3 dietas na última hora, o limite por hora. Tente novamente em 12 minutos." }`
- **503 Service Unavailable** — a IA não gerou uma dieta utilizável: falha de comunicação com o Gemini (rede, status de erro, timeout) ou resposta inválida mesmo depois de uma nova tentativa (bloqueada, cortada, JSON inválido, semana sem exatamente 7 dias únicos com os nomes esperados). Nada é salvo e o detalhe vai só para o log do servidor:
  `{ "mensagem": "Não foi possível gerar sua dieta agora. Tente novamente em alguns instantes." }`

### `GET /api/dietas/atual`
**Devolve:** a dieta mais recente do usuário logado (`dataGeracao`, `dietaId`, `revisao`, `semTroca`, `alergiasNaDieta`, `dieta`)
**Erros:** 404 se o usuário ainda não gerou nenhuma dieta.

### `GET /api/dietas/historico`
**Devolve:** lista das dietas salvas do usuário (até 3), da mais recente para a mais antiga, cada uma no formato `DietaResponse` completo (`dataGeracao`, `dietaId`, `revisao`, `semTroca`, `dieta`)

---

## Trocar alimento

Troca um alimento da dieta atual por um substituto da base nutricional, só na refeição tocada ou na semana toda (cada vez em que o mesmo alimento aparece). Tudo é calculado em C# pela base (`CalculadoraTroca`), sem chamar a IA. A dieta é sempre a atual do usuário do token: o `dietaId` do cliente só é comparado com ela, então o id da dieta de outra pessoa cai no 409, sem saber se ela existe. Toda gravação confere a `revisao` (token de concorrência no banco): duas abas trocando ao mesmo tempo não sobrescrevem uma à outra.

Regras do substituto: mesmo grupo de troca, na lista de alimentos da pessoa com as preferências e os chips de alergia **atuais** (um `nao_como` ou um chip novo vale já; o substituto nunca tem alergia marcada; um item do `evitar` leve pode ser oferecido, mas vem depois dos outros), quantidade que mantém o nutriente principal do original (o peso ou o volume nos itens sem nutriente principal), até a porção máxima da troca (`PorcaoMaximaTroca`, calibração do projeto). Fica de fora o que deixaria algum dia afetado mais de 5% fora da meta (um dia que já estava fora aceita o que não piora), o que já está na refeição, o refogado numa refeição com azeite ou óleo (e vice-versa) e, nas gorduras, o item de outro uso. Só o item trocado muda: os outros alimentos não são reajustados, e a meta do dia não muda. As marcações "já tenho" da lista de compras dos itens que saíram, ou que passaram a pedir mais, caem na mesma gravação.

**Limites por usuário** (do token, em memória), contados à parte: 120 leituras e 30 gravações por minuto. Acima disso, **429** `{ "mensagem": "Muitos pedidos de troca em pouco tempo. Espere um minuto e tente de novo." }` (a mensagem do 429 é escolhida pela política da rota; a redefinição de senha tem a dela).

**Erros comuns às quatro rotas** (corpo sempre `{ "mensagem" }`):
- **404** `"Nenhuma dieta gerada ainda."`
- **422** quando a dieta não tem troca: `"Esta dieta foi gerada antes da troca de alimentos existir. Gere uma dieta nova para trocar alimentos."` ou `"A base de alimentos do Ronu mudou desde que esta dieta foi gerada. Gere uma dieta nova para trocar alimentos."`
- **400** `"Alimento não encontrado na dieta."`: a posição (`dia`, `refeicao`, `item`, índices a partir de 0, na ordem da dieta salva) não existe
- **409** `{ mensagem, motivo: "refeicao_da_casa" }` (plano família, etapa 5; só nas opções e na troca): o item é do almoço ou do jantar da casa (`daCasa: true`). Para o membro, `"Este alimento é da refeição da casa: só quem cuida da casa troca. Use Pedir troca."`; para o dono, `"Este alimento é da refeição da casa. Troque pela semana da casa: a troca vale para todos."` (a troca na casa é `POST /api/casa/semana/trocas`). Nada é gravado. Quem saiu da casa tem as refeições com `daCasa` falso e troca normalmente
- Na dieta de quem está numa casa, o escopo `semana` não cruza a fronteira da casa: trocar um item do café na semana toda não muda o mesmo item no almoço ou no jantar da casa

### `GET /api/dietas/atual/trocas/opcoes?dia=&refeicao=&item=`
**Devolve:**
- `dietaId`, `revisao`
- `original`: `alimentoId`, `nome`, `quantidade`, `unidade`, `kcal` e `nutrienteG` (gramas do nutriente principal; nulo sem nutriente principal)
- `nutriente`: `proteina`, `carboidrato`, `gordura` ou `livre` (sem nutriente a manter: fica o peso)
- `ocorrenciasNaSemana`: quantas vezes o mesmo alimento aparece na semana
- `foraPeloEvitar`: quantos itens do grupo saíram pelo que a pessoa marcou em "Não como" (`nao_como`; sem contar os que saíram por alergia). O nome do campo é de antes da etapa 3 e ficou, para não quebrar a tela; o `evitar` leve não tira item
- `foraPelaAlergia`: quantos itens do grupo saíram pelos chips de alergia (TACO 2c), mesmo que também estejam no `nao_como`
- `refeicao` e `semana`, os dois escopos, cada um com:
  - `dasPreferencias` (até 3, no máximo um por gosto), `parecidos` (até 3) e `verMais` (o resto que passa nas regras). Primeiro o mesmo uso, depois o que menos muda as kcal. Cada opção: `alimentoId`, `nome` (regional pela UF, com a contagem nos contáveis), `quantidade`, `unidade`, `contagem` (nos contáveis), `kcal` e `nutrienteG` do item tocado, `difKcal` (a soma das diferenças de todas as vezes do escopo), `outroUso` (substituto de outro uso, como macarrão no lugar do pão do café), `ocorrencias` e `dias [{ dia, diaSemana, totalAntes, totalDepois, meta, desvioPct }]` (a prévia de cada dia afetado; `desvioPct` com 2 casas)
  - `fora`: quantas opções ficaram de fora por motivo (`porcaoMaxima`, `foraDaMeta`, `jaNaRefeicao`, `refogadoComOleo`, `usoDiferente`)
  - `mensagemSemOpcoes`: só quando nenhuma opção passou, o porquê em uma frase (ex.: `"Nenhum substituto serve aqui: 2 passariam da porção máxima e 1 já está nesta refeição."`; os de alergia entram como `"3 têm uma das suas alergias"`)

### `POST /api/dietas/atual/trocas`
**Recebe:** `dietaId`, `revisao`, `dia`, `refeicao`, `item`, `alimentoIdAtual` (o alimento que a tela mostrava na posição), `alimentoIdNovo` e `escopo` (`refeicao` ou `semana`). Sem quantidade nem kcal: o servidor recalcula tudo e confere as regras de novo.
**Devolve:** a `DietaResponse` com a revisão nova, `alergiasNaDieta` recalculado e `marcacoesRemovidas` (quantas marcações "já tenho" caíram nesta gravação; só a contagem). Trocar de novo um item já trocado mantém o primeiro `original`.
**Erros:** **400** `"Escopo inválido."`; **400** `"Este substituto não serve aqui (porção, meta do dia, o que você marcou em Não como ou suas alergias). Atualize as opções e escolha outro."` (por exemplo, as preferências mudaram em outra aba); **409** `"Sua dieta mudou desde que a tela foi aberta. Atualize e tente de novo."` (revisão velha, outra dieta gerada ou outro alimento na posição)

### `DELETE /api/dietas/atual/trocas/ultima?dietaId=&revisao=`
"Desfazer": volta exatamente ao que estava antes da última gravação (troca ou volta ao original), se ela ainda é a última.
**Devolve:** a `DietaResponse` com a revisão nova, `alergiasNaDieta` e `marcacoesRemovidas`
**Erros:** **409** `"Não há troca para desfazer."` ou a mensagem de dieta mudada; **422** `{ mensagem, motivo: "alergia" }` quando desfazer traria de volta um alimento com um chip de alergia marcado depois da troca: `"Não dá para voltar a \"Leite integral\": ele tem uma das suas alergias (Leite e derivados). Escolha outro substituto em Trocar."` (nada é gravado; pelo `motivo`, a tela mostra a mensagem sem recarregar a dieta)

### `DELETE /api/dietas/atual/trocas?dietaId=&revisao=&dia=&refeicao=&item=&escopo=`
"Voltar ao original": o item volta ao primeiro original, só ele (`escopo=refeicao`) ou, na semana, todos os itens com o mesmo alimento que vieram do mesmo original (`escopo=semana`).
**Devolve:** a `DietaResponse` com a revisão nova, `alergiasNaDieta` e `marcacoesRemovidas`. Pode ser desfeito como uma troca.
**Erros:** **400** `"Escopo inválido."`; **400** `"Este alimento não foi trocado."`; **409** a mensagem de dieta mudada; **422** a mesma mensagem do "Desfazer", com `motivo: "alergia"`, quando o original tem um chip de alergia marcado (nada é gravado)

---

## Lista de compras

A lista é sempre da dieta atual (a mais recente do usuário do token, a mesma regra do `GET /api/dietas/atual`). Ela é calculada na hora a partir da dieta salva e da conversão de `Data/ConversaoCompras.cs` (fontes e método em `docs/compras/README.md` e em `docs/fontes-nutricionais.md`). Só as marcações "já tenho" são gravadas. Nenhuma das duas rotas recebe nem devolve peso, meta, kcal, macros ou objetivo: é o que a tela mostra e o que o "Compartilhar" manda. A exceção, desde a TACO 2c, é a marca de alergia de cada item (`temAlergeno`, `alergias`), que é só para a tela e não deve ir no "Compartilhar".

### `GET /api/dietas/atual/lista-compras`
**Recebe:** nada
**Devolve:**
- `dietaId` (o id da dieta de onde a lista saiu, que o `PUT` manda de volta), `dataGeracao` (UTC; a tela mostra no fuso da UF), `versaoConversao` (informativa: a lista é recalculada a cada consulta, nada dela é gravado; 2 desde as embalagens, em 2026-11-16)
- `disponivel`: falso nas dietas geradas antes da lista fechada da base nutricional (algum alimento sem `AlimentoId`) ou com um item que a base não tem mais. Nesse caso vem `motivo: "dieta-antiga"` e `secoes` vazio: a lista não adivinha o item pelo nome
- `avisoOleo`: a dieta tem refogado ou ovo frito (a TACO já conta o óleo do preparo no prato, e ele não vira item da lista)
- `secoes`, na ordem do mercado (`hortifruti`, `acougue-e-peixaria`, `padaria`, `frios-laticinios-e-ovos`, `mercearia`, `bebidas`, `congelados`), só as que têm item, cada uma com `chave`, `nome` e `itens`. Cada item:
  - `chave` (o identificador estável do item de compra, ex.: `arroz-branco`, `ovos`), `nome` (com o nome regional pela UF da conta: Aipim no RJ, Macaxeira no Norte e Nordeste)
  - `quantidade` e `unidade` (`g`, `ml`, `unidades`, `fatias` ou `saches`), já arredondadas **para cima**: contáveis na próxima unidade inteira; gramas e mililitros em passos de 10 até 100 e de 50 acima disso, nunca zero
  - `texto`: a quantidade pronta para mostrar ("7 bananas", "550 g", "1,25 kg", "1,05 L", "14 sachês")
  - `embalagem` (desde 2026-11-16, **opcional**: `null` quando não se aplica): o que se compra em embalagens, sem mudar `quantidade`, `unidade` nem `texto` (que continuam sendo a conta e o que o "já tenho" compara). Calculada sobre a quantidade **exata** (`detalhe.quantidadeExata`, antes do arredondamento): para cada tamanho do item, o menor número de embalagens que cobre o exato; ficam só os tamanhos que precisam de até 4 embalagens, e entre eles o de menor total e, no empate, o de menos embalagens. Tamanhos diferentes não se combinam. Campos: `tipo` (`embalagem`, ou `peca` no frango inteiro abaixo de 1 kg: a peça tem peso variável, de 1 kg ou mais, e a sobra é o mínimo), `texto` ("1 pacote de 1 kg", "2 latas de 350 ml", "3 dúzias", "1 peça de 1 kg ou mais"), `quantidade` (1 a 4), `nome` (o nome da embalagem já no número certo: "pacote", "pacotes", "dúzias", "peça") e `rotulo` (o tamanho: "1 kg", "350 ml", "1 kg ou mais"; vazio na dúzia), as duas partes do `texto` (`texto` = "{quantidade} {nome}" + " de {rotulo}"), para a tela mostrar a contagem e o tamanho em duas camadas, `tamanhoTotal` (na unidade da linha: 1000 no "1 pacote de 1 kg", 16 fatias no pacote de pão de forma de 400 g), `sobra` (`tamanhoTotal` − exato, 2 casas; não é guardada), `sobraTexto` (para "sobram cerca de …": gramas e ml nos 10 mais próximos, contáveis para baixo; na peça, para baixo, porque a tela diz "sobram pelo menos …"; vazio sem sobra) e `fonte` (de onde vem o tamanho, para o "Ver detalhe"). Vem `null` nos itens por peso (hortifrúti, açougue), contáveis, prontos, nos itens que se compram embalados mas ficaram fora da v1 (motivo de cada um em `docs/compras/embalagens.csv`) e quando passaria de 4 embalagens. Tamanhos e método: `docs/fontes-nutricionais.md`, seção "Embalagens"
  - `pronto` (a quantidade é do prato pronto, sem fonte para converter: refogados e suco de laranja), `nota` (aviso que vai na própria linha, ex.: "de carne, sem osso e sem pele · compre um pouco mais"), `tenho` (marcado como "já tenho" nesta dieta)
  - `temAlergeno` e `alergias` (TACO 2c): algum item da dieta que entra na linha tem um chip de alergia marcado pela pessoa, e quais (códigos, na ordem dos chips). O item não some da lista: a tela diz "tem soja: troque no painel"
  - `detalhe`: `quantidadeExata` (antes do arredondamento, 2 casas), `origens` (cada item da dieta que entra na linha: ovo cozido, ovo frito e clara entram em "Ovos"), com `alimentoId`, `nome`, `quantidadeNaDieta` (soma da semana), `medida`, `fator`, `convertido`, `alergias` (TACO 2c: os chips marcados que tiram aquele alimento, para o "Ver detalhe" dizer qual trocar; a linha soma os de todas as origens) e `passos [{ tipo, descricao, fator }]` (`tipo` = `par-cru-cozido`, `descarte`, `unidade`, `receita`, `calibracao` ou `derivacao`; a `descricao` traz o código e a página da fonte), e `observacoes` (ex.: "Sobram as gemas.")
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

## Refeições feitas

"Comi esta refeição do plano": um toque por refeição, gravado na conta (tabela `RefeicoesFeitas`). A marcação diz só isso; a falta dela não diz se a pessoa pulou, comeu outra coisa ou esqueceu. Vale sempre para a dieta atual (a mais recente do usuário do token, a mesma regra do `GET /api/dietas/atual`), também para as dietas que a semana da casa criou (`origem: casa`): a marcação é pessoal, não aparece para os outros membros nem entra na lista da casa. Dá para marcar **hoje e ontem**, no fuso da UF da conta (`FusoHorarioEstado`), e nunca antes do dia local em que a dieta foi gerada (na dieta gerada hoje, só hoje). Separado do `GET /api/dietas/atual` de propósito: a resposta dele é reaproveitada nas trocas. Marcar não sobe a `revisao` da dieta (uma folha de troca aberta em outra aba não leva 409 por causa de um toque), e a troca de alimento não apaga a marcação (a posição da refeição não muda). Dieta nova começa sem marcações; as da dieta anterior continuam no banco (sem chave estrangeira para a `DietaIA`, como a `MetasDieta`) até o prazo de 35 dias da limpeza automática. Hoje a marcação não entra na meta adaptativa (decisão de 2026-11-16: decidir com 3 a 4 semanas de dados, `docs/lacunas-conhecidas.md`). As duas rotas exigem o consentimento geral (403 `motivo: "consentimento"`, como as outras rotas logadas). **429** `{ "mensagem": "Muitas marcações em pouco tempo. Espere um minuto e tente de novo." }` acima de 120 leituras ou de 60 marcações por minuto do mesmo usuário (em memória).

### `GET /api/dietas/atual/refeicoes-feitas`
**Recebe:** nada
**Devolve:** `dietaId` (o que o `PUT` manda de volta), `hoje` (`AAAA-MM-DD`, o dia de hoje no fuso da UF: o painel abre nele) e `dias`, só as datas marcáveis, da mais antiga para a mais nova (hoje sempre vem, mesmo sem marcação), cada uma com `data` (`AAAA-MM-DD`), `indiceDia` (a posição do dia em `dieta.dias`, casada pelo **nome** do dia da semana, porque a lista da IA pode vir em outra ordem; nulo se a dieta não tem esse dia) e `marcadas` (as posições das refeições marcadas como feitas nesse dia, em `dieta.dias[indiceDia].refeicoes`, em ordem)
**Erros:** 404 `{ "mensagem": "Nenhuma dieta gerada ainda." }` sem dieta.

### `PUT /api/dietas/atual/refeicoes-feitas/{data}/{refeicao}`
Marca (`feita: true`) ou desmarca (`feita: false`) a refeição na posição `{refeicao}` do dia `{data}` (`AAAA-MM-DD`). Idempotente: marcar o que já está marcado, ou desmarcar o que não está, dá 204 do mesmo jeito. Desmarcar apaga a linha (é o "desfazer" do toque, não um histórico). As kcal da refeição e a quantidade de refeições do dia são lidas da dieta no servidor, no momento do toque, e gravadas junto (a troca de alimento muda as kcal depois).
**Recebe:** `dietaId` (a dieta que estava aberta na tela), `feita` (booleano)
**Devolve:** 204 sem corpo
**Erros** (corpo sempre `{ "mensagem" }`):
- **400** `"Data inválida. Use o formato AAAA-MM-DD."`: `{data}` mal formada ou inexistente
- **400** `"Refeição não encontrada na dieta."`: `{refeicao}` fora das refeições daquele dia na dieta
- **400** `"Informe a dieta."` ou `"Informe se a refeição foi feita."`: falta `dietaId` ou `feita` no corpo
- **404** `"Nenhuma dieta gerada ainda."`: o usuário não tem dieta
- **409** `"Sua dieta mudou desde que a tela foi aberta. Atualize e tente de novo."`: o `dietaId` não é o da dieta atual (outra dieta foi gerada com a tela aberta, por exemplo em outro aparelho ou pela semana da casa). Um id de dieta de outra pessoa também cai aqui, e nada é gravado
- **422** `"Só dá para marcar as refeições de hoje e de ontem da dieta atual."`: data no futuro, anterior a ontem, ou anterior ao dia em que a dieta foi gerada

Dois toques ao mesmo tempo na mesma refeição (PC e celular) são barrados pelo índice único (`UsuarioId`, `Data`, `DietaIAId`, `RefeicaoIndice`); só a violação de chave única do PostgreSQL é tratada como "já marcada" (204), como na lista de compras.

---

## Metas (simulação)

### `POST /api/metas/simulacao`
Calcula a meta da fórmula sem gravar nada e sem chamar a IA, com as mesmas classes da geração da dieta. Sem o ajuste adaptativo (que depende do histórico).
**Recebe (tudo opcional; o que não vier usa o dado salvo):** `sexo` (`Masculino` ou `Feminino`), `altura` (cm, 100 a 250), `dataNascimento` (idade de 18 a 100 anos), `peso` (kg, 20 a 300; ausente = peso de tendência salvo), `objetivo` (`perder peso`, `manter peso` ou `ganhar peso`; ausente = o do registro mais recente), `nivelAtividade` (`sentado`, `em_pe`, `andando` ou `bracal`; ausente = o salvo, e sem nível salvo o fator 1,4 de antes da pergunta), `modalidades [{ modalidadeId, diasSemana, duracaoMediaHoras, intensidade }]` (mesmas regras do `POST /api/usuarios/modalidades`, sem repetir modalidade; se vier, mesmo vazia, substitui a lista salva; a `intensidade` ausente usa a salva daquela modalidade e, sem vínculo salvo, `normal`)
**Devolve:** `versaoFormula`; `entradas { sexo, altura, dataNascimento, idade, peso, pesoOrigem ("informado" ou "tendencia"), objetivo }`; `gastoRepouso { total, peso, altura, idade, sexo }` (as parcelas de Mifflin-St Jeor, que somam o total pelo maior resto); `atividadeForaDoTreino { fator, nivel, kcal }` (`nivel` nulo = sem resposta, fator 1,4); `ritmoEsperadoKgSemana`; `modalidades [{ modalidadeId, nome, metReferencia, intensidade, metEfetivo, duracaoHoras, diasSemana, kcalSessao }]` (`metReferencia` é o MET de catálogo, o do ritmo "normal", mantido para as telas de antes; `metEfetivo` é o MET da conta pelo ritmo da aula, e `kcalSessao` = (`metEfetivo` − 1) × peso × horas: a tela deve mostrar a conta com o `metEfetivo`); `dias` (sempre 7, de 1 = segunda a 7 = domingo) com `diaSemana`, `treinoKcal`, `manutencao`, `objetivoKcal`, `tetoDeficitAplicado` e `metaFormula`. Valores em kcal inteiras: cada parte é arredondada uma vez e todo total é a soma das partes mostradas
**Erros:** 400 `{ mensagem }` para valor fora do conjunto ou da faixa e quando falta um dado que também não está salvo ("Informe seu peso." etc.); 404 se uma modalidade não existe.

---

## Consentimento

O texto de cada tipo de consentimento fica no código, por versão (`Services/Consentimentos/TextosConsentimento.cs`); a vigente é a maior versão do tipo, e o histórico do git guarda o que cada versão dizia. Existem três tipos: `geral` (dados de saúde, obrigatório para usar o Ronu), `alergias` (TACO 2c, v1: só para gravar chips de alergia; o aceite é gravado pelo `PUT /api/perfil/alergias/{codigo}` junto com o chip) e `casa` (plano família, v1: dividir com as pessoas da casa o que a casa precisa; o aceite é gravado pelo `POST /api/casa` ou pelo `POST /api/casa/convites/aceitar`, e sair da casa o revoga). Mudar o que um texto publicado diz pede uma versão nova, e aí todo mundo aceita de novo no próximo acesso (o aceite vale só se é da versão vigente e não foi revogado). A tela sempre mostra o texto que vem da API e manda de volta a versão que mostrou: o texto lido e a versão gravada não têm como divergir.

### `GET /api/consentimentos/vigente/{tipo}`
Sem login: é o que a caixa do cadastro mostra, antes de a conta existir.
**Devolve:** `tipo`, `versao`, `titulo`, `texto`. Nenhum dado de conta
**Erros:** 400 "Tipo de autorização inválido."; **429** `{ mensagem }` acima de 60 leituras por minuto do mesmo IP.

### `GET /api/consentimentos/{tipo}`
**Devolve:** `tipo`, `versaoVigente`, `titulo`, `texto`, `aceito` (o aceite mais recente ainda não revogado, de qualquer versão: `{ versao, aceitoEm }`; nulo se nunca aceitou) e `precisaAceitar` (sem aceite da versão vigente). Responde do mesmo jeito com `Consentimento:Exigir` desligado: o interruptor só decide se a API barra
**Erros:** 400 "Tipo de autorização inválido."

### `POST /api/consentimentos`
**Recebe:** `tipo`, `versao` (a que a tela mostrou)
**Devolve:** 204 sem corpo. A data é a do servidor e o usuário vem do token. Aceitar de novo a versão já aceita não duplica (e duas abas ao mesmo tempo esbarram no índice único parcial, tratado como já aceito)
**Erros:** 400 "Tipo de autorização inválido." ou campo faltando; 400 "A autorização das alergias é dada ao marcar a primeira alergia." com `tipo` = `alergias` (esse aceite só nasce junto com o primeiro chip, no `PUT /api/perfil/alergias/{codigo}`; por aqui ficaria um aceite de dado de saúde sem alergia nenhuma, que nada revogaria); 400 "A autorização da casa é dada ao criar a casa ou ao aceitar um convite." com `tipo` = `casa` **para quem não está numa casa** (pelo mesmo motivo: um aceite sem casa, que sair da casa nunca revogaria); **409** `{ mensagem: "O texto da autorização mudou. Leia de novo e aceite a versão nova.", versaoVigente }` se a versão não é a vigente (com `tipo` = `casa`, também `motivo: "consentimento_casa"`, o mesmo das rotas da casa); **429** acima de 10 aceites por minuto do mesmo usuário.

Não há rota para revogar: revogar o consentimento geral é apagar a conta (`DELETE /api/conta`), porque sem ele o Ronu não funciona. O de `alergias` é revogado quando a pessoa tira o último chip (`DELETE /api/perfil/alergias/{codigo}`): as alergias saem e o aceite ganha `RevogadoEm`.

---

## Conta (direitos do titular)

As três rotas exigem login, mas não o consentimento: valem também para quem nunca aceitou ou quer revogar (LGPD, art. 18).

### `GET /api/conta`
**Devolve:** `temSenha`, só isso: diz se a exclusão se confirma com a senha ou com a frase. Existe à parte do `GET /api/perfil` porque a tela de consentimento precisa dele, e lá o perfil ainda responde 403
**Erros:** 404 se a conta não existe mais.

### `GET /api/conta/exportacao`
"Baixar meus dados" (art. 18, II e V).
**Devolve:** um arquivo JSON (`application/json`, anexo `ronu-dados-AAAA-MM-DD.json`, com `Cache-Control: no-store`), formato `ronu-exportacao-v1`: `geradoEm`, `conta` (nome, email, UF, datas, se tem senha e se tem Google vinculado, com o `googleId`), `perfil` (com os horários de acordar e dormir), `pesagens`, `modalidades` (com a hora de início e o ritmo da aula, `intensidade`), `preferenciasAlimentares`, `alergias` (`codigo`, `marcadoEm`), `dietas` (com o cardápio como JSON e a `origem`: `pessoa` ou `casa`, a dieta que a semana da casa criou, com o almoço e o jantar da casa na porção da pessoa), `metasDieta`, `listaComprasMarcacoes`, `refeicoesFeitas` (`data`, `dietaId`, `refeicao` (a posição no dia), `kcal`, `refeicoesNoDia`, `marcadaEm`), `consentimentos` (todos os tipos, com `revogadoEm`), `pedidosRedefinicaoSenha`, `casa` (`papel`, `entrouEm`, `transferenciaOferecidaEm`; nulo sem casa, e nada dos outros membros) e `convitesCasa` (os convites que a pessoa gerou: `criadoEm`, `expiraEm`, `usadoEm`, `revogadoEm`). Montado campo a campo, nunca serializando as entidades: ficam de fora o hash da senha, o hash do token de redefinição e o do convite, e um campo novo no modelo só entra se alguém o escrever em `ExportacaoConta`
**Erros:** 404 "Conta não encontrada."; **429** acima de 5 exportações por hora do mesmo usuário.

### `DELETE /api/conta`
Apaga a conta e tudo dela (art. 18, VI), também usado para revogar o consentimento.
**Recebe:** conta com senha: `senha`; conta só com Google: `confirmacao` com a frase `apagar minha conta` (sem diferenciar maiúsculas, ignorando espaços nas pontas)
**Devolve:** 204 sem corpo, também se a conta já tinha sido apagada (outra aba). `ExclusaoConta` apaga cada tabela com `UsuarioId` explicitamente, numa transação (tudo ou nada). Com casa: o membro sai; o dono sozinho leva a casa junto; o dono com membros não precisa transferir antes, a casa passa sozinha ao membro mais antigo (`EntrouEm`), e os convites que ele gerou saem com a conta, com a cascata do PostgreSQL como segunda garantia; um teste por reflexão falha se uma tabela nova com `UsuarioId` não entrar na lista. Depois disso, todo token da conta responde 401. O log guarda só o id. As cópias de segurança do banco seguem o prazo de `docs/privacidade.md`
**Erros:** 400 "Digite sua senha para confirmar.", "Senha incorreta." ou `Digite "apagar minha conta" para confirmar.` (400, não 401: o 401 faria a tela encerrar a sessão por uma senha errada); **429** acima de 5 tentativas em 15 minutos do mesmo usuário.

### Limpeza automática (sem rota)
O `ServicoLimpezaRetencao` roda a `LimpezaRetencao` 1 minuto depois de a API subir e a cada 24 horas (no App Service F1 a API dorme sem uso, então roda pelo menos a cada subida). Ela apaga:
- pedidos de redefinição de senha com mais de 30 dias;
- convites da casa usados, vencidos ou revogados há mais de 30 dias;
- pedidos e avisos da casa com mais de 30 dias (rede de segurança: eles já saem ao ser dispensados, atendidos ou quando chega uma semana nova);
- refeições marcadas como feitas com mais de 35 dias (a janela da meta adaptativa é de 28 dias, mais uma folga), pela `data` da marcação;
- contas com mais de 30 dias que nunca aceitaram nenhuma versão do consentimento e não têm nenhum dado além de nome e email (estar numa casa ou ter uma refeição marcada como feita conta como dado). Contas com `CriadoEm` nulo (anteriores ao campo) nunca entram.

---

## Casa (plano família)

Etapa 1: formar a casa (até 6 pessoas, um dono). Etapa 4: a semana da casa. Etapa 5: os pedidos, a troca na casa e a entrada e a saída no meio da semana. Etapa 6: a lista de compras da casa e o "já tenho" da casa (abaixo). Todas as rotas exigem login e o consentimento geral (como as outras). Regras comuns:
- `usuarioId` sempre do token. Quem não está numa casa recebe **404** `"Você não está em nenhuma casa."` em toda rota de membro, mesmo passando o id de algo de outra casa: a resposta não revela casa nenhuma. Membro em rota de dono recebe **403** `"Só quem cuida da casa pode fazer isso."`.
- As rotas que mostram ou mudam a casa exigem também o aceite `casa` vigente: sem ele, **409** `{ mensagem, motivo: "consentimento_casa", versaoVigente }`. Sair (`DELETE /api/casa/eu`) e recusar ou cancelar a transferência não exigem: deixar de dividir os dados não pode depender de aceitar dividir. Sair dispensa até o consentimento geral. Quando o texto da casa ganha uma versão nova, quem já está numa casa aceita pelo `POST /api/consentimentos` com `tipo` = `casa` (só para quem está numa casa; o primeiro aceite continua nascendo ao criar a casa ou aceitar o convite), e a aba Casa mostra essa caixa no lugar da casa.
- Sair ou ser removido apaga a linha em `MembrosCasa`, preenche o `RevogadoEm` do aceite `casa` e solta as dietas da pessoa da casa (etapa 5, abaixo).
- Sobre os outros, nenhuma resposta leva e-mail, sobrenome, idade, peso, altura, sexo, objetivo, meta, treino, porção, restrição com dono ou dado de saúde. Os pedidos não dizem quem pediu.
- **429** `{ mensagem }`: gerar convite acima de 10 por hora do mesmo usuário; verificar e aceitar convite (contagem única) acima de 20 por hora do mesmo IP; pedidos acima de 10 por hora do mesmo usuário (`"Você já fez muitos pedidos na última hora. Tente de novo mais tarde."`); as demais escritas acima de 30 por minuto do mesmo usuário. A troca na casa usa os limites da troca individual (120 leituras e 30 gravações por minuto).

**`CasaResponse`:** `souDono`, `membros [{ membroId, primeiroNome, dono, souEu, transferenciaPendente }]` (ordem de entrada; `primeiroNome` é só a primeira palavra do nome), `orcamento` (`economico`, `moderado`, `sem_restricao` ou nulo), `restricoes { naoEntra, evitamosQuandoDa }` (abaixo, igual para todos os membros) e, só para o dono (nulos para os membros): `convitesAbertos [{ id, expiraEm }]`, `pedidos` (os pedidos e avisos abertos, abaixo) e `pedidosAbertos` (a contagem, para o contador da aba Casa). O membro não vê nem a contagem: ela diria a ele que outra pessoa pediu algo. A transferência pendente aparece na linha do escolhido (`transferenciaPendente`): o dono mostra "Aguardando Bruno aceitar", e o escolhido, a faixa de aceitar ou recusar.

**`restricoes`** (complementos da etapa 7, shape S2; `RestricoesDaCasa`): `naoEntra` junta o `nao_como` e as alergias de todos os membros; `evitamosQuandoDa`, o `evitar` de todos, sem o que já está em `naoEntra`. Listas de nomes (strings), sem repetição e em ordem alfabética (pt-BR), para a ordem não seguir membro, chip nem data. Cada nome é o de um item da base (o regional da UF do dono, o mesmo do cardápio da casa) ou, quando todos os itens de uma categoria saem, o da categoria ("Peixes", "Carne bovina"). Nunca vão: o texto que a pessoa escreveu (o que não casou com a base fica de fora, como fica de fora da lista da casa), o nome de um chip de alergia, quem marcou, nem se veio de alergia ou de "não como" (para leite, ovo, soja, glúten, amendoim e castanhas, o casamento do texto e o chip tiram os mesmos itens). Os itens que a casa nunca usa (bebida alcoólica, café e chás, os refogados com versão sem óleo) não entram. Casa sem restrição: as duas listas vazias.

### `GET /api/casa`
**Devolve:** `CasaResponse`
**Erros:** 404 sem casa; 409 `consentimento_casa`.

### `POST /api/casa`
Cria a casa com a pessoa como dono e grava o aceite `casa` junto (não há outro caminho para ele).
**Recebe:** `versaoConsentimento` (a do texto que a tela mostrou, lido do `GET /api/consentimentos/vigente/casa`), `orcamento?`
**Devolve:** 201 `CasaResponse`
**Erros:** 400 "Orçamento semanal inválido."; 409 `ja_em_casa` (também quando duas abas criam ao mesmo tempo: o índice único em `MembrosCasa.UsuarioId` barra a segunda); 409 `consentimento_casa` com `versaoVigente` se a versão falta ou não é a vigente.

### `PUT /api/casa/orcamento` (dono)
O orçamento da casa vale **só para o almoço e o jantar da casa**: vai ao prompt da semana da casa ("Orçamento semanal da casa para alimentação", com a mesma regra de custo da geração individual). O café e os lanches de cada um seguem o orçamento do perfil da pessoa (`orcamentoSemanal` do `PUT /api/perfil`), na geração individual. Só o dono muda; todos da casa leem no `CasaResponse.orcamento`. Mudar o orçamento não muda a semana atual: vale para a próxima semana gerada.
**Recebe:** `orcamento` (nulo ou vazio = sem orçamento)
**Devolve:** `CasaResponse`
**Erros:** 400 "Orçamento semanal inválido."

### `POST /api/casa/convites` (dono)
**Devolve:** 201 `{ link, expiraEm }`. O link é `{Frontend:UrlBase}/convite.html#convite=<token>` (endereço da configuração, nunca dos cabeçalhos; token no fragmento, que não chega a servidor nenhum). Token de 32 bytes aleatórios em Base64Url; o banco guarda só o SHA-256. Vale 7 dias e uma vez. Gerar outro não invalida os anteriores
**Erros:** 409 `casa_cheia` com 6 pessoas.

### `DELETE /api/casa/convites/{id}` (dono)
Revoga (`RevogadoEm`). **Devolve:** 204, também se já estava revogado ou usado (sem mudar nada)
**Erros:** 404 se o convite não é desta casa.

### `POST /api/casa/convites/verificar`
**Recebe:** `token`
**Devolve:** `{ primeiroNomeDono, membros (a contagem), expiraEm, consentimento: { tipo, versao, titulo, texto } }`. Não usa o convite. `expiraEm` é só o dia (`"2026-11-20"`), no fuso da UF de quem abriu: a hora diria o minuto em que o dono gerou o link (a validade é sempre de 7 dias), e a tela só precisa do "válido até dd/mm"
**Erros, nesta ordem:** 400 `invalido` (não existe ou foi revogado), 400 `usado`, 400 `expirado`; 409 `ja_em_casa` (inclusive a mesma casa); 409 `cadastro_incompleto` (sem data de nascimento, altura, sexo ou nenhum registro de objetivo); 403 `menor_de_18`; 409 `casa_cheia`. Todos como `{ mensagem, motivo }`.

### `POST /api/casa/convites/aceitar`
**Recebe:** `token`, `versaoConsentimento`
**Devolve:** `CasaResponse`. As checagens da verificação são refeitas com a casa travada (`SELECT ... FOR UPDATE`), e o aceite `casa`, a linha em `MembrosCasa` e o `UsadoEm` são gravados numa transação. O uso único é um `UPDATE ... WHERE UsadoEm IS NULL AND RevogadoEm IS NULL AND ExpiraEm > agora` que precisa mudar 1 linha: de dois aceites ao mesmo tempo, só um passa
**Erros:** os da verificação; 409 `consentimento_casa` com `versaoVigente` (versão ausente ou velha; o convite não é usado).
**Entrada no meio da semana (etapa 5):** depois do aceite gravado, se a casa tem uma semana da versão da base atual, a pessoa recebe na hora uma `DietaIA` nova (`Origem = casa`, `SemanaCasaId`), sem chamar o Gemini: as porções dela no almoço e no jantar da casa (`PorcoesCasa`, a mesma conta da semana nova) e o café e os lanches que ela já tinha, reajustados para o que sobra da meta. Uma refeição da casa com algo que ela não come ou tem alergia vira individual só para ela: fica o almoço ou o jantar que ela já tinha naquele dia (sem os itens que ela não come), ou nada até ela gerar o café e os lanches. O dono recebe um aviso sem nome (`aviso_entrada`, abaixo), e a semana marca o `refeicaoIndividual`. Se o cálculo falhar, a entrada vale do mesmo jeito (o erro vai só para o log, com o id da casa).

### `DELETE /api/casa/membros/{membroId}` (dono)
Tira alguém da casa, com as mesmas regras de sair (as refeições da casa ficam na dieta da pessoa, abaixo). **Devolve:** 204
**Erros:** 400 se for o próprio dono ("Para sair da casa, use a opção Sair."); 404 se não é desta casa.

### `DELETE /api/casa/eu`
Sair. **Devolve:** 204. O dono sozinho leva a casa (e os convites, as semanas, os pedidos e o "já tenho" da lista da casa) junto.
**Quem sai ou é removido (etapa 5):** fica com o almoço e o jantar da casa na própria dieta, com as mesmas comidas e a porção dele, até gerar outra. Em todas as dietas da pessoa, as refeições perdem `daCasa` e `chaveCasa`, a dieta perde o `SemanaCasaId` e a `revisao` sobe (uma tela aberta recarrega). A troca individual volta a valer nelas. A lista da casa, as opções de troca e o que mais depender da casa passam a ser calculados sem ela
**Erros:** 409 `{ mensagem: "Passe a casa para outra pessoa antes de sair.", motivo: "transferir_antes" }` para o dono com membros.

### `POST /api/casa/transferencia` (dono)
**Recebe:** `membroId`. Oferece a casa; uma oferta por vez (oferecer a outro desfaz a anterior). **Devolve:** 204
**Erros:** 400 se for o próprio dono; 404 se não é desta casa.

### `POST /api/casa/transferencia/aceitar`
O membro escolhido vira dono, e o antigo dono vira membro. A oferta é conferida de novo com a casa travada (um cancelamento no meio vence). Os pedidos abertos passam a aparecer para o novo dono; o antigo passa a pedir. **Devolve:** `CasaResponse`
**Erros:** 409 "Não há nenhuma casa sendo passada para você."

### `DELETE /api/casa/transferencia`
O dono cancela a oferta, ou o escolhido recusa. **Devolve:** 204 (sem oferta, não muda nada)
**Erros:** 403 para um membro que não é o escolhido.

### Semana da casa (etapa 4)

Uma chamada ao Gemini por semana da casa, só com: a lista da casa, o peso de uma unidade dos contáveis, os itens leves pelo nome da lista ("alguém da casa prefere evitar", sem dizer quem), as regras do cardápio, o orçamento da casa e uma **porção de referência fixa** de 650 kcal no almoço e 550 no jantar (calibração do projeto, decisão D5). **Não vão:** meta, peso, idade, sexo, altura, objetivo, treino, UF, nome ou e-mail de ninguém, nem o texto das preferências (nem os trechos sem casamento: o enum só deixa escolher itens da lista).
- **Lista da casa** (`ListaDaCasa`): a interseção, por `AlimentoId`, das listas de todos os membros, cada uma pelo `ListaDaPessoa`. O `nao_como` e as alergias de qualquer um tiram o item para todos (com a regra cruzada amendoim/castanhas); o `evitar` de qualquer um fica como leve (no máximo 2 vezes na semana). Sem bebida alcoólica nem bebida com cafeína. Os nomes regionais são os da UF do dono. Os mínimos por papel valem sem a fruta (proteína, carboidrato e hortaliça).
- **Checagens da resposta:** 7 dias, cada um com exatamente um `Almoço` e um `Jantar` (o nome também vai em enum); alimentos da lista; nenhum item com alérgeno da casa (mesmo na última tentativa, vira 503: a alergia nunca é relaxada); no máximo 2 **misturas** por refeição (carnes, aves, peixes e ovos; feijão e leguminosas não contam, decisão D1): na última tentativa, ficam as 2 de maior quantidade; leves acima de 2 na semana: na última, trocados pelo melhor substituto; azeite tirado de refeição com refogado; castanha-do-pará em até 8 g por dia e em até 2 dias (`LimiteCastanhaDoPara`, como na geração individual, antes das contas pela base; uma refeição que ficasse vazia com o corte vira nova tentativa, e na última, 503).
- **Porções** (`PorcoesCasa`, sem IA): o alvo de cada pessoa é a meta do dia dela × 30,7% no almoço e × 23,2% no jantar (calibração do projeto, medida no g9c). O prato da referência é escalado para as kcal do alvo, na mesma proporção, até a porção máxima da base, e depois passa pelo `AjusteQuantidades` só nessa refeição. A castanha-do-pará só desce com a escala, nunca sobe (fica no máximo na quantidade da referência, que já passou pelo teto do dia): a porção máxima de 8 g segura cada item, mas não a soma do almoço e do jantar de quem tem meta alta. Só o ajuste (50% a 150%) não alcança porções muito diferentes.
- **Uma `DietaIA` nova para cada membro** (`Origem = casa`, `SemanaCasaId`): as porções dele nas refeições da casa (`daCasa: true`, `chaveCasa: "seg-almoco"`), e o café e os lanches que ele já tinha (da dieta atual, só os itens que ainda estão na lista dele), reajustados em C# para o que sobra da meta (decisão D6). Antes do reajuste, a castanha-do-pará desse café e desses lanches passa pelo teto contando a das porções da casa no mesmo dia e nos dias da semana (o corte cai só nas individuais). Cada uma grava a sua `MetaDieta`, com a meta cheia do dia, como uma geração individual: a regra das 3 dietas e a meta adaptativa continuam valendo. O aviso de lista pobre da casa não vai para a dieta de ninguém.
- A casa guarda a semana atual e a anterior (`SemanasCasa`). Apagar a casa apaga as semanas; as dietas dos membros ficam como estão.

**`SemanaCasaResponse`:** `semanaId`, `revisao`, `geradaEm`, `dias [{ diaSemana, almoco, jantar }]`, cada refeição com `chaveCasa`, `horario` e `alimentos` (**só os nomes**, sem quantidade, sem kcal e sem a contagem dos contáveis: a porção de cada um está só na dieta dele). Só para o dono: `avisoListaPobre` (como o da dieta) e `refeicaoIndividual` (verdadeiro quando alguém teve uma refeição da casa trocada por uma individual, sem dizer quem nem qual); nulos para os membros. Etapa 5, só para o dono e fora do JSON quando não há: `trocados` em cada refeição (as posições dos itens trocados na casa, para a marca "trocado" e o "Voltar ao original") e `podeDesfazer` (verdadeiro quando a última troca na casa ainda é a última gravação).

### `POST /api/casa/semana` (dono)
**Devolve:** `SemanaCasaResponse`
**Erros:** 400 "Complete seu perfil (altura, sexo e data de nascimento) e registre um objetivo antes de gerar a semana da casa."; 400 quando a casa fica sem um papel do prato ("Com o Não como e as alergias de todos da casa juntos, não sobrou nenhuma hortaliça para o almoço e o jantar da casa. …", sem dizer de quem; nenhuma chamada ao Gemini); 429 no limite de 3 por hora do dono (abaixo); 503 "Não foi possível gerar a semana da casa agora. …" (nada é gravado).

### `GET /api/casa/semana`
**Devolve:** `SemanaCasaResponse` da semana atual
**Erros:** 404 "A casa ainda não tem uma semana gerada."

### Quem está numa casa e o `POST /api/dietas/gerar`
- Com uma semana da casa da versão da base atual, a pessoa (dono ou membro) gera **só as refeições individuais**: o prompt leva a meta de cada dia já sem as porções da casa ("monte: café da manhã e lanches") e a regra de pelo menos 3 refeições por dia; as regras 15 (5 refeições) e 16 (gordura no almoço e no jantar) não valem. Um almoço ou jantar na resposta num dia em que a refeição é da casa recusa a 1ª resposta; na última, sai. O ajuste de quantidades mexe só nas individuais; as porções da casa entram prontas. O teto da castanha-do-pará das individuais conta a castanha das porções da casa (até 8 g no dia somando as duas, e no máximo 2 dias na semana, com os da casa primeiro). A dieta é salva com `Origem = pessoa` e o `SemanaCasaId`.
- Uma refeição da casa com algo que a pessoa marcou depois (não come ou tem alergia) vira individual só para ela ("monte: café da manhã, lanches e almoço"), e a semana da casa ganha o `refeicaoIndividual` para o dono.
- Sem semana da casa (ou de outra versão da base), a geração é a de sempre.
- **Limite de 3 por hora:** a dieta que a semana da casa cria para um membro não gasta a cota dele. Para o dono, contam todas (as dele e as que a casa criou para ele), e a semana da casa e a geração individual dividem a mesma cota.

### Pedidos e avisos (etapa 5)

Os membros têm "Pedir nova semana" e "Pedir troca" (de um item de uma refeição da casa). Só aviso na aba Casa, com o contador para o dono: sem e-mail, sem notificação. **O pedido não guarda nem devolve quem pediu**: a tabela `PedidosCasa` não tem `UsuarioId` nem nenhum marcador derivado da pessoa (decisão do plano). Sem o autor, o abuso fica contido por: o limite de 10 pedidos por hora por usuário (em memória, nunca no banco); um pedido igual a um já aberto (mesma semana, tipo, refeição e alimento) não vira linha nova, e a resposta é a mesma, para quem pede não descobrir que outra pessoa já pediu; e um teto de 20 pedidos abertos por casa. O "Pedido enviado" (desabilitado até a semana mudar) fica na tela do membro, guardado pela `semanaId` da resposta: guardar no servidor "quem já pediu" seria guardar o autor.

O pedido sai (apagado, sem histórico) quando o dono o dispensa, quando uma semana nova é gerada (todos) e, na troca, quando o dono troca aquele alimento naquela refeição. A limpeza automática apaga os que sobrarem depois de 30 dias.

**`PedidoCasaResponse`** (em `CasaResponse.pedidos`, só para o dono, na ordem de chegada): `id`, `tipo` (`nova_semana`, `troca` ou `aviso_entrada`), `semanaId` (nula se a casa não tinha semana), `chavesCasa` (na troca, a refeição do item; no aviso, as refeições que viraram individuais), `item`, `alimentoId` e `alimento` (só na troca, para o "Ver substitutos") e `texto`, a frase pronta, sempre com "Alguém": `"Alguém pediu uma semana nova."`, `"Alguém pediu trocar ovo de galinha cozido no jantar de quarta."`, `"Alguém que entrou esta semana não come um item do jantar de quinta. Essa refeição ficou individual para essa pessoa; uma semana nova resolve."` (com várias: `"... não come um item de 3 refeições da casa (almoço de segunda, almoço de terça e jantar de quinta). Essas refeições ficaram individuais ..."`). **Sem data nem hora**: o horário exato ajudaria a adivinhar quem pediu. O aviso de entrada não diz qual item nem por quê (alergia é dado de saúde).

### `POST /api/casa/pedidos` (membro)
**Recebe:** `tipo` (`nova_semana` ou `troca`), `semanaId` (a que a tela mostrava; obrigatória na troca), `chaveCasa` e `item` (só na troca: a refeição da casa e a posição do item nela, a mesma na semana da casa e na porção de cada um) e `alimentoId?` (o alimento que a tela mostrava; quando vem, precisa ser o que está lá)
**Devolve:** **202** `{ semanaId }`, também quando um pedido igual já estava aberto (nada muda)
**Erros:** 400 `"Você cuida da casa: gere a semana nova ou troque o alimento direto."` para o dono; 400 `"Tipo de pedido inválido."`; 400 `"Alimento não encontrado na refeição da casa."`; 400 com a mensagem de semana mudada (`"A semana da casa mudou desde que a tela foi aberta. Atualize e veja de novo."`) na troca sem `semanaId`; 404 sem semana (só na troca); 409 `"A semana da casa mudou desde que a tela foi aberta. Atualize e veja de novo."` (outra semana, ou outro alimento na posição); 409 `"A casa já tem muitos pedidos abertos. Espere quem cuida da casa responder."`; 429 acima de 10 por hora.

### `DELETE /api/casa/pedidos/{id}` (dono)
Dispensa um pedido ou aviso (apaga). **Devolve:** 204
**Erros:** 404 se não é desta casa (ou já saiu).

### Troca na casa (etapa 5, só o dono)

Só o dono troca um item de uma refeição da casa, e a troca vale para todos (decisão de 2026-10-26). Usa a mesma `CalculadoraTroca` e o mesmo `AplicadorTroca` da troca individual, sobre a semana de referência da casa e com a **lista da casa** de agora (o `nao_como` e as alergias de todos ficam de fora; o `evitar` leve de qualquer um pode ser oferecido, mas por último). A regra dos 5% da referência compara o total do dia da referência. Depois, a porção de cada membro nas refeições afetadas é refeita (`PorcoesCasa`, a mesma conta da semana nova), e o substituto precisa **servir para todos**: a porção refeita de cada um fica a até 5% do alvo da refeição dele (ou não piora o que já estava). O que não serve para alguém sai das opções na contagem `naoServeParaACasa`, sem dizer para quem nem por quê. A semana (`revisao` sobe) e as dietas de todos (a `revisao` de cada uma sobe) são gravadas num SaveChanges só; as marcações "já tenho" que deixaram de valer na lista de cada um caem junto. A refeição que já virou individual para alguém não muda. O item é a posição na refeição da casa: `chaveCasa` + `item`.

Erros comuns: 403 para membro; 404 sem semana; 422 `"A base de alimentos do Ronu mudou desde que esta semana foi gerada. Gere uma semana nova para trocar alimentos."`; 400 `"Alimento não encontrado na refeição da casa."`.

### `GET /api/casa/semana/trocas/opcoes?chaveCasa=&item=` (dono)
**Devolve:** `semanaId`, `revisao`, `chaveCasa`, `item`, `original`, `nutriente`, `ocorrenciasNaSemana`, `foraPelasRestricoesDaCasa` (quantos do grupo a lista da casa tirou, o "não como" e as alergias de todos numa contagem só, sem separar alergia) e `refeicao` e `semana`, no formato da troca individual (`dasPreferencias` sempre vazia: a casa não usa os gostos de ninguém; `parecidos`, `verMais`, `fora` com o motivo a mais `naoServeParaACasa`, `mensagemSemOpcoes`, como `"Nenhum substituto serve para a casa: 2 não servem para a porção de alguém da casa e 3 estão no que a casa não come."`). **Quantidades, kcal, `difKcal` e a prévia dos dias são só as do dono** (a porção e o dia dele): nada da porção de outro membro.

### `POST /api/casa/semana/trocas` (dono)
**Recebe:** `semanaId`, `revisao`, `chaveCasa`, `item`, `alimentoIdAtual`, `alimentoIdNovo`, `escopo` (`refeicao` ou `semana`: todas as vezes em que o item aparece no almoço e no jantar da casa)
**Devolve:** `SemanaCasaResponse` (do dono, com `trocados` e `podeDesfazer`). Os pedidos de troca daquele alimento nas refeições trocadas saem.
**Erros:** 400 `"Escopo inválido."`; 400 `"Este substituto não serve para a casa (a porção de alguém, a meta do dia ou o que alguém não come). Atualize as opções e escolha outro."` (nada é gravado); 409 `"A semana da casa mudou desde que a tela foi aberta. Atualize e tente de novo."` (outra semana, revisão velha, outro alimento na posição, ou uma dieta de membro gravada no meio).

### `DELETE /api/casa/semana/trocas/ultima?semanaId=&revisao=` (dono)
"Desfazer", no padrão da troca individual (`UltimaTroca` na referência): volta a referência e refaz a porção de cada um. **Devolve:** `SemanaCasaResponse`
**Erros:** 409 `"Não há troca para desfazer."` ou a de semana mudada; **422** `{ mensagem: "Não dá para voltar a esse alimento: ele não serve mais para todos da casa. Escolha outro substituto em Trocar.", motivo: "restricao_da_casa" }` quando o que volta foi marcado depois em "não como" ou nas alergias de alguém (sem dizer quem; nada é gravado).

### `DELETE /api/casa/semana/trocas?semanaId=&revisao=&chaveCasa=&item=&escopo=` (dono)
"Voltar ao original" (o item, ou na semana todos os trocados do mesmo jeito). **Devolve:** `SemanaCasaResponse`
**Erros:** 400 `"Escopo inválido."`; 400 `"Este alimento não foi trocado."`; 409 de semana mudada; 422 `restricao_da_casa` como no "Desfazer".

### Lista de compras da casa (etapa 6)

Para quem está numa casa, a página de compras tem duas visões: **a lista da casa** (padrão, rotas abaixo) e **"Só a minha parte"**, que é a lista individual de sempre (`GET /api/dietas/atual/lista-compras`, com as marcações individuais em `ListaComprasMarcacoes`; nada muda nela).

A lista da casa é a **semana inteira de todos os membros**, somada por alimento para uma compra só (decisão de 2026-10-26): o almoço e o jantar da casa com a porção de **cada** membro e o café e os lanches de cada um. É calculada na hora, com o mesmo montador da lista individual (`MontadorListaCompras`: soma por `AlimentoId`, cozido para cru, seções do mercado, nome regional), recebendo as dietas de todos como uma só; o arredondamento para a compra é feito uma vez, sobre o total da casa. Entra a dieta atual de cada membro que seja da semana atual da casa (`SemanaCasaId`); quem entra no meio da semana passa a contar assim que recebe as porções, e quem sai deixa de contar na hora. A dieta de quem ainda não tem a semana atual (por exemplo, um perfil incompleto, que fica sem dieta nova) fica de fora, e a resposta diz só `completa: false`, sem dizer de quem.

**Privacidade:** cada número é o total da casa; nada diz de quem é um item, e nenhuma porção ou item de um membro aparece separado (nem no "Ver detalhe": `quantidadeNaDieta` de cada origem é a soma de todos). Numa casa pequena, dá para deduzir a parte do outro subtraindo a própria, e o consentimento `casa` avisa disso. A **marca de alergia** de cada item (`temAlergeno`, `alergias`, também nas origens) usa **só as alergias de quem está vendo**: o item do café de outra pessoa com um alérgeno de um terceiro fica sem marca, porque marcar, mesmo sem dizer qual, contaria à casa que alguém tem aquela alergia (dado de saúde). O nome regional e o fuso da data são os da UF de quem vê.

### `GET /api/casa/lista-compras` (membro)
**Devolve:** `semanaId` (a semana atual da casa, que o `PUT` manda de volta), `geradaEm` (UTC), `tituloCompartilhar` (`"Lista de compras da casa · semana de 26/10"`, a data da semana no fuso da UF de quem vê: o "Compartilhar" leva só isto e os itens, sem alergia nem nomes), `versaoConversao`, `disponivel` e `motivo` (`"dieta-antiga"` quando alguma dieta somada é anterior à lista fechada), `avisoOleo`, `completa` e `secoes`, no **mesmo formato da lista individual** (`chave`, `nome`, `quantidade`, `unidade`, `texto`, `embalagem`, `pronto`, `nota`, `tenho`, `temAlergeno`, `alergias`, `detalhe`). A `embalagem` é calculada uma vez, sobre o total da casa (uma compra só): por isso não é a soma das embalagens da "Só a minha parte" de cada um. `tenho` é o "já tenho" **da casa**. Não leva `dietaId`, peso, meta, kcal, macros, objetivo, nome nem e-mail de ninguém
**Erros:** 404 sem casa (mesmo de quem não é da casa); 404 "A casa ainda não tem uma semana gerada."; 409 `consentimento_casa`.

### `PUT /api/casa/lista-compras/itens/{chave}` (membro)
Marca (`tenho: true`) ou desmarca (`tenho: false`) um item como "já tenho" **para a casa toda** (decisão D4). Qualquer membro marca e desmarca, e todos veem a mesma marca. A tabela `ListaCasaMarcacoes` (`CasaId`, `SemanaCasaId`, `Chave`, `Quantidade`, `MarcadoEm`) **não tem `UsuarioId`, de propósito: não guarda quem marcou**. Vale para uma semana: semana nova começa sem marcações, e as marcações saem com a semana (a casa guarda 2) ou com a casa. A lista muda sem a semana mudar (alguém gera o café e os lanches, troca, entra ou sai): a marcação guarda a quantidade da lista quando foi marcada, e **cai quando a quantidade de agora passa dela** (a mesma regra da troca individual); se a quantidade voltar a caber (quem entrou saiu), a marca volta. Remarcar renova a quantidade. Idempotente (204 do mesmo jeito); dois toques ao mesmo tempo no mesmo item são barrados pelo índice único (`SemanaCasaId`, `Chave`) e tratados como "já marcado".
**Recebe:** `semanaId`, `tenho` e, para marcar, `quantidade` (a que a tela mostrava)
**Devolve:** 204
**Erros:** 400 `"Item inválido."` (formato da chave); 400 `"Informe a quantidade do item."` (marcar sem `quantidade`); 400 `"Este item não está na lista da casa."`; 400 os de corpo (`"Informe a semana da lista."`, `"Informe se a casa já tem o item."`); 404 sem casa ou sem semana; 409 `consentimento_casa`; 409 `"A semana da casa mudou desde que a lista foi aberta. Atualize a lista e tente de novo."` (outra semana, inclusive a de outra casa, e nada é gravado); 409 `"A lista da casa mudou desde que foi aberta. Atualize a lista e tente de novo."` (a lista pede agora mais do que a `quantidade` que a tela mostrava). Sem limite de requisições próprio, como o "já tenho" individual.

---

## Resumo de convenções aplicadas

- URLs representam **recursos** (substantivos, no plural), não ações
- Verbos HTTP seguem o padrão REST: `GET` para buscar, `POST` para criar, `PUT` para sobrescrever, `DELETE` para remover. Alguns `POST` atualizam o registro existente em vez de duplicar (objetivo do mesmo dia, mesma modalidade, mesmo alimento)
- Dados sensíveis (senha, token) nunca trafegam via query string / URL da API. A única exceção é o link de redefinição de senha, que leva o token no **fragmento** da URL do frontend (`#token=…`): o fragmento não é enviado a servidor nenhum, e a página manda o token à API no corpo de um POST
- `usuarioId` é sempre extraído do token JWT, nunca enviado pelo cliente
- Catálogos do sistema (ex: modalidades) são públicos; dados pessoais exigem autenticação
