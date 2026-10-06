using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// A travessia entre monitores desligada por padrão (DEC-046, pedido do usuário de 2026-10-06: "deixe desativado ... quando
/// ele tá ativado ele fica bugado em quem não tem 2 telas"), ligável nas Configurações: o padrão, o comando que grava a
/// escolha e o esquema v7, em que um arquivo de versão anterior (gravado sem que ninguém tivesse escolhido, porque não havia
/// a opção) vale o padrão novo. O esperado vem da decisão, escrito aqui à parte do núcleo.
/// </summary>
internal static class TravessiaDesligadaPorPadraoTestes
{
    [Teste]
    public static void Padrao_Desligada()
        => Afirmar.Falso(Preferencias.Padrao.AtravessarMonitores, "a travessia desligada por padrão");

    // O comando grava a escolha numa transição para o mesmo estado; repetir não faz nada; antes da carga, nada.
    [Teste]
    public static void Comando_GravaAEscolha()
    {
        var c = new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        c.Aplicar(new CmdSetCrossMonitors(true));
        Afirmar.Verdadeiro(c.Atual.Preferencias.AtravessarMonitores, "ligada");
        Afirmar.Verdadeiro(c.Efeito<GravarPreferencias>().Preferencias.AtravessarMonitores, "a escolha gravada");
        Afirmar.Igual((Estado.Idle, Estado.Idle, "CMD_SET_CROSS_MONITORS: ligado"), (c.Transicoes[0].De, c.Transicoes[0].Para, c.Transicoes[0].Regra), "no mesmo estado");
        c.Aplicar(new CmdSetCrossMonitors(true)).SemTransicao().SemEfeito<GravarPreferencias>();
        c.Aplicar(new CmdSetCrossMonitors(false));
        Afirmar.Falso(c.Atual.Preferencias.AtravessarMonitores, "desligada de novo");

        Resultado antesDaCarga = Maquina.Aplicar(EstadoDoNucleo.Inicial(1), new CmdSetCrossMonitors(true), new ConfiguracaoDoNucleo());
        Afirmar.Igual((Preferencias.Padrao, 0), (antesDaCarga.Estado.Preferencias, antesDaCarga.Efeitos.Count), "antes da carga: ignorado");
    }

    // Até a v6, "atravessarMonitores" era gravado sempre ligado, sem escolha do usuário: lido, vale o padrão novo (desligado).
    // Na v7, a escolha gravada vale.
    [Teste]
    public static void Esquema_AnteriorValeOPadrao_V7ValeOGravado()
    {
        foreach (int versao in new[] { 1, 5, 6 })
        {
            LeituraDasConfiguracoes antiga = EsquemaDeConfiguracoes.Ler(System.Text.Encoding.UTF8.GetBytes(
                $$$"""{"schemaVersion": {{{versao}}}, "preferencias": {"atravessarMonitores": true}}"""));
            Afirmar.Falso(antiga.Configuracoes.Preferencias.AtravessarMonitores, $"v{versao}: o padrão novo");
        }
        LeituraDasConfiguracoes v7 = EsquemaDeConfiguracoes.Ler("""{"schemaVersion": 7, "preferencias": {"atravessarMonitores": true}}"""u8.ToArray());
        Afirmar.Verdadeiro(v7.Configuracoes.Preferencias.AtravessarMonitores, "v7: a escolha gravada");
        LeituraDasConfiguracoes semCampo = EsquemaDeConfiguracoes.Ler("""{"schemaVersion": 7, "preferencias": {}}"""u8.ToArray());
        Afirmar.Falso(semCampo.Configuracoes.Preferencias.AtravessarMonitores, "v7 sem o campo: o padrão");
    }
}
