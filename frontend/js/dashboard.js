// Ronu — Placeholder de área logada
// Só confirma que o login funcionou; o painel real (objetivo, modalidades,
// preferências, dieta) ainda não foi construído.

document.addEventListener('DOMContentLoaded', () => {
  const usuario = ronuUsuarioLogado();

  if (!usuario) {
    window.location.href = 'login.html';
    return;
  }

  document.getElementById('boas-vindas').textContent = `Bem-vindo, ${usuario.nome}`;

  document.getElementById('logout-btn').addEventListener('click', () => {
    ronuLimparSessao();
    window.location.href = 'login.html';
  });
});
