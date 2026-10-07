using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

// Valores de QUERY_USER_NOTIFICATION_STATE (SHQueryUserNotificationState). É só um
// sinal auxiliar da tela cheia, nunca a única fonte.
internal enum EstadoDoShell
{
    // Protetor de tela, sessão bloqueada ou outra sessão ativa.
    NaoPresente = 1,

    // App em tela cheia, ou modo apresentação ligado.
    Ocupado = 2,

    // Direct3D em tela cheia exclusiva.
    D3dEmTelaCheia = 3,

    Apresentacao = 4,

    AceitaNotificacoes = 5,

    // Primeira hora depois de instalar ou atualizar o Windows.
    HoraQuieta = 6,

    // App da Store rodando (em tela cheia, no Windows 8).
    App = 7,
}

// Lido da janela em primeiro plano e descartado logo depois da avaliação: nada de
// título, conteúdo ou processo. Janela em px físicos, nula se não houver ou o Windows
// não informar. DoBuzzy (o menu do próprio Buzzy) mantém o que estava.
internal readonly record struct LeituraDoPrimeiroPlano(RetanguloPx? Janela, EstadoDoShell? Shell, bool DoBuzzy);

// Vai pro núcleo e pro log TELA_CHEIA. Candidatos conta os monitores cobertos antes
// de olhar o shell, só pra diagnóstico.
internal readonly record struct MudancaDaTelaCheia(MonitoresOcupados Ocupados, string Motivos, EstadoDoShell? Shell, int Candidatos);

// Quando reavaliar a tela cheia. Leitura, topologia e agendador injetados pra testar
// sem janela. Só disparos únicos; parado, nenhum timer. Só na thread da interface.
// - Cada sinal (troca de foco, geometria da janela do foco, topologia nova) agenda a
//   avaliação 250 ms depois do primeiro da rajada, sem adiar com os seguintes: janela
//   arrastada sem parar é reavaliada no máximo uma vez por espera.
// - Ocupado = monitor cuja tela inteira está dentro da janela do foco E o shell diz
//   tela cheia. Sem o shell, a área de trabalho (cobre todos) e janela maximizada com a
//   barra escondida enganariam.
// - Só publica quando o conjunto muda.
internal sealed class AgendaDaTelaCheia
{
    // Junta a troca de foco com o redimensionamento que costuma vir junto.
    internal static readonly TimeSpan Espera = TimeSpan.FromMilliseconds(250);

    // O excedente só é contado (",+k" no fim).
    internal const int MaximoDeMotivos = 8;

    private readonly Func<LeituraDoPrimeiroPlano> _ler;
    private readonly Func<Topologia?> _topologia;
    private readonly Func<TimeSpan, Action, Action> _agendarUmaVez;
    private readonly Action<MudancaDaTelaCheia> _publicar;
    private readonly Action<string>? _aoCandidatoDoFoco;
    private readonly List<string> _motivos = [];
    private int _motivosAlemDoTeto;
    private Action? _cancelar;
    private bool _parada;

    // topologia: a mesma publicada pro núcleo. aoCandidatoDoFoco recebe o monitor do
    // foco a cada avaliação, pra curiosidade confirmar depois da carência.
    internal AgendaDaTelaCheia(Func<LeituraDoPrimeiroPlano> ler, Func<Topologia?> topologia, Func<TimeSpan, Action, Action> agendarUmaVez, Action<MudancaDaTelaCheia> publicar,
        Action<string>? aoCandidatoDoFoco = null)
    {
        _aoCandidatoDoFoco = aoCandidatoDoFoco;
        _ler = ler ?? throw new ArgumentNullException(nameof(ler));
        _topologia = topologia ?? throw new ArgumentNullException(nameof(topologia));
        _agendarUmaVez = agendarUmaVez ?? throw new ArgumentNullException(nameof(agendarUmaVez));
        _publicar = publicar ?? throw new ArgumentNullException(nameof(publicar));
    }

    internal MonitoresOcupados Publicados { get; private set; } = MonitoresOcupados.Nenhum;

    // Só pra linha de resumo no fim do log.
    internal long VaosPedidos { get; private set; }

    internal bool AvaliacaoPendente => _cancelar is not null;

    internal void Sinalizar(string motivo)
    {
        if (_parada) return;
        GuardarMotivo(motivo);
        if (_cancelar is not null) return;
        _cancelar = _agendarUmaVez(Espera, () =>
        {
            _cancelar = null;
            Avaliar();
        });
    }

    // Usado na partida: cancela a pendente e leva os motivos dela.
    internal void AvaliarAgora(string motivo)
    {
        if (_parada) return;
        GuardarMotivo(motivo);
        _cancelar?.Invoke();
        _cancelar = null;
        Avaliar();
    }

