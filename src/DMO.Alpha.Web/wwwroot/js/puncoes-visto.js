// PU NNPB, «Punção visto» / «Arrefecedor visto» (Diogo, 27/10/2026: sem
// botões nas linhas). Um clique escolhe a punção na lista; os dois botões do
// canto enviam o formulário #puVistoForm com campo=puncao|arrefecedor. Aqui
// junta-se o «visto», o contrário do estado da linha escolhida
// (data-pu-puncao / data-pu-arrefecedor): carregar duas vezes no mesmo botão
// sem a resposta chegar não desfaz a primeira. Sem este ficheiro, o
// servidor troca o estado que a punção tem.
(function () {
  'use strict';

  // Em captura: antes do envio parcial da casa (no document, sem captura).
  document.addEventListener('submit', function (evento) {
    var form = evento.target;
    if (!form || !form.matches || !form.matches('[data-pu-visto-form]')) return;
    var botao = evento.submitter;
    var campo = botao && botao.name === 'campo' ? botao.value : null;
    var antigo = form.querySelector('input[name="visto"]');
    if (antigo) antigo.remove();
    var escolhida = document.querySelector('input[name="inspecao_id"][form="' + form.id + '"]:checked');
    var linha = escolhida && escolhida.closest('tr');
    if (!campo || !linha || !linha.hasAttribute('data-pu-' + campo)) return;
    var visto = document.createElement('input');
    visto.type = 'hidden';
    visto.name = 'visto';
    visto.value = linha.getAttribute('data-pu-' + campo) === 'true' ? 'false' : 'true';
    form.appendChild(visto);
  }, true);
})();
