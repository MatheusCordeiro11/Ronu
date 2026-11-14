# Política de privacidade do Ronu

> **Rascunho para aprovação (LGPD base, etapa 3, 2026-11-01).** Ainda não está publicado. Na etapa 5 este texto vira a
> página `/privacidade`; as notas "⚖️ Advogado" e os marcadores entre colchetes saem antes disso.
> Vigência preenchida em 2026-11-13, dia da publicação. Contato de privacidade: a conta Gmail do Ronu, decidido em 2026-11-13.
> O Ronu usa o plano gratuito do Gemini (decisão de 2026-11-02): os trechos sobre o Gemini dizem o que esse plano permite
> ao Google (termos da Gemini API, "Unpaid Services", vigentes desde 2026-03-23).
> Conferidos na Azure em 2026-11-01: backup de 7 dias e região Chile Central; logs HTTP do App Service, 7 dias;
> Application Insights, 30 dias no total.
> Casa (plano família, etapa 8, 2026-11-14): seção "A Casa" e linhas novas nas tabelas, conferidas com o código da branch
> `casa-commits`. Ainda não publicada: a data de "Vigente desde" e a da "Última mudança" são 2026-11-14 e mudam para o dia
> do push do frontend.

Vigente desde 14 de novembro de 2026.

O Ronu monta uma dieta semanal a partir do seu treino, do seu corpo e do que você gosta de comer. Para isso, ele
precisa de dados seus, alguns deles de saúde. Esta página explica, sem rodeios, quais dados são esses, para que servem,
onde ficam, com quem são compartilhados, por quanto tempo e como você controla tudo.

## Quem cuida dos seus dados

O Ronu é um projeto pessoal de **Matheus Cordeiro Berto**, que é o controlador dos dados (quem decide como eles são
usados) e também o encarregado (a pessoa com quem você fala sobre seus dados).

Contato para qualquer assunto de privacidade: **ronu.appdono@gmail.com**.

> ⚖️ Advogado: confirmar se pessoa física pode figurar como controlador de um app público e se o projeto se enquadra
> como agente de pequeno porte (Res. CD/ANPD 2/2022), que dispensa indicar encarregado, mesmo tratando dado sensível.
> Ver também se é preciso informar endereço ou outro dado de identificação além do nome.

## Que dados o Ronu usa

**Da sua conta**
- nome e e-mail;
- senha, guardada só como um código embaralhado (hash), que nem o Ronu consegue reverter;
- se você entra com o Google: o identificador da sua conta Google (não recebemos sua senha do Google);
- estado (UF) onde mora, para usar nomes regionais dos alimentos e o fuso certo;
- quando a conta foi criada e quando foi seu último login.

**De saúde e do corpo** (dados sensíveis, art. 5º, II da LGPD)
- peso e cada pesagem que você registra, com o objetivo (perder, manter ou ganhar peso) e se você seguiu a dieta;
- altura, sexo e data de nascimento;
- como é o seu dia fora do treino (sentado, em pé, andando, braçal);
- as modalidades que você treina, os dias e a duração;
- os alimentos que você gosta, os que não come e os que prefere evitar;
- as alergias que você marcar (leite, glúten, ovo, amendoim, castanhas, soja, peixe e crustáceos), com uma autorização própria;
- seus horários de acordar e dormir e o horário em que começa cada treino, se você informar;
- o texto de rotina que você escreveu antes, se houver: não é mais usado e será apagado até 60 dias depois de 13 de novembro de 2026, ou antes, se você apagar no app;
- seu orçamento para alimentação, se você informar;
- as dietas geradas e as metas de calorias de cada uma.

**Do uso do app**
- a lista de compras: quais itens você marcou como "já tenho";
- seus aceites de consentimento (qual texto, qual versão e quando);
- pedidos de "esqueci minha senha" (quando foram feitos e se foram usados).

