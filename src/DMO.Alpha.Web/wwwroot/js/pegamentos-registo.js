// O registo dos Pegamentos na própria tabela (Diogo, 17/10/2026): a
// ovalização e a média calculam-se ao escrever, Enter regista ou guarda,
// Esc cancela a correção e o × pede confirmação antes de apagar. O servidor
// volta a calcular tudo; isto é só a pré-visualização.
(function () {
  function numero(texto) {
    if (texto == null) return null;
    var limpo = String(texto).trim().replace(',', '.');
    if (limpo === '') return null;
    var valor = Number(limpo);
    return isFinite(valor) ? valor : null;
  }

  function fixo(valor) { return valor.toFixed(2); }

  document.querySelectorAll('[data-peg-registo]').forEach(function (tabela) {
    var card = tabela.closest('[data-peg-component]');
    var tolerancia = numero(tabela.getAttribute('data-peg-tolerancia')) || 0;
    var campoNominal = card ? card.querySelector('[data-peg-nominal]') : null;
    var minimo = card ? card.querySelector('[data-peg-min]') : null;
    var maximo = card ? card.querySelector('[data-peg-max]') : null;

    function nominal() {
      return campoNominal ? numero(campoNominal.value) : numero(tabela.getAttribute('data-peg-nominal-valor'));
    }

    function calcular(linha) {
      var c = numero(linha.querySelector('[data-peg-costura]').value);
      var n = numero(linha.querySelector('[data-peg-contra]').value);
      var oval = c != null && n != null ? c - n : null;
      var media = c != null && n != null ? (c + n) / 2 : c;
      var nom = nominal();
      var celOval = linha.querySelector('[data-peg-oval]');
      var celMedia = linha.querySelector('[data-peg-media]');
      celOval.textContent = oval == null ? '—' : (oval >= 0 ? '+' : '-') + fixo(Math.abs(oval));
      celOval.classList.toggle('peg-cell-bad', oval != null && Math.abs(oval) > tolerancia);
      celMedia.textContent = media == null ? '—' : fixo(media);
      celMedia.classList.toggle('peg-cell-bad', media != null && nom != null && Math.abs(media - nom) > tolerancia);
    }

    function ligar(linha) {
      linha.querySelectorAll('input').forEach(function (campo) {
        campo.addEventListener('input', function () { calcular(linha); });
        campo.addEventListener('keydown', function (evento) {
          if (evento.key === 'Enter') {
            evento.preventDefault();
            if (campo.form && campo.form.reportValidity()) campo.form.requestSubmit();
          } else if (evento.key === 'Escape' && linha.hasAttribute('data-peg-cancelar')) {
            window.location.href = linha.getAttribute('data-peg-cancelar');
          }
        });
      });
      calcular(linha);
    }

    var linhas = Array.prototype.slice.call(tabela.querySelectorAll('[data-peg-linha-nova]'));
    linhas.forEach(ligar);

    // «+ Linha»: copia a última linha nova, vazia e com o número seguinte.
    var mais = card ? card.querySelector('[data-peg-mais-linha]') : null;
    var modelo = linhas.filter(function (l) { return !l.hasAttribute('data-peg-cancelar'); }).pop();
    if (mais && modelo) {
      mais.hidden = false;
      mais.addEventListener('click', function () {
        var ultima = linhas.filter(function (l) { return !l.hasAttribute('data-peg-cancelar'); }).pop();
        var nova = ultima.cloneNode(true);
        nova.querySelectorAll('input').forEach(function (campo) {
          campo.removeAttribute('id');
          campo.removeAttribute('required');
          campo.value = '';
        });
        nova.cells[0].textContent = String(Number(ultima.cells[0].textContent) + 1);
        ultima.parentNode.insertBefore(nova, ultima.nextSibling);
        linhas.push(nova);
        ligar(nova);
        nova.querySelector('[data-peg-costura]').focus();
      });
    }

    if (campoNominal) {
      var atualizarLimites = function () {
        var nom = nominal();
        if (minimo) minimo.textContent = nom == null ? '—' : fixo(nom - tolerancia);
        if (maximo) maximo.textContent = nom == null ? '—' : fixo(nom + tolerancia);
        linhas.forEach(calcular);
      };
      campoNominal.addEventListener('input', atualizarLimites);
      atualizarLimites();
    }
  });

  document.querySelectorAll('[data-peg-apagar]').forEach(function (botao) {
    botao.addEventListener('click', function (evento) {
      if (!window.confirm('Apagar esta leitura?')) evento.preventDefault();
    });
  });
})();
