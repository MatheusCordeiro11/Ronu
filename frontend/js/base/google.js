// Ronu — Botão oficial "Entrar/Cadastrar com Google"
//
// O botão é o do Google Identity Services (um iframe deles), não um desenho
// nosso: o canvas do redesign mostrava um botão próprio, mas a regra do Google
// é usar o renderizado por eles. Ele só aceita temas fixos, então é desenhado
// de novo quando o modo da página troca (evento "ronu:tema", js/base/tema.js):
// "filled_black" no Dojo Noturno e "outline" no Washi. Também se redesenha ao
// mudar a largura, porque o Google fixa a largura em pixels (200 a 400).
//
// Uso: ronuIniciarBotaoGoogle('signin_with' | 'signup_with'), no load da
// página, com o script https://accounts.google.com/gsi/client já incluído.
// O callback é o ronuTratarCredencialGoogle (js/auth.js).
(function () {
  var CLIENT_ID = '718933216357-u2b86snquurf6ie5m1hb9lbhu7do3opi.apps.googleusercontent.com';

  function desenhar(slot, texto) {
    var modo = window.RonuTema ? window.RonuTema.modo() : 'escuro';
    var largura = Math.max(200, Math.min(400, Math.floor(slot.clientWidth || 320)));
    slot.innerHTML = '';
    window.google.accounts.id.renderButton(slot, {
      theme: modo === 'claro' ? 'outline' : 'filled_black',
      size: 'large',
      shape: 'rectangular',
      width: largura,
      text: texto,
      locale: 'pt-BR'
    });
  }

  window.ronuIniciarBotaoGoogle = function (texto) {
    var slot = document.getElementById('google-signin-button');
    // Sem o script do Google (bloqueador, rede), a página segue sem o botão.
    if (!slot || !window.google || !window.google.accounts) return;

    window.google.accounts.id.initialize({
      client_id: CLIENT_ID,
      callback: window.ronuTratarCredencialGoogle
    });
    desenhar(slot, texto);

    document.addEventListener('ronu:tema', function () { desenhar(slot, texto); });

    var ultimaLargura = slot.clientWidth;
    var espera;
    window.addEventListener('resize', function () {
      clearTimeout(espera);
      espera = setTimeout(function () {
        if (Math.abs(slot.clientWidth - ultimaLargura) < 8) return;
        ultimaLargura = slot.clientWidth;
        desenhar(slot, texto);
      }, 200);
    });
  };
})();
