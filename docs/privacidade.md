# Privacidade no Ronu (LGPD)

Documento interno, para quem mantém o Ronu. Os textos para o usuário ficam em `docs/textos-legais/` (rascunhos da
política de privacidade e dos termos) e viram as páginas `/privacidade` e `/termos`.

Controlador e encarregado: Matheus Cordeiro Berto. Contato de privacidade: `ronu.appdono@gmail.com` (a conta Gmail do Ronu, a mesma do "esqueci a senha"; decidido em 2026-11-13).

> Rascunho da LGPD base, etapa 3 (2026-11-01). Os marcadores entre colchetes dependem de leitura de configuração da
> Azure ou de decisão do Matheus. Nada desta etapa está publicado.

## 1. Registro das operações de tratamento (art. 37)

Onde fica: "Azure" = App Service `ronu-api` e PostgreSQL Flexible `ronu-db`, grupo `ronu-rg`, região Chile Central.
A conta toda sai junta no "Apagar minha conta" (`ExclusaoConta`), em qualquer linha abaixo que diga "enquanto a conta
existir".

| Dado | Onde no código | Finalidade | Base legal | Onde fica | Quem recebe | Prazo |
|---|---|---|---|---|---|---|
| Nome, e-mail | `Usuario` | conta, login, e-mail de senha | contrato (art. 7º, V) | Azure | Gmail (só o e-mail, no "esqueci a senha") | enquanto a conta existir |
| Hash da senha (BCrypt), `SenhaAlteradaEm` | `Usuario` | login, derrubar sessões após troca | contrato | Azure | ninguém | enquanto a conta existir |
| `GoogleId` | `Usuario` | login com Google | contrato | Azure | ninguém (vem do Google) | enquanto a conta existir |
| UF | `Usuario.Estado` | nomes regionais, fuso do "hoje", regra do feijão | consentimento (art. 11, I), junto do perfil | Azure | Gemini | enquanto a conta existir |
| `CriadoEm`, `UltimoLoginEm` | `Usuario` | prazos de guarda | legítimo interesse (art. 7º, IX) | Azure | ninguém | enquanto a conta existir |
| Altura, sexo, nascimento, nível de atividade | `Usuario` | meta da fórmula | consentimento (art. 11, I) | Azure | Gemini, só de forma derivada: não vão crus, mas a meta diária (kcal e macros) é calculada a partir deles | enquanto a conta existir |
| Rotina (texto livre, antiga) | `Usuario.RotinaDiaria` | antes, horários do cardápio; hoje sem uso: só mostrada para a pessoa apagar (seção 3) | consentimento | Azure | ninguém (saiu do prompt em 2026-11-02) | até a pessoa apagar, ou 60 dias depois da publicação (`LimpezaRetencao`, data em `Retencao:RotinaForaDaTelaEm`) |
| Orçamento (uma de três opções) | `Usuario.OrcamentoSemanal` | custo do cardápio | consentimento | Azure | Gemini | enquanto a conta existir |
| Horários: acordar, dormir, início de cada treino | `Usuario.HorarioAcordar`, `Usuario.HorarioDormir`, `UsuarioModalidade.HorarioInicio` | horários das refeições | consentimento | Azure | Gemini | enquanto a conta existir |
| Pesagens: peso, objetivo, aderência, data | `ObjetivoUsuario` | meta, progresso, meta adaptativa | consentimento | Azure | Gemini, só de forma derivada: peso e objetivo não vão crus, mas a meta diária e a marca de déficit, manutenção ou superávit de cada dia são calculadas a partir deles | enquanto a conta existir |
| Modalidades, dias, duração | `UsuarioModalidade` | gasto do treino | consentimento | Azure | Gemini (só quais dias são de treino e o horário, sem o nome da modalidade) | enquanto a conta existir |
| "Gosto", "não como" e "prefiro evitar" (etapa 3 do plano família: `preferido`, `nao_como`, `evitar`) | `PreferenciaAlimentar` | lista de alimentos e cardápio | consentimento | Azure | Gemini ("gosto": só itens da base que casaram; "não como": só os termos que não casaram com nenhum item, sem as palavras sobre a pessoa ou a saúde dela (`TermoParaIA`); "evitar" leve: os itens da base que casaram, pelo nome da lista, agrupados por chip e sem o texto digitado, e os termos que não casaram, também pelo `TermoParaIA`) | enquanto a conta existir |
| Alergias (8 chips: leite, glúten, ovo, amendoim, castanhas, soja, peixe, crustáceos) | `AlergiaUsuario` (tabela `AlergiasUsuario`) | tirar os alimentos do cardápio e das trocas; marcar na lista de compras e no aviso do painel (TACO 2c) | consentimento específico (art. 11, I), tipo `alergias`, à parte do geral | Azure | ninguém: o Gemini recebe só a lista de alimentos já sem esses itens (nem os códigos, nem a contagem vão ao prompt ou ao log) | até a pessoa desmarcar: sem nenhuma marcada, as linhas saem e o aceite fica revogado; ou enquanto a conta existir |
| Dietas geradas | `DietaIA` | mostrar o cardápio, troca, lista de compras | consentimento | Azure | ninguém | as 3 mais recentes |
| Metas de cada dieta | `MetaDieta` | meta adaptativa | consentimento | Azure | ninguém | enquanto a conta existir (todas) |
| Marcações "já tenho" | `ListaComprasMarcacao` | lista de compras | consentimento | Azure | ninguém | enquanto a dieta estiver entre as 3 |
| Refeições marcadas como feitas: dia (`Data`, no fuso da UF), dieta (`DietaIAId`, sem chave estrangeira), posição da refeição no dia, kcal da refeição e quantas refeições o dia tinha (fotos do momento do toque), `MarcadaEm` | `RefeicaoFeita` (`RefeicoesFeitas`) | painel (o dia de hoje, "3 de 5"), Progresso ("Desde essa pesagem, você marcou X refeições como feitas", só o número, sem total); meta adaptativa só se a etapa 7 for aprovada | consentimento (art. 11, I), o geral | Azure | ninguém: não vai para o Gemini nem aparece para os membros da casa (nem nas dietas que a semana da casa criou) | 35 dias depois da `Data` (`LimpezaRetencao.PrazoRefeicoesFeitas`); sai antes com a conta (`ExclusaoConta`) |
| Aceites de consentimento (tipos `geral`, `alergias` e `casa`) | `Consentimento` | prova do consentimento | obrigação legal / exercício de direitos (art. 7º, II e VI; art. 8º, §2º) | Azure | ninguém | enquanto a conta existir (decidido em 2026-11-01: sem conta, não há o que provar). O de alergias revogado e o da casa revogado ao sair (`OperacoesCasa.RevogarConsentimentoAsync`) continuam, com `RevogadoEm` |
| Casa: orçamento, `CriadaEm` (plano família) | `Casa` (tabela `Casas`) | o almoço e o jantar da casa; o orçamento vale só para eles | consentimento específico (art. 11, I), tipo `casa`, à parte do geral | Azure | membros da casa (o orçamento, só leitura); Gemini (o orçamento, no prompt da casa) | enquanto houver membro; sai com o último (`OperacoesCasa.ApagarCasaAsync`) |
| Participação: papel (`dono`/`membro`), `EntrouEm`, `TransferenciaOferecidaEm` | `MembroCasa` (`MembrosCasa`; índice único em `UsuarioId`: uma casa por pessoa) | permissões, ordem da passagem automática (D3) | consentimento `casa` | Azure | membros da casa: só o primeiro nome (`OperacoesCasa.PrimeiroNome`), quem é dono e a transferência pendente (`CasaResponse`) | até sair, ser removido (`OperacoesCasa.SairAsync`) ou apagar a conta (`ExclusaoConta`, `OperacoesCasa.SairParaExclusaoAsync`) |
| Convites: SHA-256 do token (`TokenHash`), `CriadoEm`, `ExpiraEm`, `UsadoEm`, `RevogadoEm`, quem gerou (`UsuarioId`) | `ConviteCasa` (`ConvitesCasa`) | link de uso único (7 dias, `OperacoesCasa.ValidadeConvite`) | consentimento `casa` de quem gerou | Azure | quem abre o link: o primeiro nome do dono, quantos membros a casa tem e o dia em que vence (`POST /api/casa/convites/verificar`); o dono vê os abertos | 30 dias depois de usado, revogado ou vencido (`LimpezaRetencao.PrazoConvitesCasa`); sai antes com a conta de quem gerou e com a casa |
| Semana da casa: almoço e jantar na porção de referência (sem dado de membro), `Revisao`, `VersaoBase`, `RefeicaoIndividualEm` | `SemanaCasa` (`SemanasCasa`, sem `UsuarioId`) | cardápio da casa, troca na casa, lista da casa | consentimento `casa` | Azure | membros (só os nomes dos alimentos, `SemanaCasaResponse`); só o dono: aviso de lista pobre e "refeição individual", sem dizer de quem | a atual e a anterior (`GeracaoSemanaCasa.SemanasGuardadas`); sai com a casa |
| Pedidos e avisos: tipo, semana, refeição, item, alimento, `CriadoEm`; **sem `UsuarioId` nem marcador do autor** | `PedidoCasa` (`PedidosCasa`) | "Pedir nova semana", "Pedir troca", aviso de entrada no meio da semana | consentimento `casa` | Azure | o dono, como "Alguém pediu…", sem data nem hora (`RegrasPedidosCasa.AbertosAsync`) | até ser dispensado, atendido (troca daquele alimento, `TrocaNaCasa`) ou a semana nova (`GeracaoSemanaCasa`); no máximo 30 dias (`LimpezaRetencao.PrazoPedidosCasa`); sai com a casa |
| "Já tenho" da casa: chave do item, quantidade, `MarcadoEm`; **sem `UsuarioId`** | `ListaCasaMarcacao` (`ListaCasaMarcacoes`) | lista de compras da casa | consentimento `casa` | Azure | membros da casa | enquanto a semana for a atual ou a anterior (sai com a semana em `GeracaoSemanaCasa`); sai com a casa |
| Origem e semana da casa da dieta: `Origem` (`pessoa`/`casa`), `SemanaCasaId` (sem chave estrangeira); no JSON, `daCasa` e `chaveCasa` das refeições | `DietaIA` (colunas novas em `DietasIA`) | porções da casa, limite de 3 gerações por hora, lista da casa | consentimento (geral) e `casa` | Azure | ninguém vê a porção; a dieta entra somada na lista da casa (`ListaComprasCasa`) | as 3 mais recentes; ao sair ou ser removido, `OperacoesCasa.SoltarDietasAsync` tira `SemanaCasaId`, `daCasa` e `chaveCasa` de todas |
| Pedidos de redefinição (tipo, datas, SHA-256 do token) | `PedidoRedefinicaoSenha` | link de uso único, limite por conta | contrato | Azure | Gmail (o e-mail com o link) | 30 dias (`LimpezaRetencao`) |
| Conta sem aceite e sem nenhum dado além de nome e e-mail | `Usuario` | — | — | Azure | — | 30 dias após `CriadoEm` (`LimpezaRetencao`) |
| IP do cliente | memória da API (limites) | limite de tentativas | legítimo interesse | memória do App Service | ninguém | até a janela do limite (15 min a 1 h) ou o reinício |
| Logs da API (id da conta, nomes de alimentos, contagens; nunca texto livre nem dado corporal) | Log stream do App Service | diagnóstico | legítimo interesse | Azure | ninguém | 7 dias, 35 MB |
| Cópias de segurança do banco | PostgreSQL Flexible | recuperar o sistema | legítimo interesse | Azure, Chile Central, sem cópia em outra região | ninguém | 7 dias (conferido em 2026-11-01) |
| Requisições, exceções e dependências (provável: URL, status, duração; IP mascarado por padrão) | Application Insights, pelo agente automático do App Service | diagnóstico | legítimo interesse | Azure | ninguém | 30 dias no total, sem arquivo (ajustado e conferido em 2026-11-01) |
| Logs HTTP do servidor web (IP, URL, status) | App Service (`WEBSITE_HTTPLOGGING_RETENTION_DAYS`) | diagnóstico | legítimo interesse | Azure | ninguém | 7 dias (conferido em 2026-11-01) |
| Prompt e resposta da geração | Gemini API | montar o cardápio | consentimento | Google, EUA | Google | o do Google: no plano gratuito, ele pode usar para melhorar os produtos dele, e revisores humanos podem ler. O Ronu não envia nada que identifique você (pendência 1) |
| E-mails enviados | caixa "Enviados" da conta Gmail do Ronu | "esqueci a senha" | contrato | Google, EUA | Google | até 30 dias (apagados à mão todo mês; pendência 3) |
| IP e páginas abertas | Vercel | servir o site | legítimo interesse | Vercel | Vercel | o da Vercel |
| IP ao abrir login e cadastro, mesmo sem usar o botão | script `accounts.google.com/gsi/client` em `entrar.html` e `criar-conta.html` | botão "Entrar com Google" | legítimo interesse | Google, EUA | Google | o do Google |
| Navegador: token, nome, tema, avisos dispensados; na troca de senha, o código do link e o e-mail digitado (`sessionStorage`, só a aba) | `localStorage` / `sessionStorage` | funcionamento | contrato | aparelho da pessoa | ninguém | até sair da conta (token) ou limpar o navegador |

