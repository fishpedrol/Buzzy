using System.Globalization;
using System.Resources;
using Buzzy.Core.Personagem;

namespace Buzzy.App;

/// <summary>Acesso aos textos de <c>Textos.resx</c> (Q-12: textos fora do código).</summary>
internal static class Textos
{
    private static readonly ResourceManager Recursos = new("Buzzy.App.Textos", typeof(Textos).Assembly);

    internal static string MenuEsconder => Obter(nameof(MenuEsconder));
    internal static string MenuMostrar => Obter(nameof(MenuMostrar));
    internal static string MenuPausar => Obter(nameof(MenuPausar));
    internal static string MenuRetomar => Obter(nameof(MenuRetomar));
    internal static string MenuSair => Obter(nameof(MenuSair));
    internal static string DicaDaBandeja => Obter(nameof(DicaDaBandeja));
    internal static string AvisoElevado => Obter(nameof(AvisoElevado));

    /// <summary>O submenu da emoção dominante (DEC-027).</summary>
    internal static string MenuEmocaoDominante => Obter(nameof(MenuEmocaoDominante));

    /// <summary>A opção "Automática" da emoção dominante: o humor varia como antes da escolha.</summary>
    internal static string MenuEmocaoAutomatica => Obter(nameof(MenuEmocaoAutomatica));

    /// <summary>
    /// O nome de uma das 14 caras de humor (<see cref="Expressoes.DeHumor"/>) no submenu da emoção dominante, com a
    /// tecla de acesso. As caras de efeito do tamagotchi não são emoção dominante: pedir uma delas é erro.
    /// </summary>
    internal static string Emocao(Expressao emocao)
        => Expressoes.EhDeHumor(emocao)
            ? Obter(ChaveDaEmocao(emocao))
            : throw new ArgumentOutOfRangeException(nameof(emocao), emocao, "A emoção dominante é uma das 14 caras de humor.");

    /// <summary>O submenu dos itens do tamagotchi (DEC-028); só existe com a chave dele ligada.</summary>
    internal static string MenuItens => Obter(nameof(MenuItens));

    /// <summary>"Recolher itens": todos os itens saem da tela (CMD_CLEAR_ITEMS).</summary>
    internal static string MenuRecolherItens => Obter(nameof(MenuRecolherItens));

    /// <summary>"Conteúdo adulto": liga ou desliga os itens adultos, as ondas de substância e a paranoia (DEC-033).</summary>
    internal static string MenuConteudoAdulto => Obter(nameof(MenuConteudoAdulto));

    /// <summary>"Desviar da tela cheia": liga ou desliga o modo de tela cheia (Q-09; DEC-034).</summary>
    internal static string MenuModoTelaCheia => Obter(nameof(MenuModoTelaCheia));

    // Fase 8 (DEC-038): o painel de energia, as configurações e as entradas do menu.
    internal static string MenuEnergia => Obter(nameof(MenuEnergia));
    internal static string MenuConfiguracoes => Obter(nameof(MenuConfiguracoes));
    internal static string PainelTitulo => Obter(nameof(PainelTitulo));
    internal static string EnergiaGrupo => Obter(nameof(EnergiaGrupo));
    internal static string EnergiaBaixa => Obter(nameof(EnergiaBaixa));
    internal static string EnergiaMedia => Obter(nameof(EnergiaMedia));
    internal static string EnergiaAlta => Obter(nameof(EnergiaAlta));
    internal static string PainelAjuda => Obter(nameof(PainelAjuda));
    internal static string ConfigTitulo => Obter(nameof(ConfigTitulo));
    internal static string ConfigComportamento => Obter(nameof(ConfigComportamento));
    internal static string ConfigAparencia => Obter(nameof(ConfigAparencia));
    internal static string ConfigWindows => Obter(nameof(ConfigWindows));
    internal static string ConfigEnergiaAjuda => Obter(nameof(ConfigEnergiaAjuda));
    internal static string ConfigTelaCheia => Obter(nameof(ConfigTelaCheia));
    internal static string ConfigTelaCheiaAjuda => Obter(nameof(ConfigTelaCheiaAjuda));
    internal static string ConfigAdulto => Obter(nameof(ConfigAdulto));
    internal static string ConfigAdultoAjuda => Obter(nameof(ConfigAdultoAjuda));
    internal static string ConfigItensAdultos => Obter(nameof(ConfigItensAdultos));
    internal static string ConfigItensAdultosAjuda => Obter(nameof(ConfigItensAdultosAjuda));
    internal static string ConfigItemAdultoAjuda => Obter(nameof(ConfigItemAdultoAjuda));
    internal static string ConfigTravessia => Obter(nameof(ConfigTravessia));
    internal static string ConfigTravessiaAjuda => Obter(nameof(ConfigTravessiaAjuda));
    internal static string ConfigPorContaPropria => Obter(nameof(ConfigPorContaPropria));
    internal static string ConfigPorContaPropriaNome => Obter(nameof(ConfigPorContaPropriaNome));
    internal static string ConfigPorContaPropriaAjuda => Obter(nameof(ConfigPorContaPropriaAjuda));
    internal static string ConfigTamanho => Obter(nameof(ConfigTamanho));
    internal static string ConfigTamanhoPequeno => Obter(nameof(ConfigTamanhoPequeno));
    internal static string ConfigTamanhoMedio => Obter(nameof(ConfigTamanhoMedio));
    internal static string ConfigTamanhoGrande => Obter(nameof(ConfigTamanhoGrande));
    internal static string ConfigTamanhoProximaVez => Obter(nameof(ConfigTamanhoProximaVez));
    internal static string ConfigTopo => Obter(nameof(ConfigTopo));
    internal static string ConfigTopoAjuda => Obter(nameof(ConfigTopoAjuda));
    internal static string ConfigInicio => Obter(nameof(ConfigInicio));
    internal static string ConfigInicioDesativado => Obter(nameof(ConfigInicioDesativado));
    internal static string ConfigInicioOutraCopia => Obter(nameof(ConfigInicioOutraCopia));
    internal static string ConfigInicioIndisponivel => Obter(nameof(ConfigInicioIndisponivel));
    internal static string ConfigInicioAjuda => Obter(nameof(ConfigInicioAjuda));

