using System.Reflection.Emit;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Reflection.Metadata;
using Microsoft.VisualBasic.FileIO;

namespace QueryProjection;

public static class AnonymousTypeGenerator
{
    private static readonly Dictionary<string, Type> PreviousAnonTypesCache = [];
    private static readonly CustomAttributeBuilder CompilerGeneratedAttributeBuilder = new CustomAttributeBuilder(typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes)!, []);
    private static ModuleBuilder? AnonymousTypeModuleBuilder;
    private static int AssemblyCounter = 25;

    public static Type CreateAnonymousType(IDictionary<string, Type> propertyTypes)
    {
        // Find or create AssemblyBuilder for dynamic assembly
        if (AnonymousTypeModuleBuilder == null)
        {
            var assemblyName = new AssemblyName($"DynamicAssembly{AssemblyCounter}");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            AnonymousTypeModuleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
        }

        // Get a dynamic TypeBuilder
        var typeBuilder = AnonymousTypeModuleBuilder.DefineType($"<>f__AnonymousType{AssemblyCounter++}`{propertyTypes.Count}",
                                                                 TypeAttributes.AnsiClass | TypeAttributes.Class | TypeAttributes.AutoLayout |
                                                                 TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);
        typeBuilder.SetCustomAttribute(CompilerGeneratedAttributeBuilder);

        // Define generic type parameters
        var genericTypeParameterNames = propertyTypes.Keys.Select(GenerateGenericTypeParameter).ToArray();
        var genericTypeParameterBuilders = typeBuilder.DefineGenericParameters(genericTypeParameterNames);
        var genericTypeParameterNameToBuilderMap = genericTypeParameterNames.Zip(genericTypeParameterBuilders, (name, builder) => new { name, builder }).ToDictionary(pair => pair.name, pair => pair.builder);

        // Add public fields to match the source object
        var fieldBuilders = new List<FieldBuilder>();
        foreach (var propertyName in propertyTypes.Keys)
            fieldBuilders.Add(typeBuilder.DefineField(propertyName, genericTypeParameterNameToBuilderMap[GenerateGenericTypeParameter(propertyName)], FieldAttributes.Public));

        var fieldTypeList = propertyTypes.Values.ToArray();

        // Define constructor
        var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, fieldTypeList);
        var constructorIL = constructor.GetILGenerator();

        // Generate IL to assign fields
        for (int i = 0; i < fieldTypeList.Length; i++)
        {
            var field = fieldBuilders[i];

            constructorIL.Emit(OpCodes.Ldarg_0);
            constructorIL.Emit(OpCodes.Ldarg_S, i + 1);
            constructorIL.Emit(OpCodes.Stfld, field);
        }

        constructorIL.Emit(OpCodes.Ret);

        return typeBuilder.CreateType().MakeGenericType(fieldTypeList);

        // Method to generate generic type parameter names
        string GenerateGenericTypeParameter(string fieldName)
        {
            return $"<{fieldName}>j__TPar";
        }
    }

    public static Type FindOrCreateAnonymousType(IDictionary<string, Type> objDict)
    {
        Type? result = null;
        var wantedKey = CreateAnonymousTypeKey(objDict);

        lock (PreviousAnonTypesCache)
        {
            if (!PreviousAnonTypesCache.TryGetValue(wantedKey, out result))
            {
                result = CreateAnonymousType(objDict);
                PreviousAnonTypesCache[wantedKey] = result;
            }
        }

        return result;
    }

    private static string CreateAnonymousTypeKey(IDictionary<string, Type> objDict)
        => String.Join('|', objDict.Select(d => $"{d.Key}~{d.Value}"));
}
