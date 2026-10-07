using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

// Edição escolhida na compilação: -p:BuzzyEdicao=publica tira
// as drogas ilícitas. O nome vai pro log pro empacotador conferir.
internal static class EdicaoDoBuild
{
#if BUZZY_PUBLICO
    internal const EdicaoDoBuzzy Atual = EdicaoDoBuzzy.Publica;
#else
    internal const EdicaoDoBuzzy Atual = EdicaoDoBuzzy.Completa;
#endif

    internal static string NomeNoLog => Atual == EdicaoDoBuzzy.Publica ? "publica" : "completa";
}