O que vai para o Gemini, exatamente (`GeradorDietaGemini.MontarPrompt`, desde 2026-11-02): as metas de cada dia
(kcal e gramas de proteína, carboidrato e gordura), cada dia marcado como de déficit, manutenção ou superávit e, para
quem treina, de treino (com o horário) ou de descanso; os horários de acordar e dormir; a UF; a faixa de orçamento; a
lista fechada de alimentos (itens da base), os "gostos" que casaram com a base, os termos do "não como" que não casaram (pelo `TermoParaIA`) e, do "evitar"
leve (etapa 3), os itens que casaram, pelo nome da lista e agrupados por chip (o limite de 2 vezes na semana vale por
grupo), e os termos que não casaram, também pelo `TermoParaIA`. Risco residual do leve: um grupo pode sugerir uma
condição de saúde ("prefere evitar: açúcar refinado; mel; refrigerante de cola"); antes da etapa 3, o item só sumia da
lista. Por isso a política diz que os itens leves vão pelo nome (linha datada de 2026-11-14 em "Mudanças nesta política").
Não vão: nome, e-mail, id da conta, nome das modalidades, a rotina em texto, o "não como" e o "evitar" inteiros como foram digitados e as
alergias marcadas (TACO 2c: a lista fechada já chega sem os alimentos que as contêm; risco residual: quem lê a lista
pode deduzir a alergia pela falta de um grupo de itens, como todos os laticínios). Uma alergia fora dos 8 chips, escrita
no "não como" e sem casamento com a base (gergelim, por exemplo), continua indo como termo do "não como". Não
vão crus: sexo, idade, peso, altura e objetivo. A meta diária e a marca de cada dia são calculadas a partir de altura, peso, sexo,
nascimento, nível de atividade, treino e objetivo: não dizem o peso nem a idade e não identificam a pessoa, mas a marca
mostra a direção do objetivo (perder, manter ou ganhar peso). Detalhes e as checagens da resposta em `docs/design-api.md`, seção Dietas.

