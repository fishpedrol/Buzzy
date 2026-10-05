using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Fase 8, passo F8-P8 (DEC-038, item 9; critério 9): a escala em três passos fixos. O mapa passo → DIP; e o personagem com
/// os três tamanhos, em horas de autonomia com tudo o que o aplicativo liga, nunca sai da união das áreas úteis (fora do
/// gesto do usuário e do esconderijo), inclusive partindo de uma posição salva com outro tamanho. Os itens continuam com
/// 48 DIP.
/// </summary>
internal static class EscalaDoPersonagemTestes
{
    [Teste]
    public static void Passos_96_128_192_EOsItensEm48()
    {
        Afirmar.Sequencia([96, 128, 192], new[] { EscalaDoPersonagem.Pequena, EscalaDoPersonagem.Media, EscalaDoPersonagem.Grande }.Select(e => ConfiguracaoDoNucleo.TamanhoDoPersonagem(e).Largura), "os passos");
        Afirmar.Igual(new TamanhoDip(128, 128), ConfiguracaoDoNucleo.TamanhoDoPersonagem((EscalaDoPersonagem)9), "fora do enum, o Médio");
        foreach (EscalaDoPersonagem e in Enum.GetValues<EscalaDoPersonagem>())
        {
            ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(e);
            Afirmar.Igual((ConfiguracaoDoNucleo.TamanhoDoPersonagem(e), new TamanhoDip(48, 48)), (cfg.Tamanho, cfg.TamanhoDoItem), $"{e}: o personagem no passo, o item em 48");
            Afirmar.Igual(ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with { Tamanho = cfg.Tamanho }, cfg, $"{e}: o resto igual ao aplicativo");
        }
    }

    [Teste]
    public static void TresTamanhos_NaUniaoDasAreasUteis_HorasDeAutonomia()
    {
        Topologia[] topologias = [TopologiasDeExemplo.LadoALado, TopologiasDeExemplo.EmL, TopologiasDeExemplo.EmpilhadoSecundarioAcima, TopologiasDeExemplo.EscalasMistas];
        int eventos = 0;
        foreach (EscalaDoPersonagem escala in Enum.GetValues<EscalaDoPersonagem>())
        {
            foreach (Topologia topologia in topologias)
            {
                // Uma posição salva com o tamanho Médio, numa lateral, restaurada com o tamanho do passo.
                var salva = new PosicaoDoPersonagem(topologia.Principal.Chave, 0.98, 1, new PontoPx(topologia.Principal.AreaUtil.Direita - 64, topologia.Principal.AreaUtil.Base));
                var sim = new SimuladorDeTempo(ConfiguracaoDoNucleo.DoAplicativo(escala), 3, topologia, new Preferencias(NivelDeEnergia.Alta, true), salva);
                sim.AoAplicar = (_, e, depois) =>
                {
                    eventos++;
                    if (depois.Lugar is not { } lugar || !depois.Estado.Visivel() || depois.Estado is Estado.Pressed or Estado.Dragging or Estado.Peeking or Estado.Reacting
                        || depois.Esconderijo != LadoDoEsconderijo.Nenhum) return;
                    Afirmar.Igual(ConfiguracaoDoNucleo.TamanhoDoPersonagem(escala).ParaPixels(lugar.Monitor.Dpi), lugar.Tamanho, $"{escala}: o tamanho do sprite");
                    Afirmar.Verdadeiro(Passagens.NaUniaoDasAreasUteis(topologia, lugar.Retangulo), $"{escala}, {e}, {depois.Estado}: {lugar.Retangulo} na união das áreas úteis");
                };
                sim.Avancar(TimeSpan.FromHours(1));
            }
        }
        Afirmar.Verdadeiro(eventos > 100_000, $"eventos: {eventos}");
    }
}
