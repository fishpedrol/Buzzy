using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Buzzy.PortaoApis.Testes.Apoio;

/// <summary>
/// Monta um assembly mínimo, só de metadados e sem código executável, com as referências e
/// declarações pedidas. Cobre o que o compilador não produz com facilidade neste projeto:
/// namespaces WinRT sem projeção, módulos e pontos de entrada arbitrários, grafias estranhas.
/// O PE sai no formato PE32 (I386), com a importação mscoree.dll!_CorDllMain que o
/// ManagedPEBuilder sempre grava nesse formato.
/// </summary>
internal sealed class AssemblySintetico
{
    private readonly MetadataBuilder _md = new();
    private readonly AssemblyReferenceHandle _runtime;
    private readonly TypeReferenceHandle _object;
    private readonly BlobHandle _assinaturaVazia;
    private readonly List<(string Modulo, string Entrada)> _pinvokes = [];
    private readonly List<string> _metodosCom = [];
    private readonly string _namespaceDasDeclaracoes;
    private readonly string _tipoDasDeclaracoes;
    private readonly string? _aninhadoDasDeclaracoes;

    /// <param name="nome">Nome do assembly e do módulo (com .dll).</param>
    /// <param name="namespaceDasDeclaracoes">Namespace do tipo que declara os P/Invoke.</param>
    /// <param name="tipoDasDeclaracoes">Tipo que declara os P/Invoke.</param>
    /// <param name="aninhado">
    /// Se dado, os P/Invoke ficam num tipo com este nome aninhado no tipo acima (<c>Tipo+Aninhado</c>), e o externo fica
    /// sem métodos.
    /// </param>
    public AssemblySintetico(string nome, string namespaceDasDeclaracoes = "Sintetico", string tipoDasDeclaracoes = "Amostra", string? aninhado = null)
    {
        _namespaceDasDeclaracoes = namespaceDasDeclaracoes;
        _tipoDasDeclaracoes = tipoDasDeclaracoes;
        _aninhadoDasDeclaracoes = aninhado;
        _md.AddModule(0, _md.GetOrAddString(nome + ".dll"), _md.GetOrAddGuid(Guid.NewGuid()), default, default);
        _md.AddAssembly(_md.GetOrAddString(nome), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        _runtime = ReferenciarAssembly("System.Runtime");
        _object = _md.AddTypeReference(_runtime, _md.GetOrAddString("System"), _md.GetOrAddString("Object"));

        var assinatura = new BlobBuilder();
        new BlobEncoder(assinatura).MethodSignature().Parameters(0, retorno => retorno.Void(), _ => { });
        _assinaturaVazia = _md.GetOrAddBlob(assinatura);
    }

    public AssemblyReferenceHandle ReferenciarAssembly(string nome)
        => _md.AddAssemblyReference(_md.GetOrAddString(nome), new Version(10, 0, 0, 0), default, default, 0, default);

    public TypeReferenceHandle ReferenciarTipo(string nomeDoNamespace, string nome)
        => _md.AddTypeReference(_runtime, _md.GetOrAddString(nomeDoNamespace), _md.GetOrAddString(nome));

    /// <summary>Tipo aninhado: o escopo de resolução é o tipo externo.</summary>
    public TypeReferenceHandle ReferenciarTipoAninhado(TypeReferenceHandle externo, string nome)
        => _md.AddTypeReference(externo, default, _md.GetOrAddString(nome));

    public void ReferenciarMembro(TypeReferenceHandle tipo, string membro)
        => _md.AddMemberReference(tipo, _md.GetOrAddString(membro), _assinaturaVazia);

    public void ReferenciarMembro(string nomeDoNamespace, string tipo, string membro)
        => ReferenciarMembro(ReferenciarTipo(nomeDoNamespace, tipo), membro);

    public void DeclararPInvoke(string modulo, string entrada) => _pinvokes.Add((modulo, entrada));

    /// <summary>Método numa interface declarada no próprio assembly, como uma interface [ComImport].</summary>
    public void DeclararMetodoCom(string nome) => _metodosCom.Add(nome);

    public void Gravar(string caminho) => File.WriteAllBytes(caminho, Gerar());

    public byte[] Gerar()
    {
        // MethodDef: primeiro os P/Invoke (do tipo das declarações), depois os métodos da interface.
        var modulos = new Dictionary<string, ModuleReferenceHandle>(StringComparer.Ordinal);
        for (int i = 0; i < _pinvokes.Count; i++)
        {
            (string modulo, string entrada) = _pinvokes[i];
            MethodDefinitionHandle metodo = _md.AddMethodDefinition(
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.PinvokeImpl,
                MethodImplAttributes.PreserveSig,
                _md.GetOrAddString($"Metodo{i}"),
                _assinaturaVazia,
                bodyOffset: -1,
                parameterList: MetadataTokens.ParameterHandle(1));
            if (!modulos.TryGetValue(modulo, out ModuleReferenceHandle referencia))
            {
                referencia = _md.AddModuleReference(_md.GetOrAddString(modulo));
                modulos[modulo] = referencia;
            }
            _md.AddMethodImport(metodo, MethodImportAttributes.CallingConventionWinApi, _md.GetOrAddString(entrada), referencia);
        }
        foreach (string nome in _metodosCom)
        {
            _md.AddMethodDefinition(
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                MethodImplAttributes.IL,
                _md.GetOrAddString(nome),
                _assinaturaVazia,
                bodyOffset: -1,
                parameterList: MetadataTokens.ParameterHandle(1));
        }

        FieldDefinitionHandle semCampos = MetadataTokens.FieldDefinitionHandle(1);
        _md.AddTypeDefinition(default, default, _md.GetOrAddString("<Module>"), default, semCampos, MetadataTokens.MethodDefinitionHandle(1));
        TypeDefinitionHandle declarante = _md.AddTypeDefinition(
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class,
            _md.GetOrAddString(_namespaceDasDeclaracoes), _md.GetOrAddString(_tipoDasDeclaracoes), _object, semCampos, MetadataTokens.MethodDefinitionHandle(1));
        if (_aninhadoDasDeclaracoes is not null)
        {
            // O externo fica sem métodos: a lista dele começa onde começa a do aninhado.
            TypeDefinitionHandle aninhado = _md.AddTypeDefinition(
                TypeAttributes.NestedPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class,
                default, _md.GetOrAddString(_aninhadoDasDeclaracoes), _object, semCampos, MetadataTokens.MethodDefinitionHandle(1));
            _md.AddNestedType(aninhado, declarante);
        }
        _md.AddTypeDefinition(
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
            _md.GetOrAddString("Sintetico"), _md.GetOrAddString("IAmostraCom"), default, semCampos,
            MetadataTokens.MethodDefinitionHandle(_pinvokes.Count + 1));

        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(_md), new BlobBuilder(), flags: CorFlags.ILOnly);
        var saida = new BlobBuilder();
        pe.Serialize(saida);
        return saida.ToArray();
    }
}