**Na semana da casa** (plano família, `GeradorDietaGemini.MontarPromptCasa`), o Gemini recebe só: a lista da casa
(`ListaDaCasa`: a interseção das listas de todos os membros, já sem o "não como" que casou com a base e sem as alergias
de todos, com os nomes regionais da UF do dono), o peso de uma unidade dos contáveis, os grupos do "evitar" leve de
qualquer membro pelo nome da lista ("alguém da casa prefere evitar", sem o texto digitado e sem os termos sem
casamento), as regras do cardápio (a do feijão escolhida pela UF do dono, sem escrevê-la), o orçamento da casa e a porção
de referência fixa (650 kcal no almoço e 550 no jantar, `PorcoesCasa`). Não vai nada de nenhum membro: meta, peso,
idade, sexo, altura, objetivo, treino, UF, nome, e-mail, id ou o texto das preferências (um teste inspeciona o prompt).
A porção de cada um é calculada em C# (`PorcoesCasa`), também na entrada no meio da semana
(`GeracaoSemanaCasa.EntrarNaSemanaAsync`). O café e os lanches de quem está numa casa vão pelo prompt individual, com a
meta do dia já sem as porções da casa. Risco residual: a falta de um grupo de itens na lista da casa pode sugerir que
alguém da casa tem uma alergia, e um grupo leve pode sugerir uma condição, como no prompt individual; e os nomes
regionais sugerem a região do dono. Nada disso identifica uma pessoa.