**Da casa, se você criar uma ou entrar numa** (veja "A Casa", abaixo)
- em qual casa você está, se cuida dela ou é membro, quando entrou e, se for o caso, que a casa foi oferecida a você;
- os convites que você gerou: quando foram criados, até quando valem e se foram usados ou cancelados. Do link, o Ronu
  guarda só um código embaralhado (hash), que não serve para montar o link de novo;
- o orçamento da casa, se quem cuida dela informar;
- o almoço e o jantar da semana da casa (a atual e a anterior), numa porção de referência, sem a porção nem nenhum dado
  de ninguém;
- os pedidos de semana nova e de troca, e os avisos para quem cuida da casa, que não guardam quem pediu;
- os itens da lista da casa marcados como "já tenho", que também não guardam quem marcou;
- seu aceite da autorização da casa (qual versão e quando) e, se você saiu, quando saiu.

**O que o Ronu não pede:** CPF, endereço, telefone, documentos, cartão ou dado bancário. Nos campos "Não como" e
"Prefiro evitar", escreva só o alimento: **não escreva doenças, remédios ou diagnósticos**, porque o que não for reconhecido como um
alimento da nossa lista vai para a IA do jeito que você escreveu.

## Para que esses dados servem, e com que base

| Para quê | Quais dados | Base legal |
|---|---|---|
| Criar e manter sua conta, deixar você entrar e recuperar a senha | conta | execução do contrato (art. 7º, V) |
| Calcular sua meta de calorias e macros | saúde e corpo | seu consentimento (art. 11, I) |
| Montar o cardápio com a IA do Google (Gemini) | a meta de cada dia (calculada com seus dados de saúde e do corpo), se o dia é de déficit, manutenção ou superávit, os dias e horários de treino, seus horários de acordar e dormir, o estado (UF), o orçamento e os alimentos que você gosta, não come ou prefere evitar | seu consentimento (art. 11, I) |
| Tirar do cardápio e das trocas os alimentos com as suas alergias, e marcá-los na lista de compras | as alergias marcadas | seu consentimento específico, dado à parte (art. 11, I) |
| Mostrar seu progresso e ajustar a meta pelo que aconteceu de verdade | pesagens, metas, dietas | seu consentimento (art. 11, I) |
| Montar a Casa, se você estiver numa: a semana do almoço e do jantar, a sua porção, a lista de compras da casa e as restrições da casa | seu primeiro nome; os alimentos que você não come ou prefere evitar e as alergias marcadas (para a lista da casa e as restrições da casa, sem dizer de quem); a sua meta de cada dia (só para calcular a sua porção, sem mostrar a ninguém); suas dietas (somadas na lista da casa) | seu consentimento específico para a casa, dado à parte (art. 11, I) |
| Montar o almoço e o jantar da casa com a IA do Google (Gemini) | a lista de alimentos que todos da casa aceitam, os alimentos dessa lista que alguém prefere evitar (sem dizer de quem) e o orçamento da casa | seu consentimento (art. 11, I) e a autorização da casa |
| Proteger o app contra abuso (limite de tentativas, registros técnicos) | endereço IP, identificador da conta | legítimo interesse (art. 7º, IX) |

A meta é calculada por fórmulas publicadas (Mifflin-St Jeor e o Compêndio de Atividades Físicas) e ajustada pelas
pesagens que você registra. Se quiser entender
como chegamos no seu número, escreva para ronu.appdono@gmail.com.

O consentimento é pedido numa caixa própria, desmarcada, antes de qualquer dado de saúde. Sem ele, o Ronu não
funciona, porque não dá para montar a dieta sem esses dados. As alergias têm uma autorização à parte, também numa caixa
própria e desmarcada, pedida na primeira vez que você marca uma. A Casa também tem uma autorização à parte (veja
"A Casa", abaixo).

