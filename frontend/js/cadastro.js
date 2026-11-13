// Ronu — Tela de cadastro
// Após criar a conta, faz login automaticamente com as mesmas credenciais
// (POST /api/auth/cadastro não devolve token; quem devolve é POST /api/auth/login).

document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('cadastro-form');
  const formError = document.getElementById('form-error');
  const submitBtn = document.getElementById('submit-btn');
  const campoConfirmarSenha = document.getElementById('field-confirmar-senha');
  const confirmarSenhaErro = document.getElementById('confirmar-senha-error');
  const caixaAceite = document.getElementById('aceite');

  // Etapas do envio que já deram certo nesta página (ver o submit).
  let contaCriada = false;
  let sessaoIniciada = false;

  // O texto da autorização vem da rota pública: ainda não há conta.
  RonuAceite.iniciar(caixaAceite);
  RonuAceite.carregar(caixaAceite);

  // Desde o redesign (tela aprovada Cadastro, out/2026) todos os campos
  // aparecem de uma vez; a revelação um por um saiu junto com o accordeon.

  form.addEventListener('submit', async (evento) => {
    evento.preventDefault();

    if (!form.checkValidity()) {
      form.reportValidity();
      return;
    }

    ronuOcultarErroFormulario(formError);
    ronuLimparCampoInvalido(campoConfirmarSenha, confirmarSenhaErro);

    const nome = document.getElementById('nome').value.trim();
    const email = document.getElementById('email').value.trim();
    const senha = document.getElementById('senha').value;
    const confirmarSenha = document.getElementById('confirmar-senha').value;
    const estado = document.getElementById('estado').value;

    if (confirmarSenha !== senha) {
      ronuMarcarCampoInvalido(campoConfirmarSenha, confirmarSenhaErro, 'As senhas não coincidem.');
      return;
    }

    // A conta já criada numa tentativa anterior já levou o aceite.
    const versaoConsentimento = contaCriada ? null : RonuAceite.versaoMarcada(caixaAceite);
    if (!contaCriada && versaoConsentimento == null) return;

    ronuDefinirCarregando(submitBtn, true, 'Criando conta...');
    // Um aviso só para o fluxo inteiro (cadastro + login automático +
    // redirecionamento): a demora pode cair em qualquer uma das chamadas.
    const cancelarAvisoDeEspera = ronuAvisoDeEspera(submitBtn.querySelector('.btn-label'));

    try {
      // Um novo envio depois de uma falha retoma de onde parou: se a conta já
      // foi criada (ex.: a checagem pós-login falhou com a API instável),
      // repetir o cadastro daria "email já cadastrado".
      if (!contaCriada) {
        await ronuCadastrar(nome, email, senha, estado, versaoConsentimento);
        contaCriada = true;
      }
      if (!sessaoIniciada) {
        await ronuLogin(email, senha);
        sessaoIniciada = true;
      }
      await ronuRedirecionarPosAuth();
    } catch (erro) {
      ronuDefinirCarregando(submitBtn, false);
      // O texto mudou entre abrir a tela e enviar: a caixa mostra o novo, desmarcada.
      if (erro.textoMudou && !contaCriada) {
        await RonuAceite.textoMudou(caixaAceite, erro.message);
        return;
      }
      ronuMostrarErroFormulario(formError, erro.message);
    } finally {
      cancelarAvisoDeEspera();
    }
  });
});

// O script do Google carrega com async: o botão só é desenhado no load. Fica
// aqui, e não num <script> inline no HTML, porque a CSP não libera script
// inline. No load o js/base/google.js (carregado depois deste) já rodou.
window.addEventListener('load', () => ronuIniciarBotaoGoogle('signup_with'));
