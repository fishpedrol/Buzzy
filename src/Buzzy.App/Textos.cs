using System.Globalization;
using System.Resources;
using Buzzy.Core.Personagem;

namespace Buzzy.App;

// Textos da interface ficam no Textos.resx, não no código.
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

    internal static string MenuEmocaoDominante => Obter(nameof(MenuEmocaoDominante));

    // "Automática": o humor volta a variar sozinho.
    internal static string MenuEmocaoAutomatica => Obter(nameof(MenuEmocaoAutomatica));

    // Com a tecla de acesso. Só as 14 caras de humor; as caras de efeito do tamagotchi
    // não são emoção dominante, então pedir uma é erro.
    internal static string Emocao(Expressao emocao)
        => Expressoes.EhDeHumor(emocao)
            ? Obter(ChaveDaEmocao(emocao))
            : throw new ArgumentOutOfRangeException(nameof(emocao), emocao, "A emoção dominante é uma das 14 caras de humor.");

    // Só aparece com o tamagotchi ligado.
    internal static string MenuItens => Obter(nameof(MenuItens));

    internal static string MenuRecolherItens => Obter(nameof(MenuRecolherItens));

    // Liga/desliga itens adultos, ondas de substância e paranoia.
    internal static string MenuConteudoAdulto => Obter(nameof(MenuConteudoAdulto));

    internal static string MenuModoTelaCheia => Obter(nameof(MenuModoTelaCheia));

    // Painel de energia e janela de configurações.
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

    // Só o nome, com a tecla de acesso; o menu não descreve o item.
    internal static string Item(Item item)
        => Enum.IsDefined(item)
            ? Obter(ChaveDoItem(item))
            : throw new ArgumentOutOfRangeException(nameof(item), item, "Item fora do enum.");

    // Sem o "&" da tecla de acesso, pra janela de configurações e o leitor de tela.
    // Pode tirar todos: nenhum nome de item tem "&" de verdade.
    internal static string NomeDoItem(Item item) => Item(item).Replace("&", "", StringComparison.Ordinal);

    // Pro teste que confere se nenhuma chave falta no .resx.
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

    // Ex.: EmocaoFeliz.
    private static string ChaveDaEmocao(Expressao emocao) => "Emocao" + emocao;

    // Ex.: ItemBanana.
    private static string ChaveDoItem(Item item) => "Item" + item;
}