**O que a IA recebe.** O Gemini recebe só o que está na linha "Montar o cardápio" da tabela acima. Seu nome, seu e-mail, o número da sua conta, seu peso, sua altura, sua idade, seu sexo e o nome das modalidades que você treina não vão para a IA: ela recebe a meta de cada dia já calculada, sem os dados usados no cálculo. Dos alimentos que você não come, os que estão na nossa lista são tirados antes, e só os que não estão vão como você escreveu. Dos que você prefere evitar, os que estão na nossa lista continuam nela e vão para a IA pelo nome da lista, com o pedido de usar pouco; os que não estão vão como você escreveu. As alergias marcadas não vão para a IA: ela recebe a lista de alimentos já sem esses itens. O Ronu usa o plano gratuito do Gemini. Nesse plano, o Google pode usar o que recebe para melhorar os produtos dele, e revisores humanos podem ler esse conteúdo. O Ronu não envia nada que identifique você.

O Ronu não vende seus dados, não mostra anúncios e não usa seus dados para nada além do que está nesta página.

> ⚖️ Advogado: conferir a base do uso de segurança (legítimo interesse) sobre IP, que não é dado sensível.

## A Casa: o que as pessoas da sua casa veem

Na Casa, até 6 adultos que moram juntos dividem o almoço e o jantar da semana: a mesma panela, com a porção de cada
um. Cada pessoa tem a própria conta, e ninguém cadastra os dados de outra. Entrar numa casa é opcional.

**O que as pessoas da casa veem**
- seu primeiro nome (sem sobrenome nem e-mail) e quem cuida da casa;
- o cardápio do almoço e do jantar da semana, só com os nomes dos alimentos;
- a lista de compras da casa: a semana inteira de todos, somada por alimento, inclusive os itens do seu café da manhã e
  dos seus lanches, sem dizer de quem é cada item;
- as restrições da casa: o que alguém não come, o que alguém prefere evitar e as alergias, sem dizer de quem é cada uma
  e sem dizer o que é alergia. Aparecem só nomes de alimentos da nossa lista, nunca o que você escreveu;
- o orçamento da casa e os itens marcados como "já tenho".

Quem cuida da casa vê também os convites ainda abertos, os pedidos de semana nova ou de troca e os avisos da casa,
sempre como "Alguém pediu…", sem nome, sem data e sem hora.

**O que fica só com você:** seu peso, sua meta, seu objetivo, seu progresso, seus treinos, a sua porção em cada
refeição e os cardápios do seu café da manhã e dos seus lanches. Na lista da casa, a marca de alergia de cada item usa
só as suas alergias: assim, uma marca não conta a ninguém que outra pessoa da casa tem uma alergia.

**O que dá para deduzir numa casa pequena.** O Ronu não diz de quem é cada restrição, cada item da lista ou cada
pedido. Mas, numa casa com poucas pessoas, como duas, dá para deduzir de quem é uma restrição, quem fez um pedido e,
tirando a sua parte da lista da casa, o que e quanto a outra pessoa come. A caixa da autorização avisa disso antes de
você entrar.

**A autorização da casa.** Dividir esses dados com a casa é um uso à parte, com autorização própria, numa caixa
desmarcada, pedida quando você cria uma casa ou aceita um convite. O Ronu guarda qual versão do texto você aceitou e
quando. Se o texto mudar, a casa fica fechada para você até você aceitar o texto novo ou sair. Para desfazer a
autorização, saia da casa, a qualquer momento.

**Convites.** Quem cuida da casa convida por um link, que vale para uma pessoa só, por 7 dias. O Ronu guarda só um
código embaralhado (hash) do link. Antes de entrar, quem abriu o link vê só o primeiro nome de quem convidou, quantas
pessoas a casa tem e até quando o link vale. Para entrar, a pessoa precisa ter 18 anos ou mais, pela data de nascimento
do perfil dela, e o perfil completo. Os convites usados, cancelados ou vencidos são apagados 30 dias depois.

