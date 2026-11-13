<!-- Cópia do docs/planos/plano-familia.md do notebook do Matheus, colada por ele na thread em 2026-10-26. Decisões novas ficam na seção "Decisões de 2026-10-26", no fim. -->

# Plano família: decisões de produto

Status: **planejamento** (nada implementado). Decisões tomadas pelo Matheus em out/2026.
Ordem no roadmap: depois de TACO 2b → lista de compras → troca de alimentos → TACO 3 e 4.

## A ideia

Uma casa cozinha uma vez só para todo mundo: **mesma panela, porções diferentes**.
Cada pessoa continua com sua própria meta calórica e de macros; o que muda entre elas é a quantidade no prato.

## Formato: cada um com sua conta, ligados numa "casa"

- Cada adulto tem **login próprio**. Ninguém cadastra peso, idade ou objetivo de outra pessoa.
- Entrada por **convite por e-mail** + **tela de consentimento** explicando o que é compartilhado.
- **Todos os membros têm 18+** (mesma regra do app).
- **Uma casa por pessoa**; até **~6 membros** por casa.
- Motivo: dado de saúde é sensível pela LGPD; assim cada um é dono dos próprios dados.

## O que é compartilhado e o que é individual

| | Compartilhado (casa) | Individual |
|---|---|---|
| Refeições | **Almoço e jantar** | Café da manhã e lanches |
| Porção | — | Gramas de cada pessoa, pela meta dela |
| Peso, meta, objetivo | Nunca | Privados |
| Lista de compras | Lista da casa (soma de todos) | Itens das refeições individuais |
| Orçamento | **Orçamento da casa** (refeições compartilhadas + lista da casa) | Orçamento individual (café e lanches) |

## Como a base comum é montada

- A base compartilhada usa **só alimentos que todos aceitam**.
- No máximo **2 "misturas"** (proteínas/pratos principais) por refeição compartilhada.
- **Favoritos** de cada um vão para as **refeições individuais**, não para a base comum.
- **Alergias nunca entram em refeição compartilhada.** (Depende do TACO 2c: alérgenos declarados pela pessoa.)

## Restrições em dois níveis

- **"Não como"**: bloqueio. O alimento não aparece nas refeições compartilhadas.
- **"Prefiro evitar"**: peso negativo. Evita quando dá, mas pode aparecer.

## Privacidade

- O app **não diz de quem é cada restrição**. A casa vê "evitamos camarão", não "evitamos camarão por causa da Ana".
- Peso, meta, objetivo, progresso e treino de cada um não aparecem para os outros.

## Quem pode o quê

- **Só o dono da casa regenera** a semana compartilhada.
- Os membros podem **"Pedir nova semana"** (o dono recebe o pedido e decide).
- Convidar/remover membros: dono.

## Telas

- Nova aba **"Casa"**: membros, convites, semana compartilhada, lista de compras da casa, orçamento da casa.
- A dieta individual continua no Dashboard, com as refeições compartilhadas marcadas como "da casa".

## Dependências

- **TACO 2b** (lista fechada de alimentos com AlimentoId): necessária para somar a lista de compras e cruzar preferências.
- **TACO 2c** (alérgenos declarados): necessária para a regra "alergia nunca no compartilhado".
- **Lista de compras**: a lista da casa reaproveita a mesma lógica (soma por AlimentoId, cru x cozido, seções do mercado).
- **Troca de alimentos**: definir se a troca numa refeição compartilhada vale para a casa toda ou só para o prato de quem trocou (em aberto).

## Em aberto

- Troca de alimento em refeição compartilhada: vale para todos ou só para a pessoa?
- O que acontece com a semana compartilhada quando alguém entra ou sai da casa no meio da semana.
- Como o dono transfere a casa se quiser sair.
- Formato do pedido "Pedir nova semana" (notificação, e-mail ou só aviso na aba Casa).

## Decisões de 2026-10-26

- **Troca de alimento em refeição da casa:** só o dono troca, e vale para a casa toda. O substituto respeita o "não como" de todos e a porção de cada um é recalculada. Os membros veem "Pedir troca", que chega ao dono como o "Pedir nova semana".
- **TACO 2c antes do plano família:** a declaração de alérgenos é feita como etapa própria, antes, porque a regra "alergia nunca no compartilhado" depende dela.
- **Convite por link, não por e-mail:** o dono copia o link ou manda pelo WhatsApp. O link vale 7 dias e um uso só; quem abre faz login ou cria conta, passa pela checagem de 18+ e pelo consentimento. E-mail fica para depois de resolver o domínio próprio (hoje cai no spam).
- **Pedidos ("Pedir nova semana" e "Pedir troca"):** só aviso na aba Casa, com contador no ícone da aba para o dono. O pedido não diz quem pediu.
- **Consentimento honesto:** a tela avisa que, numa casa pequena (ex.: 2 pessoas), dá para deduzir de quem é uma restrição e, pela lista da casa, a porção do outro.
- **Entrada ou saída no meio da semana:** a semana da casa não muda. Quem entra recebe as porções na hora, calculadas pelo backend sobre o cardápio atual, sem chamar a IA; refeição com algo que essa pessoa não come ou tem alergia vira individual só para ela, e o dono vê um aviso sugerindo nova semana. Quem sai mantém as refeições da casa na própria dieta (mesmas comidas, porções dela) até gerar uma dieta nova; a lista da casa é recalculada sem ela e o vínculo é apagado.
- **Transferir a casa:** o dono escolhe um membro, que precisa aceitar. Com membros na casa, o dono não sai sem transferir. Se for o último, sair apaga a casa.
- **Refeições individuais de quem está numa casa:** o membro gera café da manhã e lanches quando quiser, com as calorias que sobram depois das porções da casa.
- **Lista de compras da casa soma tudo (muda a tabela acima):** a lista da casa leva a semana inteira de todos os membros, ou seja, almoço e jantar da casa **e** o café da manhã e os lanches de cada um, somados por alimento e sem dizer de quem é cada item, para fazer uma compra só. As refeições continuam como estão: almoço e jantar da casa, café e lanches de cada um. O consentimento passa a dizer que os itens do café e dos lanches entram somados na lista da casa. O orçamento da casa continua valendo só para almoço e jantar.
