// Ronu — Medidor de força da senha e confirmação ao vivo (redesign de nov/2026)
//
// Protótipo "Entrada e onboarding": 4 barrinhas embaixo da senha, o texto
// "Faltam N caracteres" / Fraca / Razoável / Boa / Forte e o contador "0/64".
// A confirmação fica com a borda verde quando bate com a senha.
// Marcação: <div class="forca" data-forca-de="ID-DA-SENHA"> com 4 <i>, e na
// linha embaixo o texto (id ligado por aria-describedby) e [data-forca-conta].
// Também cuida do "Ler o que isso inclui" da autorização ([data-aceite-ler]).
// Só mostra: quem valida a senha é o formulário e a API.

(function () {
  'use strict';

  var NIVEIS = ['De 8 a 64 caracteres', 'Fraca', 'Razoável', 'Boa', 'Forte'];

  function forca(v) {
    if (v.length < 8) return v.length ? 1 : 0;
    var n = 1 + (/[a-z]/.test(v) && /[A-Z]/.test(v) ? 1 : 0) + (/\d/.test(v) ? 1 : 0) +
      (/[^A-Za-z0-9]/.test(v) || v.length >= 14 ? 1 : 0);
    return Math.min(4, Math.max(2, n));
  }

  function iniciar() {
    document.querySelectorAll('[data-forca-de]').forEach(function (medidor) {
      var campo = document.getElementById(medidor.getAttribute('data-forca-de'));
      if (!campo) return;
      var linha = medidor.nextElementSibling;
      var texto = linha && linha.querySelector('span:not([data-forca-conta])');
      var conta = linha && linha.querySelector('[data-forca-conta]');
      var confirmacao = document.querySelector('[data-confirma="' + campo.id + '"]');

      function conferir() {
        if (!confirmacao) return;
        confirmacao.classList.toggle('ok', Boolean(confirmacao.value) && confirmacao.value === campo.value);
      }

      campo.addEventListener('input', function () {
        var v = campo.value, n = forca(v);
        medidor.setAttribute('data-n', String(n));
        if (texto) texto.textContent = v.length && v.length < 8 ? 'Faltam ' + (8 - v.length) + ' caracteres' : NIVEIS[n];
        if (conta) conta.textContent = v.length + '/64';
        conferir();
      });
      if (confirmacao) confirmacao.addEventListener('input', conferir);
    });

    document.querySelectorAll('[data-aceite-ler]').forEach(function (botao) {
      var alvo = document.getElementById(botao.getAttribute('aria-controls'));
      if (!alvo) return;
      botao.addEventListener('click', function () {
        var abrir = botao.getAttribute('aria-expanded') !== 'true';
        botao.setAttribute('aria-expanded', String(abrir));
        alvo.classList.toggle('aceite-recolhido', !abrir);
        botao.textContent = abrir ? 'Fechar o texto' : 'Ler o que isso inclui';
      });
    });
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
  else iniciar();
})();