**A casa e os membros, por consentimento à parte.** Dividir dados com a casa é um uso que o geral não cobre: tem o tipo
`casa` (`TextosConsentimento`, v1, nunca publicado), gravado só ao criar a casa ou aceitar o convite (e, numa versão
nova, pelo `POST /api/consentimentos` de quem já está numa casa), e revogado ao sair ou ser removido. Sem o aceite
vigente, as rotas da casa dão 409 `consentimento_casa`; sair não exige aceite nenhum. O que os outros membros veem:
primeiro nome, papel, transferência pendente, orçamento, a semana (só nomes), a lista da casa somada (inclusive café e
lanches de cada um, sem origem), o "já tenho" da casa e as restrições da casa (`RestricoesDaCasa`: "não como" e alergias
numa lista só, "evitar" na outra, só nomes da base, sem dono, sem o texto digitado, sem dizer o que é alergia, em ordem
alfabética). Só o dono: convites abertos, pedidos e avisos sem autor, aviso de lista pobre e o "refeição individual". A
marca de alergia na lista da casa usa só as alergias de quem vê. Os logs da casa levam só ids da casa, ids de alimento e
contagens.

**Riscos residuais da casa** (aceitos, e avisados na caixa do consentimento `casa` e no cartão "O que a casa vê"):
1. **Casa pequena.** Com 2 pessoas, a restrição, o pedido e o aviso "sem autor" têm um autor óbvio, e a lista da casa
   menos a própria parte dá o que e quanto o outro come (inclusive o café e os lanches).
