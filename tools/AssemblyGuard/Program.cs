using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// THE SOFT-DEPENDENCY GUARD
//
// Usage: dotnet run --project tools\AssemblyGuard -- <assembly.dll> <ForbiddenAssembly> [...]
//
// A Bannerlord module assembly is loaded by Module.CollectModuleAssemblyTypes, which calls
// Assembly.GetTypes() and turns ANY load failure into AssemblyLoadResult.CriticalError — the
// startup error dialog, mod not loaded, no recovery. GetTypes() loads every type, and loading a
// type EAGERLY resolves its base type, its interfaces and its field types. So a single class in
// the module assembly that derives from (or holds a field of) a type in an OPTIONAL module's
// assembly makes the whole mod fail to start for everyone who lacks that module — while working
// perfectly on the developer's machine, where the optional module is installed.
//
// That is exactly how v1.3.0-v1.3.3 shipped broken for every player without War Sails
// (Nexus 2026.07.28): one deployment controller deriving from a NavalDLC type.
//
// Method BODIES are safe — they are JIT-compiled on first call, so an optional module's types
// may be used freely inside a method that only runs when that module is present. Method
// SIGNATURES are reported as warnings: they resolve when someone reflects over the member
// (some mod frameworks do), but never during the plain type load GetTypes() performs.
//
// Exit code 0 = clean, 1 = a forbidden assembly appears in the type surface, 2 = bad usage.

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: AssemblyGuard <assembly.dll> <ForbiddenAssembly> [more...]");
    return 2;
}

var target = args[0];
var forbidden = args.Skip(1).ToHashSet(StringComparer.OrdinalIgnoreCase);

using var stream = File.OpenRead(target);
using var pe = new PEReader(stream);
var reader = pe.GetMetadataReader();

var errors = new List<string>();
var warnings = new List<string>();

string TypeName(TypeDefinition type)
{
    var ns = reader.GetString(type.Namespace);
    var name = reader.GetString(type.Name);
    return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
}

// The assembly a TypeReference ultimately comes from — resolution scope can chain through
// nested-type references, so walk until an AssemblyReference (or nothing) is found.
string? ScopeAssembly(TypeReferenceHandle handle)
{
    var seen = 0;
    while (seen++ < 16)
    {
        var reference = reader.GetTypeReference(handle);
        switch (reference.ResolutionScope.Kind)
        {
            case HandleKind.AssemblyReference:
                var asm = reader.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope);
                return reader.GetString(asm.Name);
            case HandleKind.TypeReference:
                handle = (TypeReferenceHandle)reference.ResolutionScope;
                continue;
            default:
                return null; // ModuleDefinition / ModuleReference — our own assembly
        }
    }
    return null;
}

// Signature blobs (fields, methods) name their types as handles too — this provider collects the
// forbidden assembly names out of whatever shape the signature takes.
var provider = new AssemblyCollectingProvider(reader, forbidden, ScopeAssembly);

// A base type or interface reaches us either as a plain TypeReference or — when it is generic,
// like MCM's AttributeGlobalSettings<T> — as a TypeSpecification whose signature blob must be
// decoded. Miss the second shape and the guard blesses exactly the class it exists to catch.
IEnumerable<string> ForbiddenIn(EntityHandle handle)
{
    switch (handle.Kind)
    {
        case HandleKind.TypeReference:
            var name = ScopeAssembly((TypeReferenceHandle)handle);
            return name != null && forbidden.Contains(name) ? new[] { name } : Enumerable.Empty<string>();
        case HandleKind.TypeSpecification:
            return reader.GetTypeSpecification((TypeSpecificationHandle)handle)
                .DecodeSignature(provider, null);
        default:
            return Enumerable.Empty<string>();
    }
}