    internal void Parar()
    {
        _parada = true;
        _cancelar?.Invoke();
        _cancelar = null;
    }

    // Monitores com a tela inteira dentro da janela, em ordem de chave.
    internal static IReadOnlyList<string> Cobertos(Topologia topologia, RetanguloPx janela)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        if (janela.Largura <= 0 || janela.Altura <= 0) return [];
        return [.. topologia.Monitores.Where(m => janela.Contem(m.Tela)).Select(m => m.Chave).Order(StringComparer.Ordinal)];
    }

    // Modo apresentação sozinho, sessão ausente e consulta que falhou não contam.
    internal static bool ShellEmTelaCheia(EstadoDoShell? estado)
        => estado is EstadoDoShell.Ocupado or EstadoDoShell.D3dEmTelaCheia or EstadoDoShell.App;

    // O monitor com maior interseção com a área útil; empate fica com a menor chave.
    // Nulo se a janela está fora de toda área útil (minimizada, barra de tarefas) ou
    // cobre 2+ monitores inteiros (a área de trabalho).
    internal static string? MonitorDoFoco(Topologia topologia, RetanguloPx janela)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        if (janela.Vazio || Cobertos(topologia, janela).Count >= 2) return null;
        string? melhor = null;
        long maior = 0;
        foreach (MonitorDoDesktop m in topologia.Monitores.OrderBy(m => m.Chave, StringComparer.Ordinal))
        {
            long area = Intersecao(janela, m.AreaUtil);
            if (area > maior) (melhor, maior) = (m.Chave, area);
        }
        return melhor;
    }

    // Trecho horizontal da janela dentro da área útil, em 16 avos da largura da área,
    // arredondado pra fora (início pra baixo, fim pra cima).
    internal static VaoDaJanela? Vao(MonitorDoDesktop monitor, RetanguloPx janela)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        RetanguloPx area = monitor.AreaUtil;
        int esquerda = Math.Max(janela.Esquerda, area.Esquerda), direita = Math.Min(janela.Direita, area.Direita);
        if (area.Largura <= 0 || direita <= esquerda || Intersecao(janela, area) == 0) return null;
        int inicio = (int)((long)(esquerda - area.Esquerda) * VaoDaJanela.Partes / area.Largura);
        int fim = (int)(((long)(direita - area.Esquerda) * VaoDaJanela.Partes + area.Largura - 1) / area.Largura);
        return new VaoDaJanela(Math.Clamp(inicio, 0, VaoDaJanela.Partes - 1), Math.Clamp(fim, inicio + 1, VaoDaJanela.Partes));
    }

    // A pedido do núcleo: lê e converte na hora, sem guardar nada.
    internal VaoDaJanela? LerVao(string chave)
    {
        if (_parada) return null;
        VaosPedidos++;
        LeituraDoPrimeiroPlano leitura = _ler();
        if (leitura.DoBuzzy || leitura.Janela is not { } janela || _topologia()?.PorChave(chave) is not { } monitor) return null;
        return Vao(monitor, janela);
    }

    private static long Intersecao(RetanguloPx a, RetanguloPx b)
    {
        long largura = Math.Min(a.Direita, b.Direita) - Math.Max(a.Esquerda, b.Esquerda);
        long altura = Math.Min(a.Base, b.Base) - Math.Max(a.Topo, b.Topo);
        return largura > 0 && altura > 0 ? largura * altura : 0;
    }

    private void Avaliar()
    {
        if (_parada) return;
        string motivos = TirarMotivos();
        LeituraDoPrimeiroPlano leitura = _ler();
        if (leitura.DoBuzzy || _topologia() is not { } topologia) return;
        if (_aoCandidatoDoFoco is not null && leitura.Janela is { } daJanela && MonitorDoFoco(topologia, daJanela) is { } foco) _aoCandidatoDoFoco(foco);

        IReadOnlyList<string> cobertos = leitura.Janela is { } janela ? Cobertos(topologia, janela) : [];
        var ocupados = ShellEmTelaCheia(leitura.Shell) ? new MonitoresOcupados(cobertos) : MonitoresOcupados.Nenhum;
        if (ocupados.Equals(Publicados)) return;
        Publicados = ocupados;
        _publicar(new MudancaDaTelaCheia(ocupados, motivos, leitura.Shell, cobertos.Count));
    }

    private void GuardarMotivo(string motivo)
    {
        if (_motivos.Count < MaximoDeMotivos) _motivos.Add(motivo);
        else _motivosAlemDoTeto++;
    }

    private string TirarMotivos()
    {
        string texto = string.Join(",", _motivos) + (_motivosAlemDoTeto > 0 ? $",+{_motivosAlemDoTeto}" : "");
        _motivos.Clear();
        _motivosAlemDoTeto = 0;
        return texto;
    }
}