**Pedidos, avisos e "já tenho".** Os pedidos de semana nova ou de troca não guardam quem pediu, nem no banco de dados.
Quando alguém entra no meio da semana e não come algo de uma refeição da casa, essa refeição fica individual para essa
pessoa, e quem cuida da casa recebe um aviso sem nome, sem dizer qual alimento nem por quê. Pedidos e avisos são
apagados quando quem cuida da casa os dispensa, quando troca o alimento pedido, quando gera uma semana nova ou, no
máximo, 30 dias depois. O "já tenho" da lista da casa também não guarda quem marcou, e vale para uma semana.

**Sair, ser tirado da casa, passar a casa adiante**
- Ao sair, ou ser tirado da casa por quem cuida dela, seu vínculo com a casa é apagado e a sua autorização fica
  registrada como desfeita, com a data. Você fica com o almoço e o jantar da casa na sua dieta, com a sua porção, até
  gerar outra, já sem ligação com a casa. A lista da casa deixa de contar você na hora.
- Quem cuida da casa pode passá-la a outra pessoa da casa, que precisa aceitar. Com outras pessoas na casa, quem cuida
  dela passa a casa adiante ou tira as outras pessoas antes de sair.
- Quando a última pessoa sai, a casa acaba: o Ronu apaga a casa, os convites, as semanas, os pedidos e o "já tenho" da
  casa. As dietas de cada um ficam como estão.
- Se você apagar a conta, sai da casa junto, sem precisar passar a casa antes, e tudo o que é seu é apagado. Se era a
  última pessoa, a casa também é apagada. Se cuidava da casa e havia outras pessoas nela, a casa passa sozinha para
  quem está nela há mais tempo, e os convites que você gerou são apagados com a sua conta.

**A IA na semana da casa.** Nenhum dado de saúde de ninguém da casa vai para a IA na semana da casa: nem meta, peso,
altura, idade, sexo, objetivo, treino ou alergia, nem nome ou e-mail. Ela recebe só a lista de alimentos que todos da
casa aceitam (já sem o que alguém não come ou tem alergia), os alimentos dessa lista que alguém prefere evitar, pelo nome
da lista e sem dizer de quem, as regras do cardápio, o orçamento da casa e uma porção de referência, a mesma para todas
as casas. O que alguém escreveu e não está na nossa lista não vai. A porção de cada pessoa é calculada depois, pelo
próprio Ronu, sem a IA, inclusive para quem entra no meio da semana. O café da manhã e os lanches de cada um são montados
como na dieta de quem não está numa casa, com o que está na linha "Montar o cardápio" da tabela acima.

> ⚖️ Advogado: conferir se o consentimento específico (art. 11, I) é a base certa para dividir dado de saúde com outras
> pessoas da casa, e se o aviso de dedução numa casa pequena basta.

## Com quem os dados são compartilhados

O Ronu usa serviços de outras empresas para funcionar. Elas tratam os dados em nome do Ronu (são operadoras), só
para prestar o serviço, com uma exceção: no plano gratuito do Gemini, o Google também pode usar o que recebe para
melhorar os produtos dele.

> ⚖️ Advogado: nesse uso para os próprios produtos, o Google deixa de ser só operador. Conferir se a política precisa
> tratá-lo como controlador nessa parte, e se o consentimento da caixa cobre esse compartilhamento.


| Empresa | Para quê | O que recebe | Onde |
|---|---|---|---|
| **Microsoft Azure** | servidor da API e banco de dados | todos os dados da conta | Chile |
| **Google (Gemini)** | montar o cardápio | os dados da linha "Montar o cardápio" acima, sem nome nem e-mail. Na semana da casa, só o que está em "A IA na semana da casa". No plano gratuito, o Google também pode usá-los para melhorar os produtos dele | EUA |
| **Google (login)** | "Entrar com Google", se você usar | o que o Google já tem da sua conta; o Ronu recebe nome, e-mail e identificador. Mesmo sem você usar o botão, o Google recebe o seu endereço IP ao abrir as telas de login e cadastro | EUA |
| **Google (Gmail)** | enviar o e-mail de "esqueci minha senha" | seu e-mail e o link | EUA |
| **Vercel** | hospedar as páginas do site | seu endereço IP e as páginas que você abre | EUA e outros países |