    internal static string ConfigInicioFalhou => Obter(nameof(ConfigInicioFalhou));

    internal static string ConfigInicioSimulado => Obter(nameof(ConfigInicioSimulado));
    internal static string ConfigFechar => Obter(nameof(ConfigFechar));

    /// <summary>
    /// O nome de um item do tamagotchi (DEC-028) no submenu "Itens", com a tecla de acesso. Só o nome: o menu não descreve
    /// nada. Um valor fora do enum é erro.
    /// </summary>
    internal static string Item(Item item)
        => Enum.IsDefined(item)
            ? Obter(ChaveDoItem(item))
            : throw new ArgumentOutOfRangeException(nameof(item), item, "Item fora do enum.");

    /// <summary>
    /// O nome de um item sem a tecla de acesso do menu Win32 (o "&amp;" de <see cref="Item"/>): o que a janela de
    /// configurações mostra e o leitor de tela lê (DEC-041). Nenhum nome de item tem um "&amp;" literal.
    /// </summary>
    internal static string NomeDoItem(Item item) => Item(item).Replace("&", "", StringComparison.Ordinal);

    /// <summary>Todas as chaves usadas pelo aplicativo, para o teste que confere se nenhuma falta.</summary>
    internal static IReadOnlyList<string> Chaves { get; } =
    [
        nameof(MenuEsconder), nameof(MenuMostrar), nameof(MenuPausar), nameof(MenuRetomar), nameof(MenuSair), nameof(DicaDaBandeja), nameof(AvisoElevado),
        nameof(MenuEmocaoDominante), nameof(MenuEmocaoAutomatica), .. Expressoes.DeHumor.Select(ChaveDaEmocao),
        nameof(MenuItens), nameof(MenuRecolherItens), nameof(MenuConteudoAdulto), nameof(MenuModoTelaCheia), .. TabelaDoTamagotchi.Itens.Select(ChaveDoItem),
        nameof(MenuEnergia), nameof(MenuConfiguracoes), nameof(PainelTitulo), nameof(EnergiaGrupo), nameof(EnergiaBaixa), nameof(EnergiaMedia), nameof(EnergiaAlta), nameof(PainelAjuda), nameof(ConfigTitulo), nameof(ConfigComportamento), nameof(ConfigAparencia), nameof(ConfigWindows), nameof(ConfigEnergiaAjuda), nameof(ConfigTelaCheia), nameof(ConfigTelaCheiaAjuda), nameof(ConfigAdulto), nameof(ConfigAdultoAjuda), nameof(ConfigItensAdultos), nameof(ConfigItensAdultosAjuda), nameof(ConfigItemAdultoAjuda), nameof(ConfigTravessia), nameof(ConfigTravessiaAjuda), nameof(ConfigPorContaPropria), nameof(ConfigPorContaPropriaNome), nameof(ConfigPorContaPropriaAjuda), nameof(ConfigTamanho), nameof(ConfigTamanhoPequeno), nameof(ConfigTamanhoMedio), nameof(ConfigTamanhoGrande), nameof(ConfigTamanhoProximaVez), nameof(ConfigTopo), nameof(ConfigTopoAjuda), nameof(ConfigInicio), nameof(ConfigInicioDesativado), nameof(ConfigInicioOutraCopia), nameof(ConfigInicioIndisponivel), nameof(ConfigInicioAjuda), nameof(ConfigInicioFalhou), nameof(ConfigInicioSimulado), nameof(ConfigFechar),
    ];

    internal static string Obter(string chave)
        => Recursos.GetString(chave, CultureInfo.InvariantCulture)
           ?? throw new InvalidOperationException($"Texto ausente em Textos.resx: {chave}");

    /// <summary>A chave do nome de uma cara de humor no .resx: <c>Emocao</c> seguido do nome do valor (<c>EmocaoFeliz</c>).</summary>
    private static string ChaveDaEmocao(Expressao emocao) => "Emocao" + emocao;

    /// <summary>A chave do nome de um item no .resx: <c>Item</c> seguido do nome do valor (<c>ItemBanana</c>).</summary>
    private static string ChaveDoItem(Item item) => "Item" + item;
}