2. **Aviso de entrada.** Quando alguém entra no meio da semana e uma refeição vira individual, o dono sabe que quem
   acabou de entrar não come algo daquela refeição (não o quê, nem se é alergia).
3. **Dedução pela lista da casa.** A falta de um grupo inteiro nas restrições ou na lista pode sugerir uma alergia de
   alguém; as restrições não separam alergia de "não como" (para leite, ovo, soja, glúten, amendoim e castanhas, o
   casamento do texto e o chip tiram os mesmos itens), mas uma categoria inteira fora ("Laticínios") ainda sugere algo.
4. **Aparelho compartilhado.** O "Pedido enviado" fica no `localStorage` (`ronu:casa:pedidos:<id da conta>`) enquanto a
   conta está logada; o "Sair" apaga essas chaves (`ronuLimparSessao`, 2026-11-14), para quem usar o aparelho depois
   não ver que aquela conta pediu algo.

## 2. Plano de resposta a incidente (art. 48; Res. CD/ANPD 15/2024)

Incidente = qualquer acesso, perda, alteração ou vazamento de dado pessoal, confirmado ou com indício forte. Exemplos
no Ronu: chave JWT, connection string ou chave do Gemini expostas; banco acessado de fora; dump ou export com dado
real num commit; bug que mostra dado de uma conta para outra.

1. **Conter (na hora).** Trocar o que vazou: chave JWT (`Jwt:ChaveSecreta`, derruba todas as sessões), senha do banco,
   chave do Gemini, senha de app do Gmail. Fechar regras de firewall abertas. Se for bug, tirar a rota do ar ou
   voltar o deploy.