Se você está numa casa, as pessoas dela veem o que está em "A Casa", acima. Fora esses casos, o Ronu só entrega dados a
uma autoridade se a lei obrigar.

## Dados fora do Brasil

Seus dados ficam guardados no **Chile** (Microsoft Azure) e passam pelos **EUA** (Google, Vercel). A LGPD permite
essa transferência com o seu consentimento específico (art. 33, VIII), que você dá na caixa do consentimento, e com
as garantias contratuais que essas empresas oferecem.

> ⚖️ Advogado: conferir se o consentimento da caixa basta como consentimento específico para a transferência
> internacional, ou se é melhor apoiar em cláusulas-padrão contratuais (Res. CD/ANPD 19/2024) dos provedores.

## Por quanto tempo

| Dado | Quanto tempo |
|---|---|
| Conta, perfil, pesagens, treinos, preferências, metas | enquanto a conta existir |
| Alergias marcadas | até você desmarcar todas (aí o Ronu apaga suas alergias), ou enquanto a conta existir |
| Texto de rotina escrito antes de 13 de novembro de 2026 | até 60 dias depois de 13 de novembro de 2026, ou antes, se você apagar no app |
| Dietas | as 3 mais recentes (a mais antiga sai quando você gera uma nova) |
| Marcações "já tenho" da lista de compras | enquanto a dieta delas estiver entre as 3 |
| Seus aceites de consentimento (inclusive o da casa, desfeito ao sair, que fica com a data em que você saiu) | enquanto a conta existir |
| Sua participação numa casa | até você sair, ser tirado da casa ou apagar a conta |
| A casa e o orçamento dela | enquanto houver alguém na casa |
| Almoço e jantar da semana da casa (sem dado de ninguém) | a semana atual e a anterior; tudo sai quando a casa acaba |
| Convites da casa | 30 dias depois de usados, cancelados ou vencidos (o link vale 7 dias) |
| Pedidos e avisos da casa (sem quem pediu) | até quem cuida da casa dispensar ou atender, até a semana nova, ou no máximo 30 dias |
| Marcações "já tenho" da lista da casa (sem quem marcou) | enquanto a semana delas for a atual ou a anterior da casa |
| Pedidos de "esqueci minha senha" | 30 dias |
| E-mails de recuperação de senha enviados (pasta Enviados do e-mail do Ronu) | até 30 dias |
| Conta criada que nunca aceitou o consentimento e não tem nenhum dado além de nome e e-mail | 30 dias |
| Registros técnicos do servidor (só o número da conta, sem dado de saúde) | 7 dias |
| Registros de acesso e diagnóstico da Azure (endereço IP, página da API pedida, erros) | até 30 dias |
| Cópias de segurança automáticas do banco | 7 dias |
| O que vai para o Gemini | o prazo do Google: no plano gratuito, ele pode guardar e usar esse conteúdo para melhorar os produtos dele. O Ronu não envia nada que identifique você |

Quando você apaga a conta, tudo sai do banco na hora (o que acontece com a sua casa está em "A Casa"). As cópias de segurança somem sozinhas no prazo acima e não são
usadas para nada além de recuperar o sistema em caso de falha.

## Seus direitos, e como usar

A LGPD (art. 18) garante que você pode:

- **saber** se o Ronu trata dados seus e **ver** quais são;
- **corrigir** dados errados ou desatualizados;
- **baixar** seus dados num arquivo, para guardar ou levar para outro serviço;
- **apagar** sua conta e todos os seus dados;
- **revogar** o consentimento;
- saber **com quem** os dados são compartilhados;
- reclamar na **Autoridade Nacional de Proteção de Dados** (ANPD).

A maioria você faz sozinho, no app:

