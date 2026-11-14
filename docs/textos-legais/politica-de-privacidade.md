# Política de privacidade do Ronu

> **Rascunho para aprovação (LGPD base, etapa 3, 2026-11-01).** Ainda não está publicado. Na etapa 5 este texto vira a
> página `/privacidade`; as notas "⚖️ Advogado" e os marcadores entre colchetes saem antes disso.
> Vigência preenchida em 2026-11-13, dia da publicação. Contato de privacidade: a conta Gmail do Ronu, decidido em 2026-11-13.
> O Ronu usa o plano gratuito do Gemini (decisão de 2026-11-02): os trechos sobre o Gemini dizem o que esse plano permite
> ao Google (termos da Gemini API, "Unpaid Services", vigentes desde 2026-03-23).
> Conferidos na Azure em 2026-11-01: backup de 7 dias e região Chile Central; logs HTTP do App Service, 7 dias;
> Application Insights, 30 dias no total.

Vigente desde 13 de novembro de 2026.

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
| Proteger o app contra abuso (limite de tentativas, registros técnicos) | endereço IP, identificador da conta | legítimo interesse (art. 7º, IX) |

A meta é calculada por fórmulas publicadas (Mifflin-St Jeor e o Compêndio de Atividades Físicas) e ajustada pelas
pesagens que você registra. Se quiser entender
como chegamos no seu número, escreva para ronu.appdono@gmail.com.

O consentimento é pedido numa caixa própria, desmarcada, antes de qualquer dado de saúde. Sem ele, o Ronu não
funciona, porque não dá para montar a dieta sem esses dados. As alergias têm uma autorização à parte, também numa caixa
própria e desmarcada, pedida na primeira vez que você marca uma.

**O que a IA recebe.** O Gemini recebe só o que está na linha "Montar o cardápio" da tabela acima. Seu nome, seu e-mail, o número da sua conta, seu peso, sua altura, sua idade, seu sexo e o nome das modalidades que você treina não vão para a IA: ela recebe a meta de cada dia já calculada, sem os dados usados no cálculo. Dos alimentos que você não come, os que estão na nossa lista são tirados antes, e só os que não estão vão como você escreveu. Dos que você prefere evitar, os que estão na nossa lista continuam nela e vão para a IA pelo nome da lista, com o pedido de usar pouco; os que não estão vão como você escreveu. As alergias marcadas não vão para a IA: ela recebe a lista de alimentos já sem esses itens. O Ronu usa o plano gratuito do Gemini. Nesse plano, o Google pode usar o que recebe para melhorar os produtos dele, e revisores humanos podem ler esse conteúdo. O Ronu não envia nada que identifique você.

O Ronu não vende seus dados, não mostra anúncios e não usa seus dados para nada além do que está nesta página.

> ⚖️ Advogado: conferir a base do uso de segurança (legítimo interesse) sobre IP, que não é dado sensível.

## Com quem os dados são compartilhados

O Ronu usa serviços de outras empresas para funcionar. Elas tratam os dados em nome do Ronu (são operadoras), só
para prestar o serviço, com uma exceção: no plano gratuito do Gemini, o Google também pode usar o que recebe para
melhorar os produtos dele.

> ⚖️ Advogado: nesse uso para os próprios produtos, o Google deixa de ser só operador. Conferir se a política precisa
> tratá-lo como controlador nessa parte, e se o consentimento da caixa cobre esse compartilhamento.


| Empresa | Para quê | O que recebe | Onde |
|---|---|---|---|
| **Microsoft Azure** | servidor da API e banco de dados | todos os dados da conta | Chile |
| **Google (Gemini)** | montar o cardápio | os dados da linha "Montar o cardápio" acima, sem nome nem e-mail. No plano gratuito, o Google também pode usá-los para melhorar os produtos dele | EUA |
| **Google (login)** | "Entrar com Google", se você usar | o que o Google já tem da sua conta; o Ronu recebe nome, e-mail e identificador. Mesmo sem você usar o botão, o Google recebe o seu endereço IP ao abrir as telas de login e cadastro | EUA |
| **Google (Gmail)** | enviar o e-mail de "esqueci minha senha" | seu e-mail e o link | EUA |
| **Vercel** | hospedar as páginas do site | seu endereço IP e as páginas que você abre | EUA e outros países |

