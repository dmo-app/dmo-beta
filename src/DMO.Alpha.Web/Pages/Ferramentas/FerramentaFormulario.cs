using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Web.Pages.Ferramentas;

/// <summary>
/// UM SÓ FORMULÁRIO DE FERRAMENTA (Diogo, 20/10/2026: «ja falei muit vez
/// sobre isto, temos varias paginas criar ferramenta»): o Criar e o Editar
/// da ficha desenham os mesmos campos, com os mesmos nomes e a mesma ordem
/// (_FerramentaFactos e _FerramentaTecnica). Só os ids e os valores mudam.
/// </summary>
/// <param name="Prefixo">O início dos ids («ferr-new» no Criar, «ferr-edit» no Editar).</param>
/// <param name="Criar">No Criar o tipo muda os campos no browser (ferramentas-live.js) e a quantidade vem sugerida.</param>
/// <param name="TipoFixo">Vindo do Job On, o tipo escolhido; os outros ficam indisponíveis.</param>
public sealed record FerramentaFactosForm(
    string Prefixo,
    bool Criar,
    string Tipo,
    string? TipoFixo,
    string? Referencia,
    string? Lote,
    string? Quantidade,
    string? QuantidadeManual,
    string? RefMf,
    string? NomeTecnico,
    IReadOnlyList<MachineOption> Maquinas,
    Func<int, bool> MaquinaEscolhida,
    string? FabricaOrigem = null,
    string? UsoPercentagem = null,
    string? UsoData = null);

/// <param name="Prefixo">O início dos ids («ferr-new» no Criar, «ferr-tech» na ficha).</param>
/// <param name="Criar">No Criar vêm os campos de todos os tipos (o browser mostra os do tipo escolhido); na ficha, só os do tipo da ferramenta.</param>
/// <param name="Valor">O valor de cada campo pelo nome do POST (process_token, nominal_weight, …).</param>
/// <param name="Invalido">«true» quando o valor escrito não se leu (aria-invalid).</param>
/// <param name="Foco">O campo que fica com o foco depois de uma recusa.</param>
/// <param name="VolumeTampao">O volume calculado do tampão (só a ficha o mostra).</param>
public sealed record FerramentaTecnicaForm(
    string Prefixo,
    bool Criar,
    string Tipo,
    Func<string, string?> Valor,
    Func<string, string?> Invalido,
    string? Foco,
    string? VolumeTampao);
