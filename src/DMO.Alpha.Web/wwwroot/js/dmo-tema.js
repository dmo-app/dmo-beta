// Modo claro / escuro (Diogo, 21/10/2026). A escolha fica neste browser
// (localStorage «dmo-tema») e o <html> leva data-theme="dark"; as cores saem
// dos tokens do 0_ASSET_CANONICAL_DESIGN_SYSTEM.css. O <head> aplica a escolha
// antes de desenhar (sem clarão); este ficheiro só trata do botão.
(function () {
    "use strict";
    var root = document.documentElement;
    function escuro() { return root.getAttribute("data-theme") === "dark"; }
    function mostrar(botao) {
        // No telemóvel só se vê o símbolo (dmo-normalize.css): o cabeçalho não cabe.
        var texto = escuro() ? "Modo claro" : "Modo escuro";
        botao.querySelector(".dmo-shell-tema-texto").textContent = texto;
        botao.setAttribute("aria-label", texto);
        botao.setAttribute("aria-pressed", escuro() ? "true" : "false");
    }
    document.querySelectorAll("[data-dmo-tema]").forEach(function (botao) {
        mostrar(botao);
        botao.addEventListener("click", function () {
            var novo = escuro() ? "light" : "dark";
            root.setAttribute("data-theme", novo);
            try { localStorage.setItem("dmo-tema", novo); } catch (e) { /* sem armazenamento: vale só nesta página */ }
            mostrar(botao);
        });
    });
})();