foreach (var handle in reader.TypeDefinitions)
{
    var type = reader.GetTypeDefinition(handle);
    var name = TypeName(type);

    // 1. Base type — the killer. Resolved the moment the type is loaded.
    foreach (var hit in ForbiddenIn(type.BaseType))
        errors.Add($"{name} derives from a {hit} type");

    // 2. Interfaces — resolved with the type, same as the base type.
    foreach (var implHandle in type.GetInterfaceImplementations())
    {
        var impl = reader.GetInterfaceImplementation(implHandle);
        foreach (var hit in ForbiddenIn(impl.Interface))
            errors.Add($"{name} implements a {hit} interface");
    }

    // 3. Field types — part of the type's layout, resolved with it.
    foreach (var fieldHandle in type.GetFields())
    {
        var field = reader.GetFieldDefinition(fieldHandle);
        foreach (var hit in field.DecodeSignature(provider, null))
            errors.Add($"{name}.{reader.GetString(field.Name)} is typed on {hit}");
    }

    // 4. Method signatures — lazy, so only a warning (see the header).
    foreach (var methodHandle in type.GetMethods())
    {
        var method = reader.GetMethodDefinition(methodHandle);
        var signature = method.DecodeSignature(provider, null);
        var hits = signature.ReturnType.Concat(signature.ParameterTypes.SelectMany(p => p)).Distinct();
        foreach (var hit in hits)
            warnings.Add($"{name}.{reader.GetString(method.Name)} has {hit} in its signature");
    }
}

Console.WriteLine($"soft-dependency guard: {Path.GetFileName(target)} vs [{string.Join(", ", forbidden)}]");
foreach (var warning in warnings.Distinct()) Console.WriteLine("  warn: " + warning);
if (errors.Count == 0)
{
    Console.WriteLine("  OK — no forbidden assembly in the type surface; the module loads without them.");
    return 0;
}
foreach (var error in errors.Distinct()) Console.WriteLine("  FAIL: " + error);
Console.WriteLine("  These load EAGERLY: the game's GetTypes() scan throws and the mod fails to start");
Console.WriteLine("  for every player without that module. Move the type into a satellite assembly");
Console.WriteLine("  loaded by hand (see NavalBridge), or keep the foreign types in method bodies.");
return 1;

/// <summary>Decodes a signature blob down to just the forbidden assembly names it mentions.</summary>
internal sealed class AssemblyCollectingProvider : ISignatureTypeProvider<IEnumerable<string>, object?>
{
    private static readonly IEnumerable<string> None = Array.Empty<string>();
    private readonly MetadataReader _reader;
    private readonly HashSet<string> _forbidden;
    private readonly Func<TypeReferenceHandle, string?> _scope;

    public AssemblyCollectingProvider(MetadataReader reader, HashSet<string> forbidden,
        Func<TypeReferenceHandle, string?> scope)
    {
        _reader = reader;
        _forbidden = forbidden;
        _scope = scope;
    }

    public IEnumerable<string> GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var name = _scope(handle);
        return name != null && _forbidden.Contains(name) ? new[] { name } : None;
    }

    public IEnumerable<string> GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => None;
    public IEnumerable<string> GetTypeFromSpecification(MetadataReader reader, object? genericContext,
        TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public IEnumerable<string> GetSZArrayType(IEnumerable<string> elementType) => elementType;
    public IEnumerable<string> GetArrayType(IEnumerable<string> elementType, ArrayShape shape) => elementType;
    public IEnumerable<string> GetByReferenceType(IEnumerable<string> elementType) => elementType;
    public IEnumerable<string> GetPointerType(IEnumerable<string> elementType) => elementType;
    public IEnumerable<string> GetPinnedType(IEnumerable<string> elementType) => elementType;
    public IEnumerable<string> GetModifiedType(IEnumerable<string> modifier, IEnumerable<string> unmodifiedType,
        bool isRequired) => modifier.Concat(unmodifiedType);
    public IEnumerable<string> GetGenericInstantiation(IEnumerable<string> genericType,
        ImmutableArray<IEnumerable<string>> typeArguments)
        => genericType.Concat(typeArguments.SelectMany(a => a));
    public IEnumerable<string> GetFunctionPointerType(MethodSignature<IEnumerable<string>> signature)
        => signature.ReturnType.Concat(signature.ParameterTypes.SelectMany(p => p));
    public IEnumerable<string> GetGenericMethodParameter(object? genericContext, int index) => None;
    public IEnumerable<string> GetGenericTypeParameter(object? genericContext, int index) => None;
    public IEnumerable<string> GetPrimitiveType(PrimitiveTypeCode typeCode) => None;
}
