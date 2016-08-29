using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;

public class ModuleWeaver
{
    // Will contain the full element XML from FodyWeavers.xml. OPTIONAL
    public XElement Config { get; set; }

    // Will log an MessageImportance.Normal message to MSBuild. OPTIONAL
    public Action<string> LogDebug { get; set; }

    // Will log an MessageImportance.High message to MSBuild. OPTIONAL
    public Action<string> LogInfo { get; set; }

    // Will log a message to MSBuild. OPTIONAL
    public Action<string, MessageImportance> LogMessage { get; set; }

    // Will log an warning message to MSBuild. OPTIONAL
    public Action<string> LogWarning { get; set; }

    // Will log an warning message to MSBuild at a specific point in the code. OPTIONAL
    public Action<string, SequencePoint> LogWarningPoint { get; set; }

    // Will log an error message to MSBuild. OPTIONAL
    public Action<string> LogError { get; set; }

    // Will log an error message to MSBuild at a specific point in the code. OPTIONAL
    public Action<string, SequencePoint> LogErrorPoint { get; set; }

    // An instance of Mono.Cecil.IAssemblyResolver for resolving assembly references. OPTIONAL
    public IAssemblyResolver AssemblyResolver { get; set; }

    // An instance of Mono.Cecil.ModuleDefinition for processing. REQUIRED
    public ModuleDefinition ModuleDefinition { get; set; }

    // Will contain the full path of the target assembly. OPTIONAL
    public string AssemblyFilePath { get; set; }

    // Will contain the full directory path of the target project. 
    // A copy of $(ProjectDir). OPTIONAL
    public string ProjectDirectoryPath { get; set; }

    // Will contain the full directory path of the current weaver. OPTIONAL
    public string AddinDirectoryPath { get; set; }

    // Will contain the full directory path of the current solution.
    // A copy of `$(SolutionDir)` or, if it does not exist, a copy of `$(MSBuildProjectDirectory)..\..\..\`. OPTIONAL
    public string SolutionDirectoryPath { get; set; }

    // Will contain a semicomma delimetered string that contains 
    // all the references for the target project. 
    // A copy of the contents of the @(ReferencePath). OPTIONAL
    public string References { get; set; }

    // Will a list of all the references marked as copy-local. 
    // A copy of the contents of the @(ReferenceCopyLocalPaths). OPTIONAL
    public List<string> ReferenceCopyLocalPaths { get; set; }

    // Will a list of all the msbuild constants. 
    // A copy of the contents of the $(DefineConstants). OPTIONAL
    public List<string> DefineConstants { get; set; }

    private static readonly MethodInfo _stringJoinMethod = typeof( string ).GetMethods().Where( x => x.Name == nameof( string.Join ) ).Single( x =>
    {
        var parameters = x.GetParameters();
        return parameters.Length == 2 && parameters[0].ParameterType == typeof( string ) &&
                parameters[1].ParameterType == typeof( object[] );
    } );
    private static readonly MethodInfo _stringformatMethod = typeof( string ).GetMethods().Where( x => x.Name == nameof( string.Format ) ).Single( x =>
    {
        var parameters = x.GetParameters();
        return parameters.Length == 2 && parameters[0].ParameterType == typeof( string ) &&
                parameters[1].ParameterType == typeof( object );
    } );
    private static readonly MethodInfo _debugWriteLineMethod = typeof( System.Diagnostics.Debug ).GetMethods().Where( x => x.Name == nameof( System.Diagnostics.Debug.WriteLine ) ).Single( x =>
    {
        var parameters = x.GetParameters();
        return parameters.Length == 1 && parameters[0].ParameterType == typeof( string );
    } );

    // Init logging delegates to make testing easier
    public ModuleWeaver()
    {
        LogDebug = m => { };
        LogInfo = m => { };
        LogWarning = m => { };
        LogWarningPoint = ( m, p ) => { };
        LogError = m => { };
        LogErrorPoint = ( m, p ) => { };

    }

    public void Execute()
    {
        foreach ( TypeDefinition type in ModuleDefinition.Types )
        {
            foreach ( MethodDefinition method in type.Methods )
            {
                ProcessMethod( method );
            }
        }
    }

    private void ProcessMethod( MethodDefinition method )
    {
        ILProcessor processor = method.Body.GetILProcessor();
        Instruction current = method.Body.Instructions.First();

        Instruction first = Instruction.Create( OpCodes.Nop );
        processor.InsertBefore( current, first );
        current = first;

        foreach ( Instruction instruction in GetInstructions( method ) )
        {
            processor.InsertAfter( current, instruction );
            current = instruction;
        }
    }

    private IEnumerable<Instruction> GetInstructions( MethodDefinition method )
    {
        yield return Instruction.Create( OpCodes.Ldstr, $"DEBUG: {method.Name}({{0}})" );
        yield return Instruction.Create( OpCodes.Ldstr, "," );

        yield return Instruction.Create( OpCodes.Ldc_I4, method.Parameters.Count );
        yield return Instruction.Create( OpCodes.Newarr, ModuleDefinition.ImportReference( typeof( object ) ) );

        for ( int i = 0; i < method.Parameters.Count; i++ )
        {
            yield return Instruction.Create( OpCodes.Dup );
            yield return Instruction.Create( OpCodes.Ldc_I4, i );
            yield return Instruction.Create( OpCodes.Ldarg, method.Parameters[i] );
            if ( method.Parameters[i].ParameterType.IsValueType )
                yield return Instruction.Create( OpCodes.Box, method.Parameters[i].ParameterType );
            yield return Instruction.Create( OpCodes.Stelem_Ref );
        }

        yield return Instruction.Create( OpCodes.Call, ModuleDefinition.ImportReference( _stringJoinMethod ) );
        yield return Instruction.Create( OpCodes.Call, ModuleDefinition.ImportReference( _stringformatMethod ) );
        yield return Instruction.Create( OpCodes.Call, ModuleDefinition.ImportReference( _debugWriteLineMethod ) );
    }

    // Will be called when a request to cancel the build occurs. OPTIONAL
    public void Cancel()
    {
    }

    // Will be called after all weaving has occurred and the module has been saved. OPTIONAL
    public void AfterWeaving()
    {
    }
}