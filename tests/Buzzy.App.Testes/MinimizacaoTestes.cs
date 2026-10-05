using Buzzy.App.Composicao;
using Buzzy.Core;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Quem minimizou a janela do personagem (Fase 5, passo P12; DEC-031, adendo): com "Minimizar janelas quando um monitor for
/// desconectado", ligada por padrão no Windows 11, o próprio Windows minimiza as janelas do monitor que sai. Isso não é o
/// usuário escondendo o Buzzy (Q-03): com uma releitura da topologia pendente na agenda, com a leitura de agora diferente da
/// publicada ou incoerente (a troca de modo em andamento), foi o sistema, e a janela volta sem esconder. Só sem nada disso a
/// minimização esconde, como antes.
/// </summary>
internal sealed class MinimizacaoTestes
{
    private static readonly MonitorDoDesktop Principal = new("m1", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1040), 96, true);
    private static readonly MonitorDoDesktop Secundario = new("m2", new RetanguloPx(-1920, 0, 0, 1080), new RetanguloPx(-1920, 0, 0, 1040), 96, false);

    private static readonly Topologia Dois = new([Principal, Secundario]);
    private static readonly Topologia SoOPrincipal = new([Principal]);

    [Teste]
    public void SemReleituraPendente_MesmaTopologia_FoiOUsuario()
        => Afirmar.Falso(Aplicacao.MinimizadaPeloSistema(releituraPendente: false, Dois, lidaAgora: new Topologia([Principal, Secundario])),
            "nada mudou e nada pendente: minimizar esconde (Q-03)");

    [Teste]
    public void ComReleituraPendente_FoiOSistema()
        => Afirmar.Verdadeiro(Aplicacao.MinimizadaPeloSistema(releituraPendente: true, Dois, lidaAgora: Dois),
            "uma mensagem de topologia acabou de chegar: a minimização é da troca de monitores");

    [Teste]
    public void TopologiaDeAgoraDiferenteDaPublicada_FoiOSistema()
        => Afirmar.Verdadeiro(Aplicacao.MinimizadaPeloSistema(releituraPendente: false, Dois, lidaAgora: SoOPrincipal),
            "o monitor saiu antes de a mensagem chegar: a minimização é da troca de monitores");

    [Teste]
    public void LeituraIncoerente_FoiOSistema()
        => Afirmar.Verdadeiro(Aplicacao.MinimizadaPeloSistema(releituraPendente: false, Dois, lidaAgora: null),
            "a troca de modo em andamento deixa a leitura incoerente: é do sistema");
}
