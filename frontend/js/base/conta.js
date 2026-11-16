// Ronu — Baixar os dados e apagar a conta (perfil.html e autorizacao.html)
// As duas rotas (/api/conta) não exigem o consentimento: são direitos de quem
// nunca aceitou ou quer revogar, e revogar o consentimento geral é apagar a
// conta. A exclusão abre numa folha (<dialog> com showModal, a mesma da troca
// de alimento): prende o foco, o Esc fecha e o foco volta para quem abriu.

window.RonuConta = (function () {
  const FRASE = 'apagar minha conta';
  const MSG_REDE = 'Não foi possível falar com o Ronu agora. Verifique sua conexão e tente de novo.';
  const ICONE_FECHAR = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg>';

  function criar(tag, classe, texto) {
    const el = document.createElement(tag);
    if (classe) el.className = classe;
    if (texto != null) el.textContent = texto;
    return el;
  }

  // ---------- Baixar os dados ----------

  // O CORS não expõe o Content-Disposition, então o nome do arquivo é montado
  // aqui, com a data local (a mesma que a pessoa vê no aparelho).
  function nomeDoArquivo() {
    const hoje = new Date();
    const dois = (n) => String(n).padStart(2, '0');
    return `ronu-dados-${hoje.getFullYear()}-${dois(hoje.getMonth() + 1)}-${dois(hoje.getDate())}.json`;
  }

  // Baixa o arquivo e escreve o resultado em elStatus. botao entra em
  // carregamento (precisa de um .btn-label) enquanto a API monta o arquivo.
  async function baixarDados(botao, elStatus) {
    if (botao.dataset.loading === 'true') return;
    ronuDefinirCarregando(botao, true, 'Preparando o arquivo...');
    elStatus.textContent = '';

    try {
      const resposta = await ronuFetchAutenticado('/conta/exportacao');
      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        elStatus.textContent = corpo?.mensagem || 'Não foi possível baixar seus dados. Tente de novo.';
        return;
      }

      const url = URL.createObjectURL(await resposta.blob());
      const nome = nomeDoArquivo();
      const link = criar('a');
      link.href = url;
      link.download = nome;
      document.body.append(link);
      link.click();
      link.remove();
      // O navegador já pegou o arquivo; a URL temporária pode sair da memória.
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      elStatus.textContent = `Pronto: ${nome}`;
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      elStatus.textContent = MSG_REDE;
    } finally {
      ronuDefinirCarregando(botao, false);
    }
  }

  // ---------- Apagar a conta ----------

  let dialogo = null;

  function fechar() {
    if (dialogo?.open) dialogo.close();
  }

  // opcoes.temSenha: confirma com a senha (senão, com a frase).
  // opcoes.revogando: veio de "Revogar": diz no topo que revogar é apagar.
  // opcoes.origem: quem recebe o foco quando a folha fecha.
  function abrirExclusao({ temSenha, revogando = false, origem = null }) {
    if (!dialogo) {
      dialogo = criar('dialog', 'folha folha-lateral');
      dialogo.setAttribute('aria-labelledby', 'exclusao-titulo');
      document.body.append(dialogo);
    }
    dialogo.onclose = () => { if (origem?.isConnected) origem.focus(); };

    // Cabeça
    const cabeca = criar('header', 'folha-cabeca');
    const cabecaTexto = criar('div', 'folha-cabeca-texto');
    const titulo = criar('h2', 'folha-titulo', 'Apagar sua conta');
    titulo.id = 'exclusao-titulo';
    titulo.tabIndex = -1;
    cabecaTexto.append(titulo, criar('p', 'folha-apoio', 'Na hora e sem volta.'));
    const btnFechar = criar('button', 'folha-fechar');
    btnFechar.type = 'button';
    btnFechar.setAttribute('aria-label', 'Fechar');
    btnFechar.innerHTML = ICONE_FECHAR;
    btnFechar.addEventListener('click', fechar);
    cabeca.append(cabecaTexto, btnFechar);

    // Corpo
    const corpo = criar('div', 'folha-corpo');
    if (revogando) {
      corpo.append(criar('p', 'nota nota-forte', 'Revogar a autorização é apagar a conta: sem esses dados, o Ronu não consegue montar a sua dieta.'));
    }
    corpo.append(
      criar('p', null, 'Sai tudo do Ronu: a conta, os seus dados de saúde, as pesagens, os treinos, as preferências, as dietas e a lista de compras.'),
      criar('p', 'apoio', 'As cópias de segurança do banco somem sozinhas em até 7 dias e só servem para recuperar o sistema em caso de falha.')
    );

    const baixar = criar('div', 'exclusao-baixar');
    const btnBaixar = criar('button', 'botao botao-fantasma botao-pequeno');
    btnBaixar.type = 'button';
    btnBaixar.append(criar('span', 'btn-label', 'Baixar meus dados antes'));
    const statusBaixar = criar('p', 'apoio');
    statusBaixar.setAttribute('role', 'status');
    btnBaixar.addEventListener('click', () => baixarDados(btnBaixar, statusBaixar));
    baixar.append(btnBaixar, statusBaixar);
    corpo.append(baixar);

    const form = criar('form', 'folha-form');
    form.noValidate = true;
    const campo = criar('div', 'campo');
    const rotulo = criar('label', 'campo-rotulo', temSenha ? 'Sua senha' : `Digite "${FRASE}"`);
    rotulo.htmlFor = 'exclusao-confirmacao';
    const entrada = criar('input', 'entrada entrada-no-card');
    entrada.id = 'exclusao-confirmacao';
    entrada.type = temSenha ? 'password' : 'text';
    entrada.autocomplete = temSenha ? 'current-password' : 'off';
    entrada.setAttribute('autocapitalize', 'none');
    entrada.spellcheck = false;
    const dica = criar('span', 'campo-dica', temSenha
      ? 'Para confirmar que é você.'
      : 'Para confirmar, já que você entra pelo Google e a conta não tem senha.');
    dica.id = 'exclusao-dica';
    const erro = criar('span', 'campo-erro');
    erro.id = 'exclusao-erro';
    erro.hidden = true;
    entrada.setAttribute('aria-describedby', 'exclusao-dica exclusao-erro');
    campo.append(rotulo, entrada, dica, erro);
    corpo.append(campo);

    // Rodapé
    const rodape = criar('div', 'folha-rodape');
    const apagar = criar('button', 'botao botao-perigo botao-bloco');
    apagar.type = 'submit';
    apagar.append(criar('span', 'btn-label', 'Apagar para sempre'));
    const cancelar = criar('button', 'botao-texto', 'Cancelar');
    cancelar.type = 'button';
    cancelar.addEventListener('click', fechar);
    rodape.append(apagar, cancelar);

    form.append(corpo, rodape);
    form.addEventListener('submit', (evento) => {
      evento.preventDefault();
      confirmar({ temSenha, entrada, erro, apagar, form });
    });
    entrada.addEventListener('input', () => {
      erro.hidden = true;
      entrada.removeAttribute('aria-invalid');
    });

    dialogo.replaceChildren(cabeca, form);
    if (!dialogo.open) dialogo.showModal();
    titulo.focus();
  }

  function mostrarErro({ entrada, erro }, mensagem) {
    erro.textContent = mensagem;
    erro.hidden = false;
    entrada.setAttribute('aria-invalid', 'true');
    entrada.focus();
  }

  async function confirmar(partes) {
    const { temSenha, entrada, apagar, form } = partes;
    if (apagar.dataset.loading === 'true') return;

    const valor = entrada.value;
    if (!valor.trim()) {
      mostrarErro(partes, temSenha ? 'Digite sua senha para confirmar.' : `Digite "${FRASE}" para confirmar.`);
      return;
    }

    ronuDefinirCarregando(apagar, true, 'Apagando...');
    apagar.setAttribute('aria-busy', 'true');
    form.inert = true;

    try {
      const resposta = await ronuFetchAutenticado('/conta', {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(temSenha ? { senha: valor } : { confirmacao: valor })
      });

      if (resposta.status === 204) {
        ronuLimparSessao();
        window.location.href = 'entrar.html?conta=apagada';
        return;
      }

      const corpo = await resposta.json().catch(() => null);
      form.inert = false;
      mostrarErro(partes, corpo?.mensagem || 'Não foi possível apagar a conta. Tente de novo.');
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      form.inert = false;
      mostrarErro(partes, MSG_REDE);
    } finally {
      ronuDefinirCarregando(apagar, false);
      apagar.removeAttribute('aria-busy');
    }
  }

  return { baixarDados, abrirExclusao, fechar };
})();