2. **Registrar (no mesmo dia).** Abrir uma entrada no registro de incidentes (fora do repositório público): data e hora
   da descoberta, o que aconteceu, dados e contas afetadas (contagem, nunca os dados), causa provável e o que foi feito.
3. **Avaliar o risco (até 2 dias úteis).** Há risco ou dano relevante? Pesa a favor: dado sensível (saúde é sensível),
   muitas contas, dado legível (sem hash), acesso confirmado por terceiro. Hash de senha BCrypt sozinho pesa menos que
   peso e dietas.
4. **Comunicar, se houver risco relevante.** À ANPD (formulário no site da ANPD) e às pessoas afetadas (por e-mail),
   em **3 dias úteis** da ciência, prazo que dobra para agente de pequeno porte (**6 dias úteis**). A comunicação diz:
   o que aconteceu, que dados, riscos, o que foi feito e o que a pessoa pode fazer (trocar senha, por exemplo), e o
   contato `ronu.appdono@gmail.com`.
5. **Corrigir a causa** e anotar no registro. Se a conta Gmail do Ronu estiver envolvida, apagar na hora os
   "Enviados" (os e-mails de recuperação de senha), sem esperar a limpeza do mês.
6. **Guardar o registro de todo incidente por 5 anos**, mesmo os que não foram comunicados (com o porquê).

> ⚖️ Advogado: confirmar o enquadramento como agente de pequeno porte (prazo em dobro) com tratamento de dado sensível.

## 3. Publicação da LGPD base com o interruptor `Consentimento:Exigir`

> **Publicada em 2026-11-13** (main 6263879), seguindo os passos abaixo. `Retencao__RotinaForaDaTelaEm` = `2026-11-13`,
> mesma data da vigência da política; `Consentimento__Exigir` apagada no fim. O passo 6 foi conferido com conta descartável.

O padrão no código é **ligado** (`appsettings.json`, e a chave ausente também conta como ligada). Ele só fica desligado
durante a publicação, para o frontend antigo não ficar travado entre o deploy do backend e o push do frontend.

1. No App Service `ronu-api` → Configurações → Variáveis de ambiente, criar `Consentimento__Exigir` = `false`
   (dois sublinhados) e salvar. O App Service reinicia.
2. Migrations `CriaConsentimentosEDatasUsuario` e `AdicionaHorariosEstruturados` em produção, pelo roteiro do
   `CLAUDE.md` (regra temporária no firewall, apagada na mesma janela). Elas só criam a tabela `Consentimentos` e
   colunas nulas (`CriadoEm`, `UltimoLoginEm` e os três horários).
3. Deploy do backend, pelo roteiro do `CLAUDE.md`. Com o interruptor desligado, o frontend antigo continua funcionando.
   Na primeira subida, a limpeza (1 minuto depois) apaga os pedidos de redefinição de senha com mais de 30 dias:
   é a regra, e o log mostra só a contagem. Nenhuma conta antiga entra (todas com `CriadoEm` nulo).
4. Push do frontend na `main` (a Vercel publica). Conferir no site: cadastro com a caixa, login de conta antiga indo
   para o consentimento.
5. **Voltar o interruptor:** apagar a variável `Consentimento__Exigir` (ou pôr `true`) e salvar. A partir daqui, toda
   conta sem aceite recebe 403 e a tela leva ao consentimento.
6. Conferir com uma conta descartável sem aceite que a API responde 403 em `/api/perfil`, e que depois do aceite tudo
   volta.

Se algo der errado entre os passos 3 e 5, o interruptor desligado mantém o app funcionando como antes.

**Depois da publicação: a rotina em texto livre (`Usuario.RotinaDiaria`).** Ela saiu da tela e do prompt (pendência 6).
Decisão de 2026-11-02, em dois tempos e nunca no mesmo deploy:

1. Durante 60 dias a partir da publicação, quem tem texto antigo o vê uma vez na Alimentação (mostrado só com
   `textContent`) e pode apagá-lo (`DELETE /api/perfil/rotina`); a exportação dos dados continua levando o texto. O
   `PUT /api/perfil` ignora o campo, para um frontend antigo em cache não dar erro. No fim do prazo, a limpeza
   automática (`LimpezaRetencao`) apaga todos os textos que sobraram. **Como o prazo começa:** no dia do passo 4
   acima (push do frontend), criar no App Service a variável `Retencao__RotinaForaDaTelaEm` com a data desse dia, no
   formato `AAAA-MM-DD`. **É a mesma data da `[DATA_VIGENCIA]` da política** (a do push do frontend): a política
   promete apagar "até 60 dias depois de [DATA_VIGENCIA]", então as duas não podem divergir. Sem ela, a limpeza da rotina não roda (o log da limpeza diz "não configurado"); com uma data
   em outro formato, a API não sobe. A limpeza apaga a partir de 60 dias depois dessa data (meia-noite UTC), na
   primeira rodada depois disso (uma por subida e a cada 24 h).
2. Só depois disso, e com o backend em produção já sem ler nem gravar a coluna (um deploy antes), uma **migration
   separada** remove a coluna `RotinaDiaria`, pelo roteiro do `CLAUDE.md`. A ordem importa: se a coluna sair antes
   do backend que ainda a lê, a API quebra.

## 4. Pendências

1. **Plano gratuito do Gemini.** Decisão de 2026-11-02: o Ronu fica no plano gratuito. Pelos termos da Gemini API
   ("Unpaid Services", vigentes desde 2026-03-23), o Google usa o que recebe e o que responde para melhorar os
   produtos dele, e revisores humanos podem ler, depois de o Google desligar o conteúdo da conta, da chave e do
   projeto. A política e a caixa do consentimento dizem isso. Os mesmos termos pedem que não se envie informação
   sensível, confidencial ou pessoal ao plano gratuito: desde 2026-11-02 o prompt não leva sexo, idade, peso, altura,
   objetivo, nome das modalidades nem texto livre (medido do g10a ao g10d, sem perda de qualidade; `docs/medicoes/`).
   **Resolvido:** a política (linha "Montar o cardápio" e "O que a IA recebe") e a caixa do consentimento (ainda v1,
   nunca publicada) dizem a lista nova do que vai e do que não vai para a IA. A caixa não cita o objetivo entre o que
   não vai, de propósito (2026-11-13): a marca de déficit, manutenção ou superávit de cada dia já mostra a direção.
2. **Advogado.** Os pontos "⚖️" deste documento e dos rascunhos em `docs/textos-legais/`.
3. **"Enviados" do Gmail: tarefa mensal.** Os e-mails de "esqueci a senha" ficam na caixa de enviados da conta do
   Ronu, e apagar a conta não os apaga. Decidido em 2026-11-01: o Matheus apaga os enviados uma vez por mês (lembrete
   no celular), e a política diz "até 30 dias". Apagar inclui esvaziar a lixeira do Gmail, senão eles ficam mais 30
   dias lá. Rever quando o envio migrar para um serviço transacional (Resend).
4. **Conta parada.** Proposta: avisar por e-mail com 24 meses sem login (`UltimoLoginEm`) e apagar 30 dias depois. Não
   está implementado, por isso não está na política. Contas com `UltimoLoginEm` nulo só começam a contar a partir do
   primeiro login depois da publicação.