Fora esses casos, o Ronu só entrega dados a uma autoridade se a lei obrigar.

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
| Pedidos de "esqueci minha senha" | 30 dias |
| E-mails de recuperação de senha enviados (pasta Enviados do e-mail do Ronu) | até 30 dias |
| Conta criada que nunca aceitou o consentimento e não tem nenhum dado além de nome e e-mail | 30 dias |
| Registros técnicos do servidor (só o número da conta, sem dado de saúde) | 7 dias |
| Registros de acesso e diagnóstico da Azure (endereço IP, página da API pedida, erros) | até 30 dias |
| Cópias de segurança automáticas do banco | 7 dias |
| O que vai para o Gemini | o prazo do Google: no plano gratuito, ele pode guardar e usar esse conteúdo para melhorar os produtos dele. O Ronu não envia nada que identifique você |

Quando você apaga a conta, tudo sai do banco na hora. As cópias de segurança somem sozinhas no prazo acima e não são
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
- **Baixar seus dados:** no Perfil, em "Baixar meus dados". Vem um arquivo com tudo da sua conta.
- **Apagar a conta:** no Perfil, em "Apagar minha conta". Pedimos sua senha (ou, se você só usa o Google, que digite
  uma frase) e apagamos tudo na hora.
- **Revogar o consentimento:** como o Ronu não funciona sem esses dados, revogar é apagar a conta. Antes, você pode
  baixar seus dados. A autorização das alergias é à parte: desmarque todas em Alimentação e o Ronu apaga suas
  alergias, sem mexer no resto da conta.

Para qualquer outro pedido, escreva para **ronu.appdono@gmail.com**. Respondemos em até 15 dias.

> ⚖️ Advogado: conferir o prazo de 15 dias (art. 19, II) e se é preciso confirmar a identidade de quem escreve antes
> de atender (por exemplo, pedir que escreva do e-mail da conta).

## Segurança

- toda comunicação com o Ronu é criptografada (HTTPS);
- a senha é guardada só como hash (BCrypt);
- o login vale por 2 horas, e trocar a senha encerra as outras sessões;
- tentativas de login, cadastro e "apagar conta" têm limite, contra quem tenta adivinhar senhas;
- o banco de dados só aceita conexão de dentro da Azure, sempre com senha;
- os registros do servidor não guardam dados de saúde.

Se acontecer um incidente que possa trazer risco a você, avisamos você e a ANPD.

## Armazenamento no navegador

O Ronu não usa cookies próprios nem rastreadores de anúncio. Ele guarda no seu navegador (armazenamento local) só o
necessário para funcionar: o login, seu nome para o cabeçalho, sua escolha de tema (claro ou escuro), alguns avisos
que você dispensou e, durante a troca de senha, o código do link de recuperação e o e-mail digitado. Ao sair da conta, o login é apagado. O botão "Entrar com Google" é do Google e segue as regras do
Google.

## Idade

O Ronu é só para maiores de 18 anos.

## Mudanças nesta política

Se esta política mudar, a data no topo muda. Se a mudança afetar o que você autorizou, pedimos seu consentimento de
novo antes de continuar.

Última mudança (13 de novembro de 2026): o antigo "prefiro evitar" virou dois campos. "Não como" tira o alimento do
cardápio, como o "prefiro evitar" fazia antes. "Prefiro evitar" deixa o alimento aparecer poucas vezes na semana, e os
que estão na nossa lista passam a ir para a IA pelo nome. Esta política também passou a descrever as alergias
marcadas.
