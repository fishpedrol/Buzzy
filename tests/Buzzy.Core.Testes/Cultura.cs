using System.Globalization;

namespace Buzzy.Core.Testes;

/// <summary>
/// Executa um trecho com uma cultura atual fixa e restaura a anterior. Os testes não podem
/// depender da cultura da máquina: com ICU, culturas como sv-SE escrevem números negativos
/// com U+2212 (sinal de menos tipográfico) em vez do hífen.
/// </summary>
internal static class Cultura
{
    public static T Com<T>(CultureInfo cultura, Func<T> acao)
    {
        CultureInfo anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = cultura;
            return acao();
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    /// <summary>Cultura invariante com o sinal de menos tipográfico (U+2212), como sv-SE com ICU.</summary>
    public static CultureInfo ComMenosTipografico()
    {
        var cultura = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        cultura.NumberFormat.NegativeSign = char.ConvertFromUtf32(0x2212);
        return cultura;
    }
}
