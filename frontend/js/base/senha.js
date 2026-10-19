// Ronu — Botão de olho nos campos de senha (login, cadastro, redefinir senha)
//
// Pega todo <input type="password"> da página, envolve num .senha-caixa e
// põe ao lado um botão que mostra ou esconde o que foi digitado. O HTML de
// cada tela não muda: basta carregar este script. Cada campo tem o seu botão
// (no cadastro, senha e confirmação são independentes).
//
// Acessibilidade: o rótulo do botão diz a ação ("Mostrar senha" / "Ocultar
// senha") e aria-pressed diz o estado. O clique não tira o foco do campo
// nem perde a posição do cursor. Ao enviar o formulário a senha volta a ficar
// escondida, para não ficar à vista na tela seguinte nem no histórico.
(function () {
  var ICONE_OLHO = '<svg class="icone-mostrar" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M2 12s3.6-6.5 10-6.5S22 12 22 12s-3.6 6.5-10 6.5S2 12 2 12z"/><circle cx="12" cy="12" r="3"/></svg>';
  var ICONE_OLHO_RISCADO = '<svg class="icone-ocultar" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 3l18 18"/><path d="M10.6 5.6A9.6 9.6 0 0 1 12 5.5c6.4 0 10 6.5 10 6.5a17 17 0 0 1-3.1 3.9M6.6 6.6C3.7 8.5 2 12 2 12s3.6 6.5 10 6.5c1.6 0 3-.4 4.3-1"/><path d="M9.9 9.9a3 3 0 0 0 4.2 4.2"/></svg>';

  function preparar(campo) {
    if (campo.dataset.olho) return;
    campo.dataset.olho = 'sim';

    var caixa = document.createElement('div');
    caixa.className = 'senha-caixa';
    campo.parentNode.insertBefore(caixa, campo);
    caixa.appendChild(campo);

    var botao = document.createElement('button');
    botao.type = 'button';
    botao.className = 'botao-ver-senha';
    if (campo.id) botao.setAttribute('aria-controls', campo.id);
    botao.innerHTML = ICONE_OLHO + ICONE_OLHO_RISCADO;
    caixa.appendChild(botao);

    function mostrar(visivel) {
      campo.type = visivel ? 'text' : 'password';
      botao.setAttribute('aria-pressed', String(visivel));
      botao.setAttribute('aria-label', visivel ? 'Ocultar senha' : 'Mostrar senha');
    }

    // mousedown sem preventDefault tiraria o foco do campo antes do clique.
    botao.addEventListener('mousedown', function (evento) { evento.preventDefault(); });
    botao.addEventListener('click', function () {
      var inicio = campo.selectionStart;
      var fim = campo.selectionEnd;
      mostrar(campo.type === 'password');
      // Só devolve o foco se ele já estava no campo (clique com o mouse); no
      // teclado o foco fica no botão, para a pessoa poder alternar de novo.
      if (document.activeElement === campo && inicio != null) {
        try { campo.setSelectionRange(inicio, fim); } catch (erro) { /* tipo sem seleção */ }
      }
    });

    if (campo.form) campo.form.addEventListener('submit', function () { mostrar(false); });
    mostrar(false);
  }

  function iniciar() {
    Array.prototype.forEach.call(document.querySelectorAll('input[type="password"]'), preparar);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
  else iniciar();
})();
