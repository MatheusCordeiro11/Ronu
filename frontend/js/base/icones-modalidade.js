// Ronu — Ícone 3D de cada modalidade (redesign de nov/2026)
//
// RonuIconeModalidade(nome, prefixo) devolve o caminho do WebP da modalidade,
// pelo nome que a API devolve (sem diferenciar acento nem maiúscula). Cinco
// modalidades usam um ícone da base; as outras têm o próprio arquivo em
// img/modalidades/ (docs/icones-3d.md). Modalidade nova sem ícone usa o halter
// até ganhar o dela. `prefixo` é o caminho até a raiz do frontend ('' nas
// páginas da raiz, '../../' na landing).

window.RonuIconeModalidade = (function () {
  'use strict';

  var BASE = {
    'jiu-jitsu': 'icones/quimono',
    'muay thai': 'icones/bandagem',
    'boxe': 'icones/luva',
    'musculacao': 'icones/halter',
    'natacao': 'icones/oculos'
  };
  var PROPRIAS = {
    'judo': 'judo', 'karate': 'karate', 'mma': 'mma', 'wrestling': 'wrestling', 'taekwondo': 'taekwondo',
    'kickboxing': 'kickboxing', 'capoeira': 'capoeira', 'futebol': 'futebol', 'basquete': 'basquete',
    'volei': 'volei', 'futsal': 'futsal', 'handebol': 'handebol', 'tenis': 'tenis', 'beach tennis': 'beach',
    'corrida': 'corrida', 'ciclismo': 'ciclismo', 'caminhada': 'caminhada', 'treino funcional': 'funcional',
    'danca': 'danca', 'yoga': 'yoga', 'pilates': 'pilates'
  };

  function chave(nome) {
    return String(nome || '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();
  }

  return function (nome, prefixo) {
    var k = chave(nome);
    var arquivo = BASE[k] || (PROPRIAS[k] ? 'modalidades/' + PROPRIAS[k] : 'icones/halter');
    return (prefixo || '') + 'img/' + arquivo + '.webp';
  };
})();