- **Corrigir:** no Perfil, em Treino e em Alimentação. Pesagens erradas podem ser apagadas no Progresso.
- **Baixar seus dados:** no Perfil, em "Baixar meus dados". Vem um arquivo com tudo da sua conta. Se você está numa
  casa, vêm também a sua participação nela (se cuida dela ou é membro e quando entrou), os convites que você gerou (sem
  o código do link) e o seu aceite da casa, e as suas dietas vêm com o almoço e o jantar da casa na sua porção. Não vêm
  os dados das outras pessoas nem o que é da casa toda (a semana, a lista, o orçamento, os pedidos e o "já tenho" da
  casa, que não guardam quem fez).
- **Apagar a conta:** no Perfil, em "Apagar minha conta". Pedimos sua senha (ou, se você só usa o Google, que digite
  uma frase) e apagamos tudo na hora.
- **Revogar o consentimento:** como o Ronu não funciona sem esses dados, revogar é apagar a conta. Antes, você pode
  baixar seus dados. A autorização das alergias é à parte: desmarque todas em Alimentação e o Ronu apaga suas
  alergias, sem mexer no resto da conta. A autorização da casa também é à parte: na aba Casa, em "Sair da casa". Quem
  cuida de uma casa com outras pessoas passa a casa adiante ou tira as outras pessoas antes de sair; apagar a conta não depende disso.

Para qualquer outro pedido, escreva para **ronu.appdono@gmail.com**. Respondemos em até 15 dias.

> ⚖️ Advogado: conferir o prazo de 15 dias (art. 19, II) e se é preciso confirmar a identidade de quem escreve antes
> de atender (por exemplo, pedir que escreva do e-mail da conta).

## Segurança

- toda comunicação com o Ronu é criptografada (HTTPS);
- a senha é guardada só como hash (BCrypt);
- o login vale por 2 horas, e trocar a senha encerra as outras sessões;
- tentativas de login, cadastro e "apagar conta" têm limite, contra quem tenta adivinhar senhas;
- o link do convite da casa vale uma vez, por 7 dias, e o Ronu guarda só um código embaralhado dele;
- o banco de dados só aceita conexão de dentro da Azure, sempre com senha;
- os registros do servidor não guardam dados de saúde.

Se acontecer um incidente que possa trazer risco a você, avisamos você e a ANPD.

## Armazenamento no navegador

O Ronu não usa cookies próprios nem rastreadores de anúncio. Ele guarda no seu navegador (armazenamento local) só o
necessário para funcionar: o login, seu nome para o cabeçalho, sua escolha de tema (claro ou escuro), alguns avisos
que você dispensou e, durante a troca de senha, o código do link de recuperação e o e-mail digitado. Se você está numa
casa, ele guarda também quais pedidos você fez na semana (para mostrar "Pedido enviado") e, enquanto você entra por um
convite, o link do convite, só naquela aba. Ao sair da conta, o login é apagado. O botão "Entrar com Google" é do Google e segue as regras do
Google.

## Idade

O Ronu é só para maiores de 18 anos. Isso vale para todas as pessoas de uma casa: o convite confere a idade pela data
de nascimento do perfil de quem vai entrar.

## Mudanças nesta política

Se esta política mudar, a data no topo muda. Se a mudança afetar o que você autorizou, pedimos seu consentimento de
novo antes de continuar.

Última mudança (14 de novembro de 2026): esta política passou a descrever a Casa, em que pessoas que moram juntas
dividem o almoço e o jantar da semana: o que a casa vê, o que fica só com você, a autorização própria da casa, os
convites, os pedidos, o que acontece ao sair e o que vai para a IA na semana da casa. Quem não entra numa casa não muda
nada.

Mudança anterior (14 de novembro de 2026): o antigo "prefiro evitar" virou dois campos. "Não como" tira o alimento do
cardápio, como o "prefiro evitar" fazia antes. "Prefiro evitar" deixa o alimento aparecer poucas vezes na semana, e os
que estão na nossa lista passam a ir para a IA pelo nome. Esta política também passou a descrever as alergias
marcadas.
