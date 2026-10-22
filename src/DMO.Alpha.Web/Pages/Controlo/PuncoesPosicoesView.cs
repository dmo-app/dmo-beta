using DMO.Alpha.Infrastructure.Controlo;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>O cartão das punções por posição da linha (_PuncoesPosicoes): a grelha e o nome da linha.</summary>
public sealed record PuncoesPosicoesView(PuncoesNaLinha Puncoes, string Linha);