5. **Application Insights e logs HTTP.** Conferido em 2026-11-01 (só os nomes das configurações): o App Service tem
   `ApplicationInsightsAgent_EXTENSION_VERSION` e `XDT_MicrosoftApplicationInsights_Mode`, o agente automático, que
   coleta sem SDK no código. Então é provável que requisições e exceções estejam indo para o Application Insights.
   Também há `WEBSITE_HTTPLOGGING_RETENTION_DAYS` (logs HTTP do servidor web, com IP e URL), lido em 2026-11-01:
   **7 dias**. O componente do Application Insights é o `ronu-api`, em Chile Central, e grava no workspace padrão do
   Log Analytics (`DefaultWorkspace-…-CLC`, grupo `DefaultResourceGroup-CLC`). Decidido em 2026-11-01: manter o
   agente. Retenção lida no componente (`az resource show`): **90 dias**. Num componente ligado a workspace, quem
   guarda os dados é o workspace, e o prazo de cada tabela pode ser diferente do componente. Retenção padrão do
   workspace, lida em 2026-11-01: **30 dias**. O `ronu-api` é o único Application Insights da assinatura. Em
   2026-11-01, com confirmação, as 11 tabelas `App*` (`AppRequests`, `AppDependencies`, `AppExceptions`, `AppTraces`,
   `AppMetrics`, `AppPerformanceCounters`, `AppAvailabilityResults`, `AppEvents`, `AppPageViews`, `AppBrowserTimings`,
   `AppSystemEvents`) foram para **30 dias no total** (`--retention-time 30` e `--total-retention-time 30`; o segundo é
   o que zera o arquivo, que antes guardava mais 60 dias), e o componente para `RetentionInDays` 30. Releitura da
   `AppRequests`: retenção 30, total 30. A política diz "até 30 dias". **Resolvido.** Cuidado: um recurso novo de
   Application Insights nessa região grava no mesmo workspace, e uma tabela `App*` nova pode nascer com outro prazo.
6. **Menos texto livre no Gemini.** Feito no prompt em 2026-11-02: do "evitar" vão só os termos que não casaram com a
   base, e a rotina em texto deu lugar aos horários estruturados. As telas dos horários estão feitas (Alimentação e
   Treino). O texto antigo da rotina, que não é mais usado (guardar dado sem uso vai contra a necessidade, art. 6º,
   III), segue a decisão de 2026-11-02 ("opção A", seção 3): a API ignora o campo no `PUT /perfil`, a pessoa apaga
   pelo `DELETE /api/perfil/rotina` (sem exigir o consentimento, como apagar a conta), e a limpeza apaga o que sobrar
   60 dias depois da publicação. **Falta, antes de publicar:** o aviso na Alimentação que mostra o texto antigo e
   oferece apagar (mudança visual, depende do shape aprovado).
7. **Regra de firewall "todos os serviços da Azure".** A única regra do `ronu-db` é
   `AllowAllAzureServicesAndResourcesWithinAzureIps_2026-9-22_18-31-2` (0.0.0.0): libera conexão de qualquer recurso
   dentro da Azure, inclusive de outros clientes, não só do `ronu-api`. A senha continua exigida, mas a superfície é
   maior que o necessário. Alternativa: VNet ou só os IPs de saída do App Service. Sinalizado, não mexido.
8. **Alergias e versão nova do texto (TACO 2c).** Se o texto do tipo `alergias` ganhar uma versão 2, os chips já
   gravados continuam filtrando a lista (o filtro é segurança e nunca é relaxado), mas o aceite deixa de ser o vigente:
   marcar um chip novo pede a caixa de novo. Decidir, antes de publicar uma v2, se os chips antigos esperam o novo
   aceite ou continuam valendo até lá. Sinalizado, não decidido.
9. **Casa (plano família, etapa 8, 2026-11-14).** A política e os termos descrevem a casa (seção "A Casa", tabelas e
   linha datada), conferidos com o código da branch `casa-commits`; o consentimento geral continua v1 (o texto dele segue
   verdadeiro: a casa tem o tipo `casa` à parte). Pontos abertos, só sinalizados: (a) resolvido: o "Pedido enviado" sai no "Sair" da conta (risco 4 acima); (b) a decisão D3 dizia que o membro que recebe a casa
   na exclusão de conta do dono "recebe um aviso", mas o código só muda o papel (`SairParaExclusaoAsync`), sem aviso;
   (c) o dono de uma casa com membros só desfaz o aceite `casa` sozinho tirando os membros um a um, ou com a
   transferência aceita por outra pessoa (apagar a conta não depende disso); (d) ⚖️ Advogado: a base do
   compartilhamento entre membros e se o aviso de dedução basta.
