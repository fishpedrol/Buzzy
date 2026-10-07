namespace Buzzy.App;

// Códigos de saída do Buzzy.exe, usados pelos testes e no diagnóstico.
internal static class CodigosDeSaida
{
    internal const int Normal = 0;

    // Não deu pra ler os monitores na partida, nem tentando de novo.
    internal const int TopologiaIlegivel = 3;

    // Rodando como administrador: o Buzzy se recusa a rodar elevado.
    internal const int Elevado = 5;

    // Não deu pra criar nem abrir os objetos da instância única.
    internal const int InstanciaUnicaIndisponivel = 6;
}
