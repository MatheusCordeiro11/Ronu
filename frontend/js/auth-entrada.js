// Ronu — Entrada do formulário nas telas de autenticação
// O .auth-form-wrap anima ao aparecer (fade + subida, ver auth.css), exceto
// quando a página inteira já está chegando pelo slide da View Transition
// entre login e cadastro (páginas com [data-auth-tela]) — aí o slide já é a
// entrada e uma segunda animação brigaria com ele. No fade padrão das outras
// telas (estado, onboarding) o formulário anima normalmente por dentro dele.
//
// Carregado no <head>, sem defer: o pagereveal dispara antes do primeiro
// quadro, então o ouvinte tem de existir antes disso. Sem suporte ao evento,
// nada é marcado e a animação simplesmente roda (padrão no CSS).
window.addEventListener('pagereveal', (evento) => {
  const raiz = document.documentElement;
  if (evento.viewTransition && raiz.hasAttribute('data-auth-tela')) {
    raiz.setAttribute('data-auth-entrada', 'transicao');
  }
});
